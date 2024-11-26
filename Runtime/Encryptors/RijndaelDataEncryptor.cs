using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Clio.Encryptors
{
    public class RijndaelDataEncryptor : IDataEncryptor
    {
        private const int BufferKeySize = 32;
        private const int BlockSize = 256;
        private const int KeySize = 256;

        private readonly string password;

        public RijndaelDataEncryptor(string password)
        {
            this.password = password;
        }

        public string Encrypt(string data)
        {
            using var rijndaelManaged = GetManager();
            var deriveBytes = new Rfc2898DeriveBytes(this.password, BufferKeySize);
            rijndaelManaged.GenerateIV();
            rijndaelManaged.Key = deriveBytes.GetBytes(BufferKeySize);

            using var encryptor = rijndaelManaged.CreateEncryptor();
            var textBytes = Encoding.UTF8.GetBytes(data);
            var encrypted = encryptor.TransformFinalBlock(textBytes, 0, textBytes.Length);

            return Convert.ToBase64String(rijndaelManaged.IV.Concat(deriveBytes.Salt).Concat(encrypted).ToArray());
        }

        public string Decrypt(string encryptedData)
        {
            var encrypted = Convert.FromBase64String(encryptedData).ToList();
            var iv = encrypted.GetRange(0, BufferKeySize);
            var salt = encrypted.GetRange(BufferKeySize, BufferKeySize);
            var data = encrypted.GetRange(2 * BufferKeySize, encrypted.Count - 2 * BufferKeySize);

            using var rijndaelManaged = GetManager();
            var deriveBytes = new Rfc2898DeriveBytes(this.password, salt.ToArray());
            rijndaelManaged.IV = iv.ToArray();
            rijndaelManaged.Key = deriveBytes.GetBytes(BufferKeySize);

            using var decryptor = rijndaelManaged.CreateDecryptor();
            var decrypted = decryptor.TransformFinalBlock(data.ToArray(), 0, data.Count);

            return Encoding.UTF8.GetString(decrypted);
        }

        private static RijndaelManaged GetManager()
        {
            return new RijndaelManaged
            {
                BlockSize = BlockSize,
                KeySize = KeySize,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
        }
    }
}