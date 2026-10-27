using System;
using System.IO;
using System.Net.NetworkInformation;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace ProLightClean
{
    public static class HardwareInfo
    {
        public static string GetMac()
        {
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    {
                        string mac = nic.GetPhysicalAddress().ToString();
                        if (!string.IsNullOrEmpty(mac) && mac.Length >= 8)
                            return mac;
                    }
                }
            }
            catch { }
            return "0011223344AA";
        }

        public static string GetMotherboardSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_BaseBoard"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        object val = mo["SerialNumber"];
                        if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                            return Clean(val.ToString());
                    }
                }
            }
            catch { }

            try
            {
                if (File.Exists("/sys/devices/virtual/dmi/id/board_serial"))
                {
                    string s = File.ReadAllText("/sys/devices/virtual/dmi/id/board_serial").Trim();
                    if (!string.IsNullOrEmpty(s)) return Clean(s);
                }
            }
            catch { }

            return "MB-CLEAN-01";
        }

        public static string GetBiosUuid()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT UUID FROM Win32_ComputerSystemProduct"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        object val = mo["UUID"];
                        if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                            return Clean(val.ToString());
                    }
                }
            }
            catch { }

            try
            {
                if (File.Exists("/sys/devices/virtual/dmi/id/product_uuid"))
                {
                    string s = File.ReadAllText("/sys/devices/virtual/dmi/id/product_uuid").Trim();
                    if (!string.IsNullOrEmpty(s)) return Clean(s);
                }
            }
            catch { }

            return Guid.NewGuid().ToString("N");
        }

        public static string GetHddSerial()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        object val = mo["SerialNumber"];
                        if (val != null && !string.IsNullOrWhiteSpace(val.ToString()))
                            return Clean(val.ToString());
                    }
                }
            }
            catch { }

            return "HDD-CLEAN-01";
        }

        public static string Sha256(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder sb = new StringBuilder();
                foreach (byte x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Clean(string str)
        {
            if (string.IsNullOrEmpty(str)) return "UNKNOWN";
            return str.Replace("|", "_").Replace("\r", "").Replace("\n", "").Trim();
        }

        public static string BuildPayload(string username, string password)
        {
            string passHash = Sha256(password);
            string hdd = GetHddSerial();
            string mb = GetMotherboardSerial();
            string mac = GetMac();
            string bios = GetBiosUuid();
            string tag = "PRO_CLEAN";
            string osUser = Clean(Environment.UserName);
            string osMachine = Clean(Environment.MachineName);
            string ver = "5.0.17";

            return string.Format("{0}|{1}|{2}|{3}|{4}|{5}|{6}|{7}|{8}|{9}",
                username, passHash, hdd, mb, mac, bios, tag, osUser, osMachine, ver);
        }
    }
}
