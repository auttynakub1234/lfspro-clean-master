using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProLightClean
{
    public static class SecureCrypto
    {
        public const string Password = "config.sys.input=UTF8";
        public const int Iterations = 100000;
        public const int KeyBytes = 32;
        public const int SaltBytes = 16;
        public const int IvBytes = 16;

        public static byte[] DeriveKeySha256(byte[] passwordBytes, byte[] salt, int iterations, int outputLength)
        {
            using (var hmac = new HMACSHA256(passwordBytes))
            {
                int hashLen = 32; // SHA256 output length
                int numBlocks = (outputLength + hashLen - 1) / hashLen;
                byte[] result = new byte[outputLength];
                int offset = 0;

                for (int i = 1; i <= numBlocks; i++)
                {
                    byte[] block = new byte[salt.Length + 4];
                    Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
                    block[salt.Length] = (byte)(i >> 24);
                    block[salt.Length + 1] = (byte)(i >> 16);
                    block[salt.Length + 2] = (byte)(i >> 8);
                    block[salt.Length + 3] = (byte)i;

                    byte[] u = hmac.ComputeHash(block);
                    byte[] f = (byte[])u.Clone();

                    for (int j = 1; j < iterations; j++)
                    {
                        u = hmac.ComputeHash(u);
                        for (int k = 0; k < hashLen; k++)
                            f[k] ^= u[k];
                    }

                    int count = Math.Min(hashLen, outputLength - offset);
                    Buffer.BlockCopy(f, 0, result, offset, count);
                    offset += count;
                }
                return result;
            }
        }

        public static string Encrypt(string plainText, string customPassword = null)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            string pwd = customPassword ?? Password;

            byte[] salt = new byte[SaltBytes];
            byte[] iv = new byte[IvBytes];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
                rng.GetBytes(iv);
            }

            byte[] key = DeriveKeySha256(Encoding.UTF8.GetBytes(pwd), salt, Iterations, KeyBytes);

            byte[] cipherText;
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    cipherText = ms.ToArray();
                }
            }

            byte[] result = new byte[SaltBytes + IvBytes + cipherText.Length];
            Buffer.BlockCopy(salt, 0, result, 0, SaltBytes);
            Buffer.BlockCopy(iv, 0, result, SaltBytes, IvBytes);
            Buffer.BlockCopy(cipherText, 0, result, SaltBytes + IvBytes, cipherText.Length);

            return Convert.ToBase64String(result);
        }

        public static string Decrypt(string base64Payload, string customPassword = null)
        {
            if (string.IsNullOrEmpty(base64Payload)) return string.Empty;
            string pwd = customPassword ?? Password;

            byte[] raw = Convert.FromBase64String(base64Payload);
            if (raw.Length < SaltBytes + IvBytes)
                throw new ArgumentException("Payload is too short.");

            byte[] salt = new byte[SaltBytes];
            byte[] iv = new byte[IvBytes];
            int cipherLen = raw.Length - SaltBytes - IvBytes;
            byte[] cipherText = new byte[cipherLen];

            Buffer.BlockCopy(raw, 0, salt, 0, SaltBytes);
            Buffer.BlockCopy(raw, SaltBytes, iv, 0, IvBytes);
            Buffer.BlockCopy(raw, SaltBytes + IvBytes, cipherText, 0, cipherLen);

            byte[] key = DeriveKeySha256(Encoding.UTF8.GetBytes(pwd), salt, Iterations, KeyBytes);

            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.IV = iv;

                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherText, 0, cipherText.Length);
                        cs.FlushFinalBlock();
                    }
                    byte[] decrypted = ms.ToArray();
                    return Encoding.UTF8.GetString(decrypted).TrimStart('\uFEFF');
                }
            }
        }
    }
}
