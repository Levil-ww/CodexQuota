using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using System.Runtime.InteropServices;

static class Program {
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static int Main(string[] args) {
        if (Array.IndexOf(args, "--probe") >= 0) {
            try {
                var q = CodexClient.Read(CancellationToken.None).GetAwaiter().GetResult();
                string result = "5H=" + (q.FiveHour == null ? "unknown" : q.FiveHour.Percent) + "; Weekly=" + (q.Weekly == null ? "unknown" : q.Weekly.Percent);
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-result.txt"), result);
                return q.FiveHour == null && q.Weekly == null ? 2 : 0;
            } catch (Exception ex) { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "probe-result.txt"), ex.Message); return 1; }
        }
        bool owned;
        using (var mutex = new Mutex(true, "Local\\CodexQuota.Widget.v1", out owned)) {
            if (!owned) return 0;
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new QuotaForm(Array.IndexOf(args, "--smoke-test") >= 0)); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Codex Quota", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
        return 0;
    }
}

public sealed class WidgetSettings { public int X; public int Y; public bool HasPosition; }

public sealed class QuotaForm : Form {
    readonly NotifyIcon tray;
    readonly ToolTip tooltip;
    readonly ContextMenuStrip menu;
    readonly ToolStripMenuItem startupItem;
    readonly System.Windows.Forms.Timer timer;
    readonly CancellationTokenSource closing = new CancellationTokenSource();
    readonly string settingsFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
    readonly bool smokeTest;
    QuotaSnapshot snapshot;
    DateTime? updated;
    string error;
    bool refreshing;
    bool exiting;
    Point dragStart, windowStart;
    bool dragging;
    int scale;

