using System.Buffers.Binary;

namespace Aether.Core.Protocol;

public enum PacketType : byte
{
    Unknown = 0x00,
    Handshake = 0x01, // ECDH Key Exchange
    Data = 0x02,      // Encrypted Payload
    Ack = 0x03,       // Acknowledgment
    Error = 0xFF      // Protocol Error
}

// Using readonly struct to minimize GC pressure (Zero-Allocation goal)
public readonly struct PacketHeader
{
    // "AETH" in ASCII -> 0x41455448
    public const uint MagicValue = 0x41455448;

    // Header Layout: [Magic(4)] + [Type(1)] + [Length(4)] + [IV(16)] = 25 Bytes
    public const int HeaderSize = 25;

    public PacketType Type { get; }
    public int PayloadLength { get; }
    public ReadOnlyMemory<byte> IV { get; }

    public PacketHeader(PacketType type, int payloadLength, ReadOnlyMemory<byte> iv)
    {
        Type = type;
        PayloadLength = payloadLength;
        IV = iv;
    }

    // --- PARSING (Read from Wire) ---
    public static bool TryParse(ReadOnlySpan<byte> buffer, out PacketHeader header)
    {
        header = default;

        if (buffer.Length < HeaderSize) return false;

        // Verify Magic Bytes (Big Endian is standard for network protocols)
        uint magic = BinaryPrimitives.ReadUInt32BigEndian(buffer.Slice(0, 4));
        if (magic != MagicValue) return false;

        PacketType type = (PacketType)buffer[4];
        int length = BinaryPrimitives.ReadInt32BigEndian(buffer.Slice(5, 4));

        // Extract IV (Must allocate array here as Ref Structs/Spans cannot be stored in fields)
        byte[] ivArray = buffer.Slice(9, 16).ToArray();

        header = new PacketHeader(type, length, ivArray);
        return true;
    }

    // --- SERIALIZATION (Write to Wire) ---
    public void WriteTo(Span<byte> destination)
    {
        if (destination.Length < HeaderSize)
            throw new ArgumentException("Buffer too small");

        BinaryPrimitives.WriteUInt32BigEndian(destination.Slice(0, 4), MagicValue);
        destination[4] = (byte)Type;
        BinaryPrimitives.WriteInt32BigEndian(destination.Slice(5, 4), PayloadLength);

        // Copy IV bytes
        IV.Span.CopyTo(destination.Slice(9, 16));
    }
}