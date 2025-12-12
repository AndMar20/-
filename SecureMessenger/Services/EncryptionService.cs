using System.Security.Cryptography;
using System.Text;

namespace SecureMessenger.Services;

public static class EncryptionService
{
    private const int SaltSize = 16;
    private const int IterationCount = 10_000;

    public static byte[] Encrypt(string passphrase, string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = DeriveKey(passphrase, aes.KeySize / 8, out var salt);
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        var result = new byte[salt.Length + aes.IV.Length + cipherBytes.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(aes.IV, 0, result, salt.Length, aes.IV.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, salt.Length + aes.IV.Length, cipherBytes.Length);
        return result;
    }

    public static string Decrypt(string passphrase, byte[] payload)
    {
        using var aes = Aes.Create();
        var salt = payload[..SaltSize];
        var iv = payload[SaltSize..(SaltSize + aes.BlockSize / 8)];
        var cipherBytes = payload[(SaltSize + aes.BlockSize / 8)..];

        aes.Key = DeriveKey(passphrase, aes.KeySize / 8, salt, out _);

        using var decryptor = aes.CreateDecryptor(aes.Key, iv);
        var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        return Encoding.UTF8.GetString(plainBytes);
    }

    private static byte[] DeriveKey(string passphrase, int keySize, out byte[] salt)
    {
        salt = RandomNumberGenerator.GetBytes(SaltSize);
        return DeriveKey(passphrase, keySize, salt, out _);
    }

    private static byte[] DeriveKey(string passphrase, int keySize, byte[] salt, out byte[] usedSalt)
    {
        using var derivation = new Rfc2898DeriveBytes(passphrase, salt, IterationCount, HashAlgorithmName.SHA256);
        usedSalt = salt;
        return derivation.GetBytes(keySize);
    }
}
