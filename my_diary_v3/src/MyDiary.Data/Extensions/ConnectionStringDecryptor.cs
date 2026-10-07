using System.Security.Cryptography;
using System.Text;

namespace MyDiary.Data.Extensions;

/// <summary>
/// AES-256-GCM connection string decryption (migrated from Project B).
/// SECURITY NOTE: The AES key must NOT be hardcoded in production.
///   → Store in Azure Key Vault / AWS Secrets Manager / environment variable.
///   → This implementation reads from the "ConnectionStringEncryptionKey"
///     environment variable, with a development fallback.
/// Output format: Base64(12-byte-nonce + 16-byte-tag + ciphertext)
/// </summary>
public static class ConnectionStringDecryptor
{
    private const int NonceSizeBytes = 12;
    private const int TagSizeBytes   = 16;

    private static byte[] GetKey()
    {
        // PROD: read from environment / secrets manager
        var key = Environment.GetEnvironmentVariable("MD_CONN_ENCRYPTION_KEY")
                  ?? "01234567890123456789012345678901"; // dev fallback only
        return Encoding.UTF8.GetBytes(key.PadRight(32)[..32]);
    }

    /// <summary>Same AES-256-GCM scheme as <see cref="Decrypt"/>, same output format
    /// (Base64(nonce + tag + ciphertext)) — used to encrypt sensitive text (Report Developer's
    /// admin-authored SQL_QUERY, and the SFTP/DB connection secrets stored alongside it in
    /// RP_M_REPORT_DEF) before it's written to the DB, so a stored/transmitted copy of the row
    /// can't be read or tampered with without the same key used to decrypt it back.</summary>
    public static string Encrypt(string plainText)
    {
        if (plainText is null) throw new ArgumentNullException(nameof(plainText));

        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSizeBytes);
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] cipherBytes = new byte[plainBytes.Length];
        byte[] tag = new byte[TagSizeBytes];

        using var aesGcm = new AesGcm(GetKey(), TagSizeBytes);
        aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);

        var raw = new byte[NonceSizeBytes + TagSizeBytes + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, raw, 0, NonceSizeBytes);
        Buffer.BlockCopy(tag, 0, raw, NonceSizeBytes, TagSizeBytes);
        Buffer.BlockCopy(cipherBytes, 0, raw, NonceSizeBytes + TagSizeBytes, cipherBytes.Length);
        return Convert.ToBase64String(raw);
    }

    /// <summary>Best-effort decrypt for values that might be plaintext (e.g. rows written before
    /// this encryption was added, or a hand-edited DB row) — returns the input unchanged if it
    /// isn't validly-formatted ciphertext, rather than throwing and breaking the whole page.</summary>
    public static string? TryDecryptOrPassThrough(string? cipherOrPlain)
    {
        if (string.IsNullOrEmpty(cipherOrPlain)) return cipherOrPlain;
        try { return Decrypt(cipherOrPlain); }
        catch { return cipherOrPlain; }
    }

    public static string Decrypt(string cipherBase64)
    {
        if (string.IsNullOrWhiteSpace(cipherBase64))
            throw new ArgumentNullException(nameof(cipherBase64));

        byte[] raw         = Convert.FromBase64String(Normalize(cipherBase64));
        byte[] nonce       = raw[..NonceSizeBytes];
        byte[] tag         = raw[NonceSizeBytes..(NonceSizeBytes + TagSizeBytes)];
        byte[] cipherBytes = raw[(NonceSizeBytes + TagSizeBytes)..];
        byte[] plainBytes  = new byte[cipherBytes.Length];

        using var aesGcm = new AesGcm(GetKey(), TagSizeBytes);
        aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);

        return Encoding.UTF8.GetString(plainBytes);
    }

    /// <summary>
    /// Decrypts all ConnectionStrings from configuration.
    /// Returns plain-text values keyed by name.
    /// In development with plain-text values, returns as-is.
    /// </summary>
    public static Dictionary<string, string> DecryptAll(IConfiguration configuration)
    {
        var result  = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var section = configuration.GetSection("ConnectionStrings");

        foreach (var child in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Value)) continue;

            try
            {
                result[child.Key] = Decrypt(child.Value);
            }
            catch
            {
                // If decryption fails the value is likely plain-text (dev mode) — use as-is
                result[child.Key] = child.Value;
            }
        }

        return result;
    }

    private static string Normalize(string s)
    {
        s = s.Replace(" ", "+").TrimEnd('=');
        return (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
    }
}
