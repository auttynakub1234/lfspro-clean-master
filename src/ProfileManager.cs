using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ProLightClean
{
    public static class ProfileManager
    {
        public const string PLY_MAGIC = "LFSPLY";
        public const string USR_MAGIC = "LFSUSR";

        public static void SetupPlayerProfile(string lfsDir, string username, string password = "")
        {
            if (string.IsNullOrEmpty(lfsDir) || !Directory.Exists(lfsDir)) return;

            string miscDir = Path.Combine(lfsDir, "data", "misc");
            Directory.CreateDirectory(miscDir);

            string user = username.Trim().ToLower();

            // 1. Generate player .ply profile
            string plyPath = Path.Combine(miscDir, user + ".ply");
            if (!File.Exists(plyPath))
            {
                byte[] ply = new byte[512];
                byte[] magic = Encoding.ASCII.GetBytes(PLY_MAGIC);
                Array.Copy(magic, 0, ply, 0, magic.Length);

                byte[] name = Encoding.ASCII.GetBytes(user);
                Array.Copy(name, 0, ply, 16, Math.Min(name.Length, 23));
                Array.Copy(name, 0, ply, 48, Math.Min(name.Length, 23));

                File.WriteAllBytes(plyPath, ply);
            }

            // 2. Generate usr.bin
            string usrPath = Path.Combine(miscDir, "usr.bin");
            if (!File.Exists(usrPath))
            {
                byte[] usr = new byte[68];
                byte[] magic = Encoding.ASCII.GetBytes(USR_MAGIC);
                Array.Copy(magic, 0, usr, 0, magic.Length);
                usr[6] = 1; // 1 user count

                byte[] name = Encoding.ASCII.GetBytes(user);
                Array.Copy(name, 0, usr, 8, Math.Min(name.Length, 23));
                usr[36] = 3; // S3 license level

                File.WriteAllBytes(usrPath, usr);
            }

            // 3. Update cfg.txt with user_name, user_pass, usemaster
            string cfgPath = Path.Combine(lfsDir, "cfg.txt");
            try
            {
                string text = File.Exists(cfgPath) ? File.ReadAllText(cfgPath) : "";
                text = UpdateCfgLine(text, "user_name", user);
                text = UpdateCfgLine(text, "user_pass", password);
                text = UpdateCfgLine(text, "usemaster", "yes");
                File.WriteAllText(cfgPath, text);
            }
            catch { }
        }

        private static string UpdateCfgLine(string content, string key, string value)
        {
            string pat = @"(?m)^" + Regex.Escape(key) + @"\s+.*$";
            string rep = key + " " + value;
            if (Regex.IsMatch(content, pat))
                return Regex.Replace(content, pat, rep);
            return content.TrimEnd() + "\r\n" + rep + "\r\n";
        }

        public static bool EnsureHostsEntry()
        {
            try
            {
                string sysHosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"drivers\etc\hosts");
                if (File.Exists(sysHosts))
                {
                    string txt = File.ReadAllText(sysHosts);
                    if (!txt.Contains("m.lfspro.net"))
                    {
                        File.AppendAllText(sysHosts, "\r\n127.0.0.1 m.lfspro.net\r\n127.0.0.1 master.lfs.net\r\n127.0.0.1 s.lfspro.net\r\n");
                        return true;
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }
    }
}
