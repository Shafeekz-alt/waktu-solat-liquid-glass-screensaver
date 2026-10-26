using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        private System.Windows.Forms.Timer clockTimer;

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

            clockTimer = new System.Windows.Forms.Timer();
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

        private static string FetchJakimJson(string zone)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string url = "https://www.e-solat.gov.my/index.php?r=esolatApi/TakwimSolat&period=today&zone=" + Uri.EscapeDataString(zone);
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 4000;
                req.ReadWriteTimeout = 4000;
                req.UserAgent = "WaktuSolatLiquidGlass/1.0";

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch
            {
                return null;
            }
        }

        private static int StartLocalServer(string rootDir)
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            Thread serverThread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(_ => HandleClient(client, rootDir));
                    }
                    catch
                    {
                        break;
                    }
                }
            });
            serverThread.IsBackground = true;
            serverThread.Start();
            return port;
        }

        private static void HandleClient(TcpClient client, string rootDir)
        {
            try
            {
                using (client)
                using (NetworkStream stream = client.GetStream())
                using (StreamReader reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
                {
                    string requestLine = reader.ReadLine();
                    if (string.IsNullOrEmpty(requestLine)) return;

                    string[] parts = requestLine.Split(' ');
                    if (parts.Length < 2) return;

                    string rawPath = parts[1];
                    string pathOnly = rawPath;
                    string query = "";
                    int qIdx = rawPath.IndexOf('?');
                    if (qIdx >= 0)
                    {
                        pathOnly = rawPath.Substring(0, qIdx);
                        query = rawPath.Substring(qIdx + 1);
                    }

                    if (pathOnly.Equals("/api/esolat", StringComparison.OrdinalIgnoreCase))
                    {
                        string zone = "WLY01";
                        foreach (string kv in query.Split('&'))
                        {
                            if (kv.StartsWith("zone=", StringComparison.OrdinalIgnoreCase))
                            {
                                zone = Uri.UnescapeDataString(kv.Substring(5));
                            }
                        }

                        string json = FetchJakimJson(zone);
                        if (!string.IsNullOrEmpty(json))
                        {
                            byte[] body = Encoding.UTF8.GetBytes(json);
                            WriteHttpResponse(stream, "200 OK", "application/json; charset=utf-8", body);
                        }
                        else
                        {
                            byte[] body = Encoding.UTF8.GetBytes("{\"status\":\"OFFLINE\"}");
                            WriteHttpResponse(stream, "503 Service Unavailable", "application/json", body);
                        }
                        return;
                    }

                    if (pathOnly == "/") pathOnly = "/index.html";
                    string relPath = Uri.UnescapeDataString(pathOnly.TrimStart('/')).Replace('/', Path.DirectorySeparatorChar);
                    string fullPath = Path.GetFullPath(Path.Combine(rootDir, relPath));

                    if (!fullPath.StartsWith(Path.GetFullPath(rootDir), StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                    {
                        WriteHttpResponse(stream, "404 Not Found", "text/plain", Encoding.UTF8.GetBytes("Not Found"));
                        return;
                    }

                    string ext = Path.GetExtension(fullPath).ToLowerInvariant();
                    string contentType = "application/octet-stream";
                    if (ext == ".html") contentType = "text/html; charset=utf-8";
                    else if (ext == ".js") contentType = "application/javascript; charset=utf-8";
                    else if (ext == ".css") contentType = "text/css; charset=utf-8";
                    else if (ext == ".jpg" || ext == ".jpeg") contentType = "image/jpeg";
                    else if (ext == ".png") contentType = "image/png";

                    byte[] fileBytes = File.ReadAllBytes(fullPath);
                    WriteHttpResponse(stream, "200 OK", contentType, fileBytes);
                }
            }
            catch
            {
                // Ignore client disconnect errors
            }
        }

        private static void WriteHttpResponse(NetworkStream stream, string status, string contentType, byte[] body)
        {
            string headers = string.Format(
                "HTTP/1.1 {0}\r\nContent-Type: {1}\r\nContent-Length: {2}\r\nAccess-Control-Allow-Origin: *\r\nCache-Control: no-cache\r\nConnection: close\r\n\r\n",
                status, contentType, body.Length);
            byte[] headerBytes = Encoding.ASCII.GetBytes(headers);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static void LaunchLiquidGlassUI(string mode)
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string htmlPath = Path.Combine(exeDir, "index.html");

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

            int port = StartLocalServer(exeDir);
            string appUrl = string.Format("http://127.0.0.1:{0}/index.html?mode={1}", port, mode);

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
                // Only clean, supported Edge flags — no warning banner at the top!
                psi.Arguments = string.Format(
                    "--user-data-dir=\"{0}\" --app=\"{1}\" --start-fullscreen --no-first-run --no-default-browser-check",
                    profileDir, appUrl);
                psi.UseShellExecute = false;

                Process proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit();
                }
            }
            else
            {
                Process.Start(appUrl);
            }
        }
    }
}
