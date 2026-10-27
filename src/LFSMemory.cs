using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace ProLightClean
{
    public class LFSMemory : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        private const uint PROCESS_VM_READ = 0x0010;
        private const uint PROCESS_VM_WRITE = 0x0020;
        private const uint PROCESS_VM_OPERATION = 0x0008;

        public Process TargetProcess { get; private set; }
        public IntPtr ProcessHandle { get; private set; }
        public IntPtr BaseAddress { get; private set; }

        // Core offsets verified directly from ProLight.dll and LFS.exe
        public const int RVA_MASTER_DOMAIN     = 0x1C890C; // "m.lfspro.net" (16 bytes)
        public const int RVA_SKINS_DOMAIN      = 0x1C6CD4; // "s.lfspro.net" (16 bytes)
        public const int RVA_BOTTOM_WATERMARK  = 0x1C830C; // "  ^0LFS^6Pro ^7" (16 bytes)
        public const int RVA_MASTER_USERNAME   = 0x5B0AA4; // Master Username Buffer (24 bytes)
        public const int RVA_MASTER_PASSWORD   = 0x5B0A8C; // Master Password Buffer (24 bytes)
        public const int RVA_ACTIVE_PROFILE    = 0x3B9CF4; // Active Profile Buffer (24 bytes)
        public const int RVA_INGAME_NAME       = 0x229214; // Active In-game Name Buffer (32 bytes)

        public LFSMemory(Process process)
        {
            TargetProcess = process ?? throw new ArgumentNullException(nameof(process));
            ProcessHandle = OpenProcess(PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_VM_OPERATION, false, process.Id);
            try
            {
                BaseAddress = process.MainModule != null ? process.MainModule.BaseAddress : (IntPtr)0x400000;
            }
            catch
            {
                BaseAddress = (IntPtr)0x400000;
            }
        }

        public static Process FindLFS()
        {
            Process[] procs = Process.GetProcessesByName("LFS");
            if (procs == null || procs.Length == 0) return null;

            foreach (var p in procs)
            {
                try
                {
                    using (var mem = new LFSMemory(p))
                    {
                        if (mem.VerifyVersion())
                            return p;
                    }
                }
                catch { }
            }
            return procs.Length > 0 ? procs[0] : null;
        }

        public bool VerifyVersion()
        {
            try
            {
                IntPtr addr = IntPtr.Add(BaseAddress, RVA_BOTTOM_WATERMARK);
                byte[] b = ReadBytes(addr, 20);
                if (b != null && b.Length >= 19)
                {
                    // Check index 17 == '6' and index 18 == 'V'
                    return b[17] == 0x36 && b[18] == 0x56;
                }
            }
            catch { }
            return false;
        }

        public byte[] ReadBytes(IntPtr address, int length)
        {
            byte[] buf = new byte[length];
            if (ReadProcessMemory(ProcessHandle, address, buf, length, out _))
                return buf;
            return null;
        }

        public bool WriteBytes(IntPtr address, byte[] data)
        {
            if (data == null || data.Length == 0) return false;
            return WriteProcessMemory(ProcessHandle, address, data, data.Length, out _);
        }

        public float ReadFloat(IntPtr address)
        {
            byte[] b = ReadBytes(address, 4);
            return b != null ? BitConverter.ToSingle(b, 0) : 0f;
        }

        public bool WriteFloat(IntPtr address, float val)
        {
            return WriteBytes(address, BitConverter.GetBytes(val));
        }

        public int ReadInt32(IntPtr address)
        {
            byte[] b = ReadBytes(address, 4);
            return b != null ? BitConverter.ToInt32(b, 0) : 0;
        }

        public bool WriteInt32(IntPtr address, int val)
        {
            return WriteBytes(address, BitConverter.GetBytes(val));
        }

        public IntPtr ResolvePointer(string chain)
        {
            if (string.IsNullOrWhiteSpace(chain)) return IntPtr.Zero;
            string clean = chain.Trim().Replace("\"", "");
            string[] parts = clean.Split('+');
            if (parts.Length < 2) return IntPtr.Zero;

            long curr = BaseAddress.ToInt64() + long.Parse(parts[1], NumberStyles.HexNumber);
            for (int i = 2; i < parts.Length; i++)
            {
                int ptr = ReadInt32((IntPtr)curr);
                if (ptr == 0) return IntPtr.Zero;
                curr = (long)ptr + long.Parse(parts[i], NumberStyles.HexNumber);
            }
            return (IntPtr)curr;
        }

        public void ApplyMasterRedirection(string username)
        {
            // 1. Domain redirection
            WriteBytes(IntPtr.Add(BaseAddress, RVA_MASTER_DOMAIN), Encoding.ASCII.GetBytes("m.lfspro.net\0"));
            WriteBytes(IntPtr.Add(BaseAddress, RVA_SKINS_DOMAIN), Encoding.ASCII.GetBytes("s.lfspro.net\0"));
            WriteBytes(IntPtr.Add(BaseAddress, RVA_BOTTOM_WATERMARK), Encoding.ASCII.GetBytes("  ^0LFS^6Pro ^7\0"));

            // 2. Multi-buffer username injection
            if (!string.IsNullOrEmpty(username))
            {
                string u = username.Trim().ToLower();
                byte[] uBytes = Encoding.ASCII.GetBytes(u);

                // Clear & set master user
                WriteBytes(IntPtr.Add(BaseAddress, RVA_MASTER_USERNAME), new byte[24]);
                WriteBytes(IntPtr.Add(BaseAddress, RVA_MASTER_USERNAME), uBytes);

                // In-game and profile names
                WriteBytes(IntPtr.Add(BaseAddress, RVA_ACTIVE_PROFILE), uBytes);
                WriteBytes(IntPtr.Add(BaseAddress, RVA_INGAME_NAME), uBytes);
            }
        }

        public void Dispose()
        {
            if (ProcessHandle != IntPtr.Zero)
            {
                CloseHandle(ProcessHandle);
                ProcessHandle = IntPtr.Zero;
            }
        }
    }
}
