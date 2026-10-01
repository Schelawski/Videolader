using System.Security.Cryptography;
using System.Text;
using Videolader.Core;

namespace Videolader;

/// <summary>Windows DPAPI, scope current user: only the same Windows account can decrypt the value.</summary>
internal sealed class DpapiSecretProtector : ISecretProtector
{
    private static readonly byte[] Entropy = "Videolader.ApiKey"u8.ToArray();

    public string Protect(string plainText) =>
        Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plainText), Entropy, DataProtectionScope.CurrentUser));

    public string? Unprotect(string protectedText)
    {
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
