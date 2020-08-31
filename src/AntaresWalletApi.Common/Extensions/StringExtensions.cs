using System.Security.Cryptography;
using Common;

namespace AntaresWalletApi.Common.Extensions
{
    public static class StringExtensions
    {
        public static string ToSha256(this string str)
        {
            if (string.IsNullOrEmpty(str))
                return null;

            var hash = SHA256.Create().ComputeHash(str.ToUtf8Bytes());
            return hash.ToHexString().ToLower();
        }
    }
}
