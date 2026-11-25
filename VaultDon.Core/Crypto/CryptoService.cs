using System.Security.Cryptography;
using System.Text;

namespace VaultDon.Core.Crypto;

public static class CryptoService
{
    private const int KeySize = 32;
    private const int IvSize = 16;
    private const int SaltSize = 16;
    private const int Iterations = 200_000;

    public static byte[] DeriveKey(string masterPassword, byte[] salt)
    {
        using var kdf = new Rfc2898DeriveBytes(masterPassword, salt, Iterations, HashAlgorithmName.SHA256);
        return kdf.GetBytes(KeySize);
    }

    public static byte[] GenerateSalt() => RandomNumberGenerator.GetBytes(SaltSize);

    public static (byte[] cipherText, byte[] iv) Encrypt(byte[] plainText, byte[] key)
    {
        var iv = RandomNumberGenerator.GetBytes(IvSize);
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(plainText, 0, plainText.Length);
        return (cipher, iv);
    }

    public static byte[] Decrypt(byte[] cipherText, byte[] key, byte[] iv)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        using var decryptor = aes.CreateDecryptor();
        return decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
    }

    public static string EncryptString(string plainText, byte[] key)
    {
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var (cipher, iv) = Encrypt(bytes, key);
        // Prepend IV to cipher
        var combined = new byte[iv.Length + cipher.Length];
        iv.CopyTo(combined, 0);
        cipher.CopyTo(combined, iv.Length);
        return Convert.ToBase64String(combined);
    }

    public static string DecryptString(string base64, byte[] key)
    {
        var combined = Convert.FromBase64String(base64);
        var iv = combined[..IvSize];
        var cipher = combined[IvSize..];
        var plain = Decrypt(cipher, key, iv);
        return Encoding.UTF8.GetString(plain);
    }

    public static string HashPassword(string password, byte[] salt)
    {
        var key = DeriveKey(password, salt);
        return Convert.ToBase64String(key);
    }

    public static bool VerifyPassword(string password, byte[] salt, string storedHash)
    {
        var hash = HashPassword(password, salt);
        return hash == storedHash;
    }
}
