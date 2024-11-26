namespace Clio
{
    public interface IDataEncryptor
    {
        string Encrypt(string data);

        string Decrypt(string encryptedData);
    }
}