using Aether.Core.Protocol;
using Aether.Core;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;

namespace Aether.Networking;

public class AetherServer(int port)
{
    private readonly TcpListener _listener = new(IPAddress.Any, port);

    public async Task StartAsync(CancellationToken token = default)
    {
        _listener.Start();
        Console.WriteLine($"[Server] Listening on port {_listener.LocalEndpoint}...");

        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(token);
                // Handle client in background (Fire-and-Forget)
                _ = ProcessClientAsync(client, token);
            }
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task ProcessClientAsync(TcpClient client, CancellationToken token)
    {
        Console.WriteLine($"[Server] Connection accepted from {client.Client.RemoteEndPoint}");

        using var stream = client.GetStream();
        // Convert NetworkStream to PipeReader for high-performance parsing
        var reader = PipeReader.Create(stream);

        // Initiate a secure session for this specific connection
        using var session = new SecureSession();

        try
        {
            while (true)
            {
                ReadResult result = await reader.ReadAsync(token);
                ReadOnlySequence<byte> buffer = result.Buffer;

                // Attempt to process as many packets as possible from the buffer
                while (TryProcessPacket(ref buffer, session, client))
                {
                    // Buffer advances as packets are consumed
                }

                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted) break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server Error] {ex.Message}");
        }
        finally
        {
            await reader.CompleteAsync();
            client.Close();
            Console.WriteLine("[Server] Client disconnected.");
        }
    }

    private bool TryProcessPacket(ref ReadOnlySequence<byte> buffer, SecureSession session, TcpClient client)
    {
        // A. Check for Header (25 Bytes)
        if (buffer.Length < PacketHeader.HeaderSize)
            return false;

        var headerSlice = buffer.Slice(0, PacketHeader.HeaderSize);

        PacketHeader header;
        if (headerSlice.IsSingleSegment)
        {
            if (!PacketHeader.TryParse(headerSlice.First.Span, out header)) return false;
        }
        else
        {
            // Handle fragmented header using stack memory
            Span<byte> localHeaderBuf = stackalloc byte[PacketHeader.HeaderSize];
            headerSlice.CopyTo(localHeaderBuf);
            if (!PacketHeader.TryParse(localHeaderBuf, out header)) return false;
        }

        // B. Check for Payload
        long totalPacketSize = PacketHeader.HeaderSize + header.PayloadLength;
        if (buffer.Length < totalPacketSize) return false;

        // C. Process Complete Packet
        var payloadSlice = buffer.Slice(PacketHeader.HeaderSize, header.PayloadLength);

        HandlePayload(header, payloadSlice, session, client);

        // D. Advance buffer
        buffer = buffer.Slice(totalPacketSize);
        return true;
    }

    private void HandlePayload(PacketHeader header, ReadOnlySequence<byte> payload, SecureSession session, TcpClient client)
    {
        byte[] data = payload.ToArray();

        // SCENARIO 1: HANDSHAKE (Exchange Keys)
        if (header.Type == PacketType.Handshake)
        {
            Console.WriteLine($"[Handshake] Received Client Public Key ({data.Length} bytes).");

            // Derive Shared Secret
            session.DeriveSharedSecret(data);

            // Send Server Public Key back
            byte[] myPublicKey = session.GetPublicKey();
            var respHeader = new PacketHeader(PacketType.Handshake, myPublicKey.Length, new byte[16]);

            var stream = client.GetStream();
            Span<byte> hBuf = stackalloc byte[PacketHeader.HeaderSize];
            respHeader.WriteTo(hBuf);

            stream.Write(hBuf);
            stream.Write(myPublicKey);

            Console.WriteLine("[Handshake] Sent Server Public Key. Secure Channel Ready. 🔒");
            return;
        }

        // SCENARIO 2: DATA (Encrypted Message)
        if (header.Type == PacketType.Data)
        {
            if (session.SharedSecret == null)
            {
                Console.WriteLine("[Security Alert] Client tried to send data before handshake!");
                return;
            }

            string encryptedPreview = Convert.ToBase64String(data);
            Console.WriteLine($"[Encrypted] {encryptedPreview[..Math.Min(data.Length, 20)]}...");

            // Decrypt using Shared Secret + IV
            ChaosCrypto.Process(data, session.SharedSecret, header.IV.Span);

            string message = System.Text.Encoding.UTF8.GetString(data);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Decrypted] {message}");
            Console.ResetColor();
        }
    }
}