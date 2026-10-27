using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ProLightClean
{
    public static class MasterServerBridge
    {
        private static TcpListener _listener;
        private static bool _isRunning = false;
        private static string _activeUser = "Driver";

        public static bool IsRunning => _isRunning;
        public static string ActiveUser
        {
            get => _activeUser;
            set => _activeUser = (value ?? "Driver").Trim().ToLower();
        }

        public static event Action<string> OnLog;
        private static void Log(string msg) => OnLog?.Invoke(msg);

        public static void Start(int port = 29339, string initialUser = null)
        {
            if (_isRunning) return;
            if (!string.IsNullOrEmpty(initialUser)) _activeUser = initialUser.Trim().ToLower();

            try
            {
                _listener = new TcpListener(IPAddress.Loopback, port);
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _listener.Start();
                _isRunning = true;

                Log($"[MasterBridge] Online at 127.0.0.1:{port} (S3 License Auto-Unlock Ready)");
                new Thread(ListenLoop) { IsBackground = true }.Start();
            }
            catch (Exception ex)
            {
                _isRunning = false;
                Log($"[MasterBridge] Error starting listener: {ex.Message}");
            }
        }

        public static void Stop()
        {
            _isRunning = false;
            try { _listener?.Stop(); } catch { }
            Log("[MasterBridge] Stopped.");
        }

        private static void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(s => HandleClient(client));
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    byte[] sizeBuf = new byte[1];
                    while (_isRunning && stream.Read(sizeBuf, 0, 1) > 0)
                    {
                        int size = sizeBuf[0];
                        if (size == 0) continue;

                        byte[] payload = new byte[size];
                        int read = 0;
                        while (read < size)
                        {
                            int r = stream.Read(payload, read, size - read);
                            if (r <= 0) break;
                            read += r;
                        }
                        if (read < size) break;

                        // Scrutineer / Unlock packet (Type 4, 76 bytes)
                        bool isScrutineer = (size >= 72 && (payload[0] == 0x4C || (payload.Length > 4 && payload[4] == 4)));
                        // JRA (Join Request Authorisation, 36 bytes)
                        bool isJra = (size >= 32 && payload[0] == 0x4A && payload[1] == 0x52 && payload[2] == 0x41);

                        if (isScrutineer || isJra)
                        {
                            byte connId = 1;
                            string username = _activeUser;

                            if (isScrutineer)
                            {
                                if (payload.Length > 1 && payload[1] != 0) connId = payload[1];
                                if (payload.Length >= 0x40)
                                {
                                    string extracted = Encoding.ASCII.GetString(payload, 0x28, Math.Min(24, payload.Length - 0x28)).Trim('\0', ' ').ToLower();
                                    if (!string.IsNullOrEmpty(extracted)) username = extracted;
                                }
                                Log($"[MasterBridge] Received Scrutineer for user: {username}");
                            }
                            else if (isJra)
                            {
                                if (payload.Length > 0x1C) connId = payload[0x1C];
                                Log($"[MasterBridge] Received JRA for user: {username} (ConnID: {connId})");
                            }

                            // Build and send S3 response
                            SendS3Auth(stream, connId, username);
                            Log($"[MasterBridge] Successfully granted S3 License to: {username}");
                        }
                    }
                }
                catch { }
            }
        }

        private static void SendS3Auth(NetworkStream stream, byte connId, string username)
        {
            byte[] resp = new byte[100];
            resp[0] = connId;
            resp[1] = 0; // 0 = Authorised
            resp[2] = 0;
            resp[3] = 0;
            resp[4] = 3; // 3 = S3 License

            // User name at offset 0x28
            byte[] userBytes = Encoding.ASCII.GetBytes(username ?? _activeUser);
            Array.Copy(userBytes, 0, resp, 0x28, Math.Min(userBytes.Length, 23));

            // License "S3" at offset 0x60
            byte[] lic = Encoding.ASCII.GetBytes("S3");
            Array.Copy(lic, 0, resp, 0x60, lic.Length);

            // 1-byte length prefix (100)
            byte[] packet = new byte[101];
            packet[0] = 100;
            Array.Copy(resp, 0, packet, 1, 100);

            stream.Write(packet, 0, packet.Length);
            stream.Flush();
        }
    }
}
