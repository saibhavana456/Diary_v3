namespace MyDiary.Web.Services
{
    public interface IEncryptionService
    {
        string EncryptStringAES(string plainText);
        string DecryptStringAES(string cipherText);
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
        string DecryptAesCbc(string cipherBase64, string key);

        /// <summary>
        /// AES-256-CBC encryption delegated to the centralized CryptoService.
        /// The key is padded/trimmed to 32 bytes; the IV is derived from the first
        /// 16 chars of <paramref name="ivSource"/>. Returns the Base64-encoded ciphertext.
        /// </summary>
        string EncryptAesCbc(string plainText, string key, string ivSource);
    }
}