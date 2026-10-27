using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace ProLightClean
{
    public class FormLauncher : Form
    {
        private TextBox txtUsername;
        private TextBox txtPassword;
        private TextBox txtLfsPath;
        private Button btnBrowse;
        private TextBox txtEndpoint;
        private Button btnInspectHwid;
        private Button btnLogin;
        private Button btnLaunch;
        private Button btnFixHosts;
        private TextBox txtLogs;
        private Label lblStatus;

        public FormLauncher()
        {
            InitializeComponent();
            MasterServerBridge.OnLog += msg =>
            {
                if (IsHandleCreated)
                    BeginInvoke((MethodInvoker)(() => AppendLog(msg)));
            };
        }

        private void InitializeComponent()
        {
            this.Text = "ProLight Clean Reborn — LFS 0.6V Launcher";
            this.Size = new Size(680, 620);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(20, 24, 32);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5f);

            // Title
            var lblTitle = new Label
            {
                Text = "⚡ PROLIGHT CLEAN REBORN",
                Location = new Point(25, 20),
                Size = new Size(400, 30),
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 195, 255)
            };
            this.Controls.Add(lblTitle);

            var lblSub = new Label
            {
                Text = "Independent Lightweight Launcher & S3 Master Bridge (45 KB)",
                Location = new Point(27, 52),
                Size = new Size(500, 22),
                ForeColor = Color.LightGray
            };
            this.Controls.Add(lblSub);

            // Group 1: Credentials
            var grpLogin = new GroupBox
            {
                Text = " Account Credentials ",
                Location = new Point(25, 85),
                Size = new Size(615, 120),
                ForeColor = Color.White
            };
            this.Controls.Add(grpLogin);

            var lblUser = new Label { Text = "Username:", Location = new Point(20, 30), Size = new Size(80, 25) };
            txtUsername = new TextBox { Location = new Point(105, 27), Size = new Size(180, 25), Text = "Pilot01" };
            var lblPass = new Label { Text = "Password:", Location = new Point(310, 30), Size = new Size(80, 25) };
            txtPassword = new TextBox { Location = new Point(395, 27), Size = new Size(190, 25), UseSystemPasswordChar = true, Text = "123456" };

            var lblApi = new Label { Text = "Server API:", Location = new Point(20, 72), Size = new Size(80, 25) };
            txtEndpoint = new TextBox { Location = new Point(105, 69), Size = new Size(480, 25), Text = "http://localhost:8080/api.php" };

            grpLogin.Controls.Add(lblUser);
            grpLogin.Controls.Add(txtUsername);
            grpLogin.Controls.Add(lblPass);
            grpLogin.Controls.Add(txtPassword);
            grpLogin.Controls.Add(lblApi);
            grpLogin.Controls.Add(txtEndpoint);

            // Group 2: Game Setup
            var grpGame = new GroupBox
            {
                Text = " LFS.exe Directory & Path ",
                Location = new Point(25, 215),
                Size = new Size(615, 95),
                ForeColor = Color.White
            };
            this.Controls.Add(grpGame);

            txtLfsPath = new TextBox { Location = new Point(20, 35), Size = new Size(465, 25) };
            btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(495, 33),
                Size = new Size(95, 28),
                BackColor = Color.FromArgb(45, 52, 64),
                FlatStyle = FlatStyle.Flat
            };
            btnBrowse.Click += (s, e) => BrowseLfs();
            grpGame.Controls.Add(txtLfsPath);
            grpGame.Controls.Add(btnBrowse);

            // Action Buttons
            btnInspectHwid = new Button
            {
                Text = "🔍 Inspect HWID",
                Location = new Point(25, 325),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(50, 60, 75),
                FlatStyle = FlatStyle.Flat
            };
            btnInspectHwid.Click += (s, e) => InspectHwid();
            this.Controls.Add(btnInspectHwid);

            btnFixHosts = new Button
            {
                Text = "🛡️ Add 127.0.0.1 Hosts",
                Location = new Point(175, 325),
                Size = new Size(160, 36),
                BackColor = Color.FromArgb(50, 60, 75),
                FlatStyle = FlatStyle.Flat
            };
            btnFixHosts.Click += (s, e) => FixHosts();
            this.Controls.Add(btnFixHosts);

            btnLogin = new Button
            {
                Text = "🚀 Authenticate",
                Location = new Point(345, 325),
                Size = new Size(140, 36),
                BackColor = Color.FromArgb(0, 120, 215),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnLogin.Click += (s, e) => DoLogin();
            this.Controls.Add(btnLogin);

            btnLaunch = new Button
            {
                Text = "🏁 Launch LFS",
                Location = new Point(495, 325),
                Size = new Size(145, 36),
                BackColor = Color.FromArgb(38, 166, 91),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
            };
            btnLaunch.Click += (s, e) => LaunchGame();
            this.Controls.Add(btnLaunch);

            // Status label
            lblStatus = new Label
            {
                Text = "Ready to launch.",
                Location = new Point(25, 372),
                Size = new Size(615, 25),
                ForeColor = Color.Yellow
            };
            this.Controls.Add(lblStatus);

            // Log output
            txtLogs = new TextBox
            {
                Location = new Point(25, 405),
                Size = new Size(615, 155),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.FromArgb(12, 14, 18),
                ForeColor = Color.LimeGreen,
                Font = new Font("Consolas", 9f)
            };
            this.Controls.Add(txtLogs);

            // Auto-detect LFS path
            AutoDetectLfs();
        }

        private void AppendLog(string msg)
        {
            txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
        }

        private void AutoDetectLfs()
        {
            string[] candidates = {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "LFS.exe"),
                @"C:\LFS\LFS.exe",
                @"D:\LFS\LFS.exe",
                "/root/prolight_clean/data/LFS.exe"
            };

            foreach (var p in candidates)
            {
                if (File.Exists(p))
                {
                    txtLfsPath.Text = p;
                    AppendLog($"Found LFS at: {p}");
                    break;
                }
            }
        }

        private void BrowseLfs()
        {
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Live for Speed Executable (LFS.exe)|LFS.exe|Executable (*.exe)|*.exe";
                if (ofd.ShowDialog() == DialogResult.OK)
                    txtLfsPath.Text = ofd.FileName;
            }
        }

        private void InspectHwid()
        {
            AppendLog("--- HARDWARE FINGERPRINT ---");
            AppendLog($"MAC:  {HardwareInfo.GetMac()}");
            AppendLog($"MB:   {HardwareInfo.GetMotherboardSerial()}");
            AppendLog($"BIOS: {HardwareInfo.GetBiosUuid()}");
            AppendLog($"HDD:  {HardwareInfo.GetHddSerial()}");
            AppendLog($"Machine: {Environment.UserName}@{Environment.MachineName}");
            lblStatus.Text = "Hardware fingerprint captured.";
        }

        private void FixHosts()
        {
            bool ok = ProfileManager.EnsureHostsEntry();
            AppendLog(ok ? "Hosts file verified with 127.0.0.1 m.lfspro.net" : "Failed to update hosts (Run as Admin on Windows).");
        }

        private void DoLogin()
        {
            lblStatus.Text = "Authenticating with server...";
            AppendLog($"Sending login request to: {txtEndpoint.Text}...");

            var res = LoginClient.Authenticate(txtUsername.Text, txtPassword.Text, txtEndpoint.Text);
            if (res.Success)
            {
                lblStatus.Text = "✅ Login Successful!";
                AppendLog($"[Login] Status: {res.Status} - {res.Message}");
            }
            else
            {
                lblStatus.Text = "❌ Login Failed.";
                AppendLog($"[Login] Failed: {res.Message}");
            }
        }

        private void LaunchGame()
        {
            string lfsPath = txtLfsPath.Text.Trim();
            if (!File.Exists(lfsPath))
            {
                MessageBox.Show("Cannot find LFS.exe at specified path.", "File Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string lfsDir = Path.GetDirectoryName(lfsPath);
            string user = txtUsername.Text.Trim();
            string pass = txtPassword.Text.Trim();

            // 1. Prepare profile & cfg
            ProfileManager.SetupPlayerProfile(lfsDir, user, pass);
            AppendLog($"Generated player profile for '{user}'");

            // 2. Start local master bridge on port 29339
            MasterServerBridge.Start(29339, user);

            // 3. Launch LFS.exe
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = lfsPath,
                    WorkingDirectory = lfsDir,
                    Arguments = $"/user={user} /pass={pass} /usemaster=yes",
                    UseShellExecute = true
                };

                Process lfsProc = Process.Start(psi);
                AppendLog($"LFS.exe process started (PID: {lfsProc.Id})");

                // 4. Inject memory after process initialized
                System.Threading.ThreadPool.QueueUserWorkItem(s =>
                {
                    System.Threading.Thread.Sleep(800);
                    try
                    {
                        using (var mem = new LFSMemory(lfsProc))
                        {
                            mem.ApplyMasterRedirection(user);
                            AppendLog("Memory injection complete (Master domains, Watermark, Username buffers applied).");
                        }
                    }
                    catch (Exception ex)
                    {
                        AppendLog($"Memory injection notice: {ex.Message}");
                    }
                });

                lblStatus.Text = "Game launched with S3 auto-unlock!";
            }
            catch (Exception ex)
            {
                AppendLog($"Launch error: {ex.Message}");
            }
        }
    }
}
