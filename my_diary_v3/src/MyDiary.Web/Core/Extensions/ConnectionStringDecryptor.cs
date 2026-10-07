using System.Security.Cryptography;
using System.Text;

namespace MyDiary.Web.Core.Extensions
{
    /// <summary>
    /// Updated version of ConnectionStringDecryptor using AES-256-GCM instead of AES-CBC.
    /// Switch to this class (and update Program.cs startup decryption call) once all
    /// connection strings in appsettings.json have been re-encrypted using ConnStringCrypto.
    /// Output format: Base64(12-byte-nonce + 16-byte-tag + ciphertext)
    /// </summary>
    public static class ConnectionStringDecryptor
    {
        // Must match the key in Connector/ConnStringCrypto.cs
        private const string AesKey = "01234567890123456789012345678901";

        private const int NonceSizeBytes = 12;
        private const int TagSizeBytes   = 16;

        /// <summary>
        /// Decrypts a Base64-encoded AES-256-GCM cipher text back to the plain connection string.
        /// </summary>
        public static string Decrypt(string cipherBase64)
        {
            if (string.IsNullOrWhiteSpace(cipherBase64))
                throw new ArgumentNullException(nameof(cipherBase64), "Encrypted connection string is empty.");

            byte[] raw = Convert.FromBase64String(NormalizeBase64(cipherBase64));

            if (raw.Length < NonceSizeBytes + TagSizeBytes)
                throw new ArgumentException("Encrypted value is too short to contain nonce and tag.");

            byte[] nonce       = raw[..NonceSizeBytes];
            byte[] tag         = raw[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
            byte[] cipherBytes = raw[(NonceSizeBytes + TagSizeBytes)..];
            byte[] plainBytes  = new byte[cipherBytes.Length];

            byte[] key = Encoding.UTF8.GetBytes(AesKey);
            using var aesGcm = new AesGcm(key, TagSizeBytes);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

            return Encoding.UTF8.GetString(plainBytes);
        }

        /// <summary>
        /// Reads all ConnectionStrings from configuration, decrypts each one,
        /// and returns a dictionary of name → plain connection string.
        /// </summary>
        public static Dictionary<string, string> DecryptAll(IConfiguration configuration)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var section = configuration.GetSection("ConnectionStrings");

            foreach (var child in section.GetChildren())
            {
                if (!string.IsNullOrWhiteSpace(child.Value))
                {
                    try
                    {
                        result[child.Key] = Decrypt(child.Value);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Failed to decrypt connection string '{child.Key}'. " +
                            $"Ensure the value was encrypted with the ConnStringCrypto. " +
                            $"Inner: {ex.Message}", ex);
                    }
                }
            }

            return result;
        }

        public static string Encrypt(string plainText)
        {
            byte[] key = Encoding.UTF8.GetBytes(AesKey);
            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] nonce = new byte[NonceSizeBytes];
            byte[] cipherBytes = new byte[plainBytes.Length];
            byte[] tag = new byte[TagSizeBytes];

            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(nonce);

            using (var aesGcm = new AesGcm(key, TagSizeBytes))
                aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

            // Output envelope: nonce + tag + ciphertext → Base64
            byte[] result = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, NonceSizeBytes);
            Buffer.BlockCopy(tag, 0, result, NonceSizeBytes, TagSizeBytes);
            Buffer.BlockCopy(cipherBytes, 0, result, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);

            return Convert.ToBase64String(result);
        }

        /// <summary>
        /// Normalises a Base64 string that may have been stored without correct padding.
        /// </summary>
        private static string NormalizeBase64(string s)
        {
            s = s.Replace(" ", "+").TrimEnd('=');
            int mod = s.Length % 4;
            if (mod == 2) s += "==";
            else if (mod == 3) s += "=";
            return s;
        }
    }
}
