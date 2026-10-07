using System.Security.Cryptography;

namespace MyDiary.Web.Core.Extensions
{
    /// <summary>
    /// Cryptographically secure random number helper.
    /// Use this instead of System.Random for security-sensitive contexts (captcha, tokens, etc.).
    /// </summary>
    public static class SecureRandom
    {
        /// <summary>Returns a random integer between min (inclusive) and max (exclusive).</summary>
        public static int Next(int min, int max) => RandomNumberGenerator.GetInt32(min, max);

        /// <summary>Returns a random integer between 0 (inclusive) and max (exclusive).</summary>
        public static int Next(int max) => RandomNumberGenerator.GetInt32(0, max);
    }
}
