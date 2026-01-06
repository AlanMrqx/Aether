using Aether.Core;
using Aether.Core.Protocol;
using System.Net.Sockets;

namespace Aether.Networking;

public class AetherClient : IDisposable
{
    private TcpClient? _client;
    private NetworkStream? _stream;
    private SecureSession? _session;

    public bool IsConnected => _client != null && _client.Connected;

    public async Task ConnectAsync(string ipAddress, int port)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(ipAddress, port);
        _stream = _client.GetStream();

        Console.WriteLine("[Client] Connected. Starting Secure Handshake...");
        _session = new SecureSession();

        // 1. Send Client Public Key
        byte[] myPublicKey = _session.GetPublicKey();
        var handshakePacket = new PacketHeader(PacketType.Handshake, myPublicKey.Length, new byte[16]);

        Span<byte> headerBuf = stackalloc byte[PacketHeader.HeaderSize];
        handshakePacket.WriteTo(headerBuf);

        await _stream.WriteAsync(headerBuf.ToArray());
        await _stream.WriteAsync(myPublicKey);

        // 2. Receive Server Public Key
        byte[] responseHeaderBuf = new byte[PacketHeader.HeaderSize];
        await _stream.ReadExactlyAsync(responseHeaderBuf);

        if (PacketHeader.TryParse(responseHeaderBuf, out var srvHeader))
        {
            byte[] srvPublicKey = new byte[srvHeader.PayloadLength];
            await _stream.ReadExactlyAsync(srvPublicKey);

            // 3. Complete Handshake
            _session.DeriveSharedSecret(srvPublicKey);
            Console.WriteLine("[Client] Handshake Complete! Secure Channel Established. 🔒");
        }
    }

    public async Task SendDataAsync(byte[] data)
    {
        if (!IsConnected || _stream == null || _session?.SharedSecret == null)
            throw new InvalidOperationException("Not connected or Handshake not complete.");

        // 1. Generate unique IV (Salt) for this packet
        byte[] iv = new byte[16];
        Random.Shared.NextBytes(iv);

        // 2. Encrypt Data (In-Place)
        ChaosCrypto.Process(data, _session.SharedSecret, iv);

        // 3. Create and Send Packet
        var header = new PacketHeader(PacketType.Data, data.Length, iv);

        Span<byte> headerBuffer = stackalloc byte[PacketHeader.HeaderSize];
        header.WriteTo(headerBuffer);

        await _stream.WriteAsync(headerBuffer.ToArray());
        await _stream.WriteAsync(data);

        Console.WriteLine($"[Client] Sent {data.Length} bytes (Encrypted).");
    }

    public void Disconnect()
    {
        _stream?.Close();
        _client?.Close();
        _session?.Dispose();
        Console.WriteLine("[Client] Disconnected.");
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}