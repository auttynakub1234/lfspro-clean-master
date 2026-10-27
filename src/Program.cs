using System;
using System.IO;
using System.Windows.Forms;

namespace ProLightClean
{
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                RunCli(args);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormLauncher());
        }

        private static void RunCli(string[] args)
        {
            string cmd = args[0].ToLower();

            if (cmd == "--inspect-hwid")
            {
                Console.WriteLine("=== HARDWARE FINGERPRINT ===");
                Console.WriteLine($"MAC:        {HardwareInfo.GetMac()}");
                Console.WriteLine($"MB Serial:  {HardwareInfo.GetMotherboardSerial()}");
                Console.WriteLine($"BIOS UUID:  {HardwareInfo.GetBiosUuid()}");
                Console.WriteLine($"HDD Serial: {HardwareInfo.GetHddSerial()}");
                Console.WriteLine($"Payload:    {HardwareInfo.BuildPayload("TestUser", "123456")}");
                return;
            }

            if (cmd == "--login" && args.Length >= 3)
            {
                string u = args[1];
                string p = args[2];
                string ep = args.Length >= 4 ? args[3] : LoginClient.DefaultEndpoint;

                Console.WriteLine($"Authenticating user '{u}' with {ep}...");
                var res = LoginClient.Authenticate(u, p, ep);
                Console.WriteLine($"Status:  {res.Status}");
                Console.WriteLine($"Message: {res.Message}");
                return;
            }

            if (cmd == "--bridge")
            {
                string u = args.Length >= 2 ? args[1] : "Driver";
                Console.WriteLine($"Starting MasterServerBridge on port 29339 for user '{u}'...");
                MasterServerBridge.OnLog += msg => Console.WriteLine(msg);
                MasterServerBridge.Start(29339, u);
                Console.WriteLine("Press Enter to stop bridge.");
                Console.ReadLine();
                MasterServerBridge.Stop();
                return;
            }

            Console.WriteLine("ProLightClean CLI Options:");
            Console.WriteLine("  --inspect-hwid                   Display local hardware fingerprint");
            Console.WriteLine("  --login <user> <pass> [endpoint] Authenticate with master server");
            Console.WriteLine("  --bridge [username]              Run Master Server S3 bridge");
        }
    }
}
