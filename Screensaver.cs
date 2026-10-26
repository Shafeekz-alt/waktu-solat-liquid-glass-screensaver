using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace WaktuSolatScreensaver
{
    public class PreviewForm : Form
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect);

        private Timer clockTimer;

        public PreviewForm(IntPtr previewHandle)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.DoubleBuffered = true;

            Rectangle parentRect;
            GetClientRect(previewHandle, out parentRect);

            SetParent(this.Handle, previewHandle);
            SetWindowLong(this.Handle, -16, new IntPtr(GetWindowLong(this.Handle, -16) | 0x40000000));

            this.Location = new Point(0, 0);
            this.Size = parentRect.Size;

            clockTimer = new Timer();
            clockTimer.Interval = 1000;
            clockTimer.Tick += (s, e) => this.Invalidate();
            clockTimer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (LinearGradientBrush bg = new LinearGradientBrush(
                this.ClientRectangle,
                Color.FromArgb(14, 34, 64),
                Color.FromArgb(6, 12, 24),
                LinearGradientMode.ForwardDiagonal))
            {
                g.FillRectangle(bg, this.ClientRectangle);
            }

            Rectangle card = new Rectangle(
                this.Width / 10,
                this.Height / 6,
                this.Width * 8 / 10,
                this.Height * 2 / 3);

            using (SolidBrush glass = new SolidBrush(Color.FromArgb(45, 255, 255, 255)))
            using (Pen border = new Pen(Color.FromArgb(130, 110, 247, 220), 1.5f))
            {
                g.FillRectangle(glass, card);
                g.DrawRectangle(border, card);
            }

            StringFormat sf = new StringFormat();
            sf.Alignment = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;

            string timeStr = DateTime.Now.ToString("hh:mm tt");
            using (Font fTime = new Font("Segoe UI", Math.Max(10, this.Height / 7), FontStyle.Bold))
            using (Font fSub = new Font("Segoe UI", Math.Max(7, this.Height / 15), FontStyle.Regular))
            using (Brush white = new SolidBrush(Color.White))
            using (Brush aqua = new SolidBrush(Color.FromArgb(110, 247, 220)))
            {
                g.DrawString(timeStr, fTime, white, new PointF(this.Width / 2f, this.Height * 0.42f), sf);
                g.DrawString("Waktu Solat JAKIM • Liquid Glass", fSub, aqua, new PointF(this.Width / 2f, this.Height * 0.66f), sf);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string mode = "screensaver";

            if (args.Length > 0)
            {
                string firstArg = args[0].ToLowerInvariant().Trim();
                if (firstArg.StartsWith("/p") || firstArg.StartsWith("-p"))
                {
                    IntPtr previewHwnd = IntPtr.Zero;
                    if (firstArg.Length > 3 && firstArg[2] == ':')
                    {
                        long val;
                        if (long.TryParse(firstArg.Substring(3), out val))
                            previewHwnd = new IntPtr(val);
                    }
                    else if (args.Length > 1)
                    {
                        long val;
                        if (long.TryParse(args[1], out val))
                            previewHwnd = new IntPtr(val);
                    }

                    if (previewHwnd != IntPtr.Zero)
                    {
                        Application.Run(new PreviewForm(previewHwnd));
                    }
                    return;
                }
                else if (firstArg.StartsWith("/c") || firstArg.StartsWith("-c"))
                {
                    mode = "interactive";
                }
                else if (firstArg.StartsWith("/s") || firstArg.StartsWith("-s"))
                {
                    mode = "screensaver";
                }
                else if (firstArg.StartsWith("/i") || firstArg.StartsWith("-i"))
                {
                    mode = "interactive";
                }
            }

            LaunchLiquidGlassUI(mode);
        }

        private static void TryRefreshJakimCache(string baseDir)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "https://www.e-solat.gov.my/index.php?r=esolatApi/TakwimSolat&period=today&zone=WLY01");
                req.Timeout = 2500;
                req.ReadWriteTimeout = 2500;
                req.UserAgent = "WaktuSolatLiquidGlass/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string todayJson = reader.ReadToEnd();
                    if (!string.IsNullOrEmpty(todayJson) && todayJson.Contains("prayerTime"))
                    {
                        string cachePath = Path.Combine(baseDir, "jakim-cache.js");
                        string jsContent = "window.JAKIM_INITIAL_CACHE = { 'WLY01_today': " + todayJson + " };";
                        File.WriteAllText(cachePath, jsContent, Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // Ignore network errors; existing jakim-cache.js or offline fallback will be used
            }
        }

        private static void LaunchLiquidGlassUI(string mode)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(exeDir, "index.html");

            // Fallback to project folder if .scr was copied to System32
            if (!File.Exists(htmlPath))
            {
                string fallbackDir = @"C:\Users\SHAFIQ-JKMNT\.gemini\antigravity\scratch\waktu-solat-screensaver";
                htmlPath = Path.Combine(fallbackDir, "index.html");
                exeDir = fallbackDir;
            }

            if (!File.Exists(htmlPath))
            {
                MessageBox.Show("Tidak dapat mencari fail index.html Waktu Solat Screensaver.",
                    "Waktu Solat Liquid Glass", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            TryRefreshJakimCache(exeDir);

            string fileUri = new Uri(htmlPath).AbsoluteUri + "?mode=" + mode;
            string edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
            if (!File.Exists(edgePath))
            {
                edgePath = @"C:\Program Files\Microsoft\Edge\Application\msedge.exe";
            }

            string profileDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WaktuSolatLiquidGlass_EdgeProfile");

            if (File.Exists(edgePath))
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = edgePath;
                psi.Arguments = string.Format(
                    "--user-data-dir=\"{0}\" --app=\"{1}\" --start-fullscreen --disable-web-security --allow-file-access-from-files --no-first-run --no-default-browser-check",
                    profileDir, fileUri);
                psi.UseShellExecute = false;

                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit();
                }
            }
            else
            {
                Process.Start(fileUri);
            }
        }
    }
}
