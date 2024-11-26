namespace Clio.Encryptors
{
    public class EmptyDataEncryptor : IDataEncryptor
    {
        public string Encrypt(string data)
        {
            return data;
        }

        public string Decrypt(string encryptedData)
        {
            return encryptedData;
        }
    }
}