using System;
using System.Security.Cryptography;
using System.Text;

namespace Clio.Encryptors
{
    public class AesDataEncryptor : IDataEncryptor
    {
        private readonly byte[] key;

        private readonly byte[] iv;

        public AesDataEncryptor(string key, string iv) : this(ConvertToKeyBytes(key, 32), ConvertToKeyBytes(iv, 16))
        {
        }

        public AesDataEncryptor(byte[] key, byte[] iv)
        {
            this.key = key;
            this.iv = iv;
        }

        public string Encrypt(string data)
        {
            using var aes = new AesManaged();
            var encryptor = aes.CreateEncryptor(this.key, this.iv);
            var textBytes = Encoding.UTF8.GetBytes(data);
            var encrypted = encryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);
            return Convert.ToBase64String(encrypted);
        }

        public string Decrypt(string encryptedData)
        {
            using var aes = new AesManaged();
            var decryptor = aes.CreateDecryptor(this.key, this.iv);
            var textBytes = Convert.FromBase64String(encryptedData);
            var decrypted = decryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);
            return Encoding.UTF8.GetString(decrypted);
        }

        private static byte[] ConvertToKeyBytes(string password, int length)
        {
            var keyBytes = Encoding.UTF8.GetBytes(password);

            if (keyBytes.Length != length)
            {
                var newKeyBytes = new byte[length];
                Array.Copy(keyBytes, newKeyBytes, Math.Min(keyBytes.Length, newKeyBytes.Length));
                keyBytes = newKeyBytes;
            }

            return keyBytes;
        }
    }
}