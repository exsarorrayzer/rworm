using System;
using System.Security.Cryptography;
using System.Text;

namespace RWORM.Client.Core
{
    public class Crypto
    {
        private static readonly byte[] Key = Encoding.UTF8.GetBytes("RWORM_KEY_32BYTES!!!!".PadRight(32, '!'));
        
        public static byte[] Encrypt(byte[] data)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.GenerateIV();
                
                using (var encryptor = aes.CreateEncryptor())
                {
                    var encrypted = encryptor.TransformFinalBlock(data, 0, data.Length);
                    var result = new byte[16 + encrypted.Length];
                    aes.IV.CopyTo(result, 0);
                    encrypted.CopyTo(result, 16);
                    return result;
                }
            }
        }
        
        public static byte[] Decrypt(byte[] data)
        {
            if (data.Length < 16) return null;
            
            var iv = new byte[16];
            var encrypted = new byte[data.Length - 16];
            
            Array.Copy(data, 0, iv, 0, 16);
            Array.Copy(data, 16, encrypted, 0, encrypted.Length);
            
            using (var aes = Aes.Create())
            {
                aes.Key = Key;
                aes.IV = iv;
                
                using (var decryptor = aes.CreateDecryptor())
                {
                    return decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                }
            }
        }
        
        public static string ComputeHMAC(byte[] data)
        {
            using (var hmac = new HMACSHA256(Key))
            {
                var hash = hmac.ComputeHash(data);
                return Convert.ToBase64String(hash);
            }
        }
    }
}