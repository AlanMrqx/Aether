using System.Security.Cryptography;

namespace Aether.Core;

public class SecureSession : IDisposable
{
    private readonly ECDiffieHellman _ecdh;

    // The derived secret key used for chaos encryption
    public byte[]? SharedSecret { get; private set; }

    public bool IsHandshakeComplete => SharedSecret != null;

    public SecureSession()
    {
        // Initialize ephemeral keys using NIST P-256 curve
        _ecdh = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    }

    public byte[] GetPublicKey()
    {
        return _ecdh.PublicKey.ExportSubjectPublicKeyInfo();
    }

    public void DeriveSharedSecret(byte[] otherPartyPublicKey)
    {
        using var otherPartyEcdh = ECDiffieHellman.Create();
        otherPartyEcdh.ImportSubjectPublicKeyInfo(otherPartyPublicKey, out _);

        // Derive common secret and hash it to 32 bytes
        SharedSecret = _ecdh.DeriveKeyFromHash(otherPartyEcdh.PublicKey, HashAlgorithmName.SHA256);
    }

    public void Dispose()
    {
        _ecdh?.Dispose();
        GC.SuppressFinalize(this);
    }
}