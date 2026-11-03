using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace RWORM.GUI.Services
{
    public static class GuiCrypto
    {
        public static string KeyString = "AES-256-DEFAULT";

        public static byte[] DeriveKey(string password)
        {
            var raw = Encoding.UTF8.GetBytes(password);
            using var sha = SHA256.Create();
            return sha.ComputeHash(raw);
        }

        private static byte[] CurrentKey => DeriveKey(KeyString);

        public static byte[] Encrypt(byte[] data)
        {
            using var aes = Aes.Create();
            aes.Key = CurrentKey;
            aes.GenerateIV();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);

            var result = new byte[16 + encrypted.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, 16);
            Buffer.BlockCopy(encrypted, 0, result, 16, encrypted.Length);
            return result;
        }

        public static byte[]? Decrypt(byte[] data)
        {
            if (data == null || data.Length < 16) return null;

            var iv = new byte[16];
            Buffer.BlockCopy(data, 0, iv, 0, 16);

            int cipherLen = data.Length - 16;
            var cipherText = new byte[cipherLen];
            Buffer.BlockCopy(data, 16, cipherText, 0, cipherLen);

            using var aes = Aes.Create();
            aes.Key = CurrentKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            try
            {
                using var decryptor = aes.CreateDecryptor();
                return decryptor.TransformFinalBlock(cipherText, 0, cipherLen);
            }
            catch
            {
                return null;
            }
        }

        public static byte[] ComputeHMAC(byte[] data)
        {
            using var hmac = new HMACSHA256(CurrentKey);
            var hash = hmac.ComputeHash(data);
            var result = new byte[16];
            Buffer.BlockCopy(hash, 0, result, 0, 16);
            return result;
        }
    }
}
