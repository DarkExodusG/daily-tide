// Steady for Windows: a native window that hosts the Steady app.
// Built with the C# compiler that ships with Windows (.NET Framework 4.x) + Microsoft WebView2.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

[assembly: System.Reflection.AssemblyTitle("Steady")]
[assembly: System.Reflection.AssemblyProduct("Steady")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

class SteadyWindow : Form
{
    const string AppUrl = "https://darkexodusg.github.io/daily-tide/";
    static readonly Color Ground = Color.FromArgb(11, 26, 31);

    readonly WebView2 web = new WebView2();
    readonly string dataDir;
    readonly string boundsFile;

    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr value);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    [STAThread]
    static void Main()
    {
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); } catch { } // per-monitor v2: crisp on scaled displays
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        bool first;
        using (var mutex = new System.Threading.Mutex(true, "Steady.SingleInstance", out first))
        {
            if (!first) { return; } // already open
            Application.Run(new SteadyWindow());
        }
    }

    SteadyWindow()
    {
        dataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Steady");
        boundsFile = Path.Combine(dataDir, "window.txt");
        Directory.CreateDirectory(dataDir);

        Text = "Steady";
        BackColor = Ground;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        StartPosition = FormStartPosition.Manual;

        float scale;
        using (var g = CreateGraphics()) scale = g.DpiX / 96f;
        MinimumSize = new Size((int)(380 * scale), (int)(560 * scale));
        if (!TryRestoreBounds())
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var w = Math.Min((int)(1120 * scale), area.Width - 80);
            var h = Math.Min((int)(820 * scale), area.Height - 60);
            Bounds = new Rectangle(area.Left + (area.Width - w) / 2, area.Top + (area.Height - h) / 2, w, h);
        }

        web.Dock = DockStyle.Fill;
        web.DefaultBackgroundColor = Ground;
        web.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Path.Combine(dataDir, "WebData") };
        Controls.Add(web);

        HandleCreated += (s, e) => ApplyTitleBarTheme();
        Shown += (s, e) => ApplyTitleBarTheme();
        Load += async (s, e) =>
        {
            try
            {
                await web.EnsureCoreWebView2Async();
                var st = web.CoreWebView2.Settings;
                st.AreDevToolsEnabled = false;
                st.IsStatusBarEnabled = false;
                st.IsZoomControlEnabled = true;
                st.AreBrowserAcceleratorKeysEnabled = false;
                // links that try to open a new window go to the normal browser
                web.CoreWebView2.NewWindowRequested += (a, b) => { b.Handled = true; OpenExternal(b.Uri); };
                // camera (for scanning a pairing QR code) is allowed for the app's own site only
                web.CoreWebView2.PermissionRequested += (a, b) =>
                {
                    if (b.Uri.StartsWith(AppUrl, StringComparison.OrdinalIgnoreCase)) b.State = CoreWebView2PermissionState.Allow;
                };
                web.CoreWebView2.DocumentTitleChanged += (a, b) => { Text = "Steady"; };
                web.Source = new Uri(AppUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Steady couldn't start its display component (Microsoft Edge WebView2).\n\n" + ex.Message,
                    "Steady", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        };
        FormClosing += (s, e) => SaveBounds();
    }

    void ApplyTitleBarTheme()
    {
        // Dark title bar when Windows apps are set to dark mode, to match the app.
        int dark = 1;
        try
        {
            using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                var v = k == null ? null : k.GetValue("AppsUseLightTheme");
                if (v is int && (int)v == 1) dark = 0;
            }
        }
        catch { }
        try { DwmSetWindowAttribute(Handle, 20, ref dark, 4); } catch { }
        if (dark == 1)
        {
            // Windows 11: paint the title bar in the app's own background colour so it blends in.
            int caption = Ground.R | (Ground.G << 8) | (Ground.B << 16);   // COLORREF is 0x00BBGGRR
            int text = 0xE4 | (0xEE << 8) | (0xEC << 16);
            int border = caption;
            try { DwmSetWindowAttribute(Handle, 35, ref caption, 4); } catch { }
            try { DwmSetWindowAttribute(Handle, 36, ref text, 4); } catch { }
            try { DwmSetWindowAttribute(Handle, 34, ref border, 4); } catch { }
        }
    }

    static void OpenExternal(string uri)
    {
        try { if (uri.StartsWith("https://") || uri.StartsWith("http://")) Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
    }

    bool TryRestoreBounds()
    {
        try
        {
            if (!File.Exists(boundsFile)) return false;
            var p = File.ReadAllText(boundsFile).Split(',');
            var r = new Rectangle(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]));
            bool visible = false;
            foreach (var sc in Screen.AllScreens) if (sc.WorkingArea.IntersectsWith(r)) visible = true;
            if (!visible || r.Width < 200 || r.Height < 200) return false;
            Bounds = r;
            if (p.Length > 4 && p[4] == "max") WindowState = FormWindowState.Maximized;
            return true;
        }
        catch { return false; }
    }

    void SaveBounds()
    {
        try
        {
            var r = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            File.WriteAllText(boundsFile, string.Format("{0},{1},{2},{3},{4}", r.X, r.Y, r.Width, r.Height,
                WindowState == FormWindowState.Maximized ? "max" : "normal"));
        }
        catch { }
    }
}
