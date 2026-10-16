using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

public static class CodexClient {
    public static string FindExecutable() {
        string overridePath = Environment.GetEnvironmentVariable("CODEX_QUOTA_CLI");
        if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;
        string bin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (Directory.Exists(bin)) {
            var files = Directory.GetFiles(bin, "codex.exe", SearchOption.AllDirectories);
            Array.Sort(files, delegate(string a, string b) { return File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)); });
            if (files.Length > 0) return files[0];
        }
        foreach (string dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
            try { string p = Path.Combine(dir.Trim('"'), "codex.exe"); if (File.Exists(p)) return p; } catch (ArgumentException) {}
        }
        throw new Exception("找不到 Codex CLI。请安装/更新 Codex，或设置 CODEX_QUOTA_CLI 为 codex.exe 路径。");
    }
    public static async Task<QuotaSnapshot> Read(CancellationToken token) {
        var info = new ProcessStartInfo(FindExecutable(), "app-server --listen stdio://") {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            WorkingDirectory = Path.GetTempPath()
        };
        using (var process = new Process { StartInfo = info }) {
            process.Start();
            // Drain stderr without retaining credentials or server diagnostics.
            process.ErrorDataReceived += delegate {};
            process.BeginErrorReadLine();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token)) {
                timeout.CancelAfter(TimeSpan.FromSeconds(25));
                using (timeout.Token.Register(delegate { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) {} })) {
                    try {
                        await process.StandardInput.WriteLineAsync("{\"id\":1,\"method\":\"initialize\",\"params\":{\"clientInfo\":{\"name\":\"codex_quota_widget\",\"title\":\"Codex Quota\",\"version\":\"1.0.0\"}}}");
                        await Response(process, 1, timeout.Token);
                        await process.StandardInput.WriteLineAsync("{\"method\":\"initialized\",\"params\":{}}");
                        await process.StandardInput.WriteLineAsync("{\"id\":2,\"method\":\"account/rateLimits/read\"}");
                        string result = await Response(process, 2, timeout.Token);
                        return QuotaSnapshot.Parse(result);
                    } catch (Exception) {
                        token.ThrowIfCancellationRequested();
                        if (timeout.IsCancellationRequested) throw new TimeoutException("连接超时，将在下一分钟重试。");
                        throw;
                    } finally {
                        try { if (!process.HasExited) { process.StandardInput.Close(); if (!process.WaitForExit(500)) process.Kill(); } } catch (InvalidOperationException) {}
                    }
                }
            }
        }
    }
    static async Task<string> Response(Process process, int id, CancellationToken token) {
        var serializer = new JavaScriptSerializer();
        while (true) {
            token.ThrowIfCancellationRequested();
            var line = await process.StandardOutput.ReadLineAsync();
            token.ThrowIfCancellationRequested();
            if (line == null) throw new Exception("Codex 连接已关闭。请检查 CLI 登录状态。");
            var obj = serializer.Deserialize<System.Collections.Generic.Dictionary<string, object>>(line);
            var responseId = QuotaSnapshot.Get(obj, "id");
            if (responseId == null || Convert.ToInt32(responseId) != id) continue;
            var error = QuotaSnapshot.Obj(QuotaSnapshot.Get(obj, "error"));
            if (error != null) {
                string msg = Convert.ToString(QuotaSnapshot.Get(error, "message"));
                if (msg.IndexOf("auth", StringComparison.OrdinalIgnoreCase) >= 0)
                    throw new Exception("Codex CLI 尚未登录当前账户。请在终端运行 codex login 后重试。");
                throw new Exception("读取额度失败：" + msg);
            }
            return serializer.Serialize(QuotaSnapshot.Get(obj, "result"));
        }
    }
}