    public QuotaForm(bool smoke) {
        smokeTest = smoke;
        Text = "Codex Quota";
        AutoScaleMode = AutoScaleMode.None;
        using (var g = CreateGraphics()) scale = Math.Max(100, (int)(g.DpiX / 96f * 100));
        ClientSize = new Size(S(298), S(40));
        FormBorderStyle = FormBorderStyle.None;
        TopMost = true; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(27, 32, 44); ForeColor = Color.FromArgb(239, 243, 250);
        DoubleBuffered = true;
        Font = new Font("Microsoft YaHei UI", 9f);
        menu = new ContextMenuStrip();
        menu.Items.Add("立即刷新", null, async delegate { await RefreshQuota(); });
        menu.Items.Add("显示 / 隐藏悬浮窗", null, delegate { ToggleVisible(); });
        menu.Items.Add("恢复顶部居中", null, delegate { CenterTop(); Show(); SavePosition(); });
        startupItem = new ToolStripMenuItem("开机启动") { Checked = StartupEnabled() };
        startupItem.Click += delegate {
            try { SetStartup(!StartupEnabled()); startupItem.Checked = StartupEnabled(); }
            catch (Exception ex) { MessageBox.Show("无法修改开机启动：" + ex.Message, Text); }
        };
        menu.Items.Add(startupItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("退出", null, delegate { exiting = true; Close(); });
        ContextMenuStrip = menu;
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "Codex Quota", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += delegate { Show(); TopMost = true; };
        tooltip = new ToolTip { AutoPopDelay = 30000, InitialDelay = 250, ReshowDelay = 100, ShowAlways = true };
        timer = new System.Windows.Forms.Timer { Interval = 60000 };
        timer.Tick += async delegate { await RefreshQuota(); };
        MouseDown += delegate(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) { dragging = true; dragStart = Cursor.Position; windowStart = Location; Capture = true; }
        };
        MouseMove += delegate {
            if (dragging) { var p = Cursor.Position; Location = new Point(windowStart.X + p.X - dragStart.X, windowStart.Y + p.Y - dragStart.Y); }
        };
        MouseUp += delegate {
            if (dragging) { dragging = false; Capture = false; ClampPosition(); SavePosition(); }
        };
        Shown += async delegate {
            RoundWindow(); LoadPosition(); UpdateTooltip();
            timer.Start();
            if (smokeTest) {
                snapshot = QuotaSnapshot.Parse("{\"rateLimits\":{\"primary\":{\"usedPercent\":1,\"windowDurationMins\":300,\"resetsAt\":1792149221},\"secondary\":{\"usedPercent\":31,\"windowDurationMins\":10080,\"resetsAt\":1792649982}}}");
                updated = DateTime.Now; UpdateTooltip(); Invalidate();
                await Task.Delay(800);
                using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, ClientRectangle); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview.png")); }
                exiting = true; Close();
            } else await RefreshQuota();
        };
    }
    int S(int value) { return value * scale / 100; }
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080; return p; } }
    void RoundWindow() {
        using (var path = new GraphicsPath()) {
            int r = S(18); path.AddArc(0, 0, r, r, 180, 90); path.AddArc(Width-r-1, 0, r, r, 270, 90);
            path.AddArc(Width-r-1, Height-r-1, r, r, 0, 90); path.AddArc(0, Height-r-1, r, r, 90, 90); path.CloseFigure();
            var old = Region; Region = new Region(path); if (old != null) old.Dispose();
        }
    }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var dot = new SolidBrush(error != null ? Color.FromArgb(251, 180, 76) : snapshot == null ? Color.Gray : Color.FromArgb(76, 220, 164))) e.Graphics.FillEllipse(dot, S(12), S(17), S(6), S(6));
        TextRenderer.DrawText(e.Graphics, "Codex", Font, new Rectangle(S(27), 0, S(57), Height), ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        using (var pen = new Pen(Color.FromArgb(65, 72, 89))) e.Graphics.DrawLine(pen, S(84), S(12), S(84), S(28));
        DrawQuota(e.Graphics, "5H ", snapshot == null ? null : snapshot.FiveHour, 96, 90);
        DrawQuota(e.Graphics, "周 ", snapshot == null ? null : snapshot.Weekly, 196, 92);
    }
    void DrawQuota(Graphics g, string prefix, QuotaWindow w, int x, int width) {
        Color color = w == null ? Color.LightGray : error != null ? Color.FromArgb(251,180,76) : w.Remaining <= 10 ? Color.FromArgb(255,116,126) : w.Remaining <= 25 ? Color.FromArgb(251,180,76) : Color.FromArgb(142,235,200);
        TextRenderer.DrawText(g, prefix + (w == null ? "--" : w.Percent) + (error != null && w != null ? "*" : ""), Font, new Rectangle(S(x), 0, S(width), Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
    async Task RefreshQuota() {
        if (refreshing || exiting || smokeTest) return;
        refreshing = true;
        try {
            var result = await CodexClient.Read(closing.Token);
            if (exiting) return;
            if (result.FiveHour == null && result.Weekly == null) throw new Exception("服务器未返回 5 小时或每周额度。");
            snapshot = result; updated = DateTime.Now; error = null;
        } catch (OperationCanceledException) { return; }
        catch (Exception ex) { if (!exiting) error = ex.Message; }
        finally { refreshing = false; if (!exiting) { UpdateTooltip(); Invalidate(); } }
    }
    string ResetText(QuotaWindow w) { return w == null || !w.Reset.HasValue ? "未知" : w.Reset.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz"); }
    void UpdateTooltip() {
        string text = "Codex 剩余额度\n5 小时：" + (snapshot == null || snapshot.FiveHour == null ? "未知" : snapshot.FiveHour.Percent) + "\n重置：" + ResetText(snapshot == null ? null : snapshot.FiveHour)
            + "\n每周：" + (snapshot == null || snapshot.Weekly == null ? "未知" : snapshot.Weekly.Percent) + "\n重置：" + ResetText(snapshot == null ? null : snapshot.Weekly)
            + "\n更新：" + (updated.HasValue ? updated.Value.ToString("MM-dd HH:mm:ss") : "尚未成功")
            + (error == null ? "" : "\n⚠ " + error + (snapshot != null ? "\n* 数据已过期，保留上次读数" : ""))
            + "\n左键拖动 · 右键菜单 · 每分钟刷新";
        tooltip.SetToolTip(this, text);
        tray.Text = "Codex 5H " + (snapshot == null || snapshot.FiveHour == null ? "--" : snapshot.FiveHour.Percent) + " | 周 " + (snapshot == null || snapshot.Weekly == null ? "--" : snapshot.Weekly.Percent) + (error == null ? "" : " (未更新)");
    }
    void ToggleVisible() { if (Visible) Hide(); else Show(); }
    void CenterTop() { var a = Screen.PrimaryScreen.WorkingArea; Location = new Point(a.Left + (a.Width - Width) / 2, a.Top + S(8)); }
    void ClampPosition() {
        var a = Screen.FromRectangle(Bounds).WorkingArea;
        Location = new Point(Math.Max(a.Left, Math.Min(Left, a.Right - Width)), Math.Max(a.Top, Math.Min(Top, a.Bottom - Height)));
    }
    void LoadPosition() {
        CenterTop();
        try { if (File.Exists(settingsFile)) { var s = new JavaScriptSerializer().Deserialize<WidgetSettings>(File.ReadAllText(settingsFile)); if (s.HasPosition) Location = new Point(s.X, s.Y); } } catch {}
        ClampPosition();
    }
    void SavePosition() {
        try { File.WriteAllText(settingsFile, new JavaScriptSerializer().Serialize(new WidgetSettings {X = Left, Y = Top, HasPosition = true})); }
        catch (Exception ex) { MessageBox.Show("无法保存位置：" + ex.Message, Text); }
    }
    static string StartupPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Codex Quota.lnk"); } }
    static bool StartupEnabled() { return File.Exists(StartupPath); }
    static void SetStartup(bool enabled) {
        StartupShortcut.Set(StartupPath, Application.ExecutablePath, enabled);
    }
    protected override void OnFormClosing(FormClosingEventArgs e) {
        exiting = true; timer.Stop(); closing.Cancel(); tray.Visible = false;
        base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing) {
        if (disposing) { timer.Dispose(); tray.Dispose(); tooltip.Dispose(); menu.Dispose(); closing.Dispose(); }
        base.Dispose(disposing);
    }
}
