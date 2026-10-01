namespace Videolader.Core;

/// <summary>Encrypts secrets (the API key) for storage. The implementation lives in the Windows project.</summary>
public interface ISecretProtector
{
    /// <summary>Returns the encrypted value as text (Base64).</summary>
    string Protect(string plainText);

    /// <summary>Returns the original text, or null if the value cannot be decrypted (other user or computer, corrupt).</summary>
    string? Unprotect(string protectedText);
}
