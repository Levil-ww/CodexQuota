using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
class QuotaTests {
    static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    static void Main() {
        var s = QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":90,\"windowDurationMins\":300}},\"rateLimitsByLimitId\":{\"codex\":{\"primary\":{\"usedPercent\":31,\"windowDurationMins\":10080,\"resetsAt\":1792649982},\"secondary\":{\"usedPercent\":1,\"windowDurationMins\":300,\"resetsAt\":1792149221}}}}");
        Check(s.FiveHour.Remaining == 99 && s.Weekly.Remaining == 69, "duration identifies windows and multi-bucket takes priority");
        Check(s.FiveHour.Reset.HasValue, "Unix reset timestamp parsed");
        s = QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":120,\"windowDurationMins\":300},\"secondary\":null}}");
        Check(s.FiveHour.Remaining == 0 && s.Weekly == null, "clamp overshoot and preserve missing window");
        s = QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"windowDurationMins\":300}}}");
        Check(s.FiveHour == null, "missing percentage must not display full quota");
        s = QuotaSnapshot.Parse("{\"rateLimitsByLimitId\":{\"other\":{\"primary\":{\"usedPercent\":1,\"windowDurationMins\":300}}}}");
        Check(s.FiveHour == null && s.Weekly == null, "do not substitute another account bucket");
        bool rejected = false; try { QuotaSnapshot.Parse("broken"); } catch { rejected = true; }
        Check(rejected, "reject malformed payload");
        string link = Path.Combine(Path.GetTempPath(), "codex-quota-test-" + Guid.NewGuid().ToString("N") + ".lnk");
        string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodexQuota.exe");
        try {
            StartupShortcut.Set(link, exe, true);
            Check(File.Exists(link), "create startup shortcut");
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            object shortcut = null;
            try {
                shortcut = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { link });
                var target = (string)shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                Check(string.Equals(target, exe, StringComparison.OrdinalIgnoreCase), "shortcut launches actual widget executable");
            } finally { if (shortcut != null) Marshal.FinalReleaseComObject(shortcut); Marshal.FinalReleaseComObject(shell); }
            StartupShortcut.Set(link, exe, false);
            Check(!File.Exists(link), "disable startup deletes shortcut");
        } finally { if (File.Exists(link)) File.Delete(link); }
    }
}
