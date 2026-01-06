using System.Buffers.Binary;
using System.Security.Cryptography;
using ChaoticEngine.Core;

namespace Aether.Core;

public static class ChaosCrypto
{
    public static void Process(Span<byte> data, byte[] sharedSecret, ReadOnlySpan<byte> iv)
    {
        if (sharedSecret == null || sharedSecret.Length == 0)
            throw new ArgumentException("Handshake not completed! No shared secret.");

        // 1. Seed Generation: HMAC(SharedSecret + IV)
        using var hmac = new HMACSHA256(sharedSecret);
        byte[] mixedKey = hmac.ComputeHash(iv.ToArray());

        // 2. Normalize to 0.0 - 1.0 range
        long seedLong = BinaryPrimitives.ReadInt64LittleEndian(mixedKey.AsSpan(0, 8));
        double x0 = (double)Math.Abs(seedLong) / long.MaxValue;
        if (x0 < 0.0001) x0 = 0.5; // Avoid zero-lock

        // 3. Initialize Engine (SineMap is fastest with AVX-512)
        var generator = ChaosFactory.Create1D(ChaosType.SineMap);
        double[] chaosBuffer = new double[data.Length];
        generator.Generate(chaosBuffer, x0);

        // 4. XOR Encryption/Decryption
        for (int i = 0; i < data.Length; i++)
        {
            byte keyByte = (byte)(chaosBuffer[i] * 255.0);
            data[i] ^= keyByte;
        }
    }
}