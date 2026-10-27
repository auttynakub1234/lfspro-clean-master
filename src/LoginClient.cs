using System;
using System.IO;
using System.Net;
using System.Text;

namespace ProLightClean
{
    public class LoginResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
        public string RawResponse { get; set; }
    }

    public static class LoginClient
    {
        public const string DefaultEndpoint = "http://localhost:8080/api.php";

        public static LoginResult Authenticate(string username, string password, string endpointUrl = DefaultEndpoint)
        {
            var res = new LoginResult { Success = false };

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                res.Message = "Username and password cannot be empty.";
                return res;
            }

            try
            {
                // Ensure TLS 1.2 is supported and bypass Mono Linux CA store issues
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072 | SecurityProtocolType.Tls;
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, sslErrors) => true;
                // 1. Build hardware payload
                string payload = HardwareInfo.BuildPayload(username, password);

                // 2. Encrypt with AES-256-CBC
                string encrypted = SecureCrypto.Encrypt(payload);

                // 3. Construct URL
                string url = endpointUrl;
                string sep = url.Contains("?") ? "&" : "?";
                string fullUrl = $"{url}{sep}sv=lfspro&info=login&data={Uri.EscapeDataString(encrypted)}";

                // 4. Send GET request
                var req = (HttpWebRequest)WebRequest.Create(fullUrl);
                req.Method = "GET";
                req.Timeout = 8000;
                req.UserAgent = "ProLight-Clean/5.0";

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var r = new StreamReader(s, Encoding.UTF8))
                {
                    string body = r.ReadToEnd().Trim();
                    res.RawResponse = body;

                    if (body.Contains("LOGINOK"))
                    {
                        res.Success = true;
                        res.Status = "LOGINOK";
                        res.Message = "Authentication Successful! Welcome to LFS.";
                    }
                    else if (body.Contains("WRONGPASS"))
                    {
                        res.Status = "WRONGPASS";
                        res.Message = "Wrong password! Please try again.";
                    }
                    else if (body.Contains("USERNOTEXIST"))
                    {
                        res.Status = "USERNOTEXIST";
                        res.Message = "User does not exist.";
                    }
                    else if (body.Contains("USERISMULTIACC"))
                    {
                        res.Status = "USERISMULTIACC";
                        res.Message = "Multiple accounts detected on this hardware.";
                    }
                    else
                    {
                        res.Status = "RESPONSE";
                        res.Message = body;
                    }
                }
            }
            catch (Exception ex)
            {
                res.Message = $"Connection failed: {ex.Message}";
            }

            return res;
        }
    }
}
