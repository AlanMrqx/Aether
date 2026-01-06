using System.Text;
using Aether.Networking;

Console.Title = "Aether Secure Grid - v1.0";
Console.ForegroundColor = ConsoleColor.Cyan;

Console.WriteLine(@"
    ___    ________________  ___________ 
   /   |  / ____/_  __/ __ \/ ____/ __ \
  / /| | / __/   / / / /_/ / __/ / /_/ /
 / ___ |/ /___  / / / __  / /___/ _, _/ 
/_/  |_/_____/ /_/ /_/ /_/_____/_/ |_|  
                                        
   CHAOS-ENCRYPTED P2P GRID SYSTEM
   Powered by ChaoticEngine (AVX-512)
");
Console.ResetColor();

Console.WriteLine("Select Mode:");
Console.WriteLine(" [S] Start Server (Listener)");
Console.WriteLine(" [C] Start Client (Sender)");
Console.Write("> ");

var input = Console.ReadKey().Key;
Console.WriteLine();

try
{
    if (input == ConsoleKey.S)
    {
        await StartServerMode();
    }
    else if (input == ConsoleKey.C)
    {
        await StartClientMode();
    }
    else
    {
        Console.WriteLine("Invalid selection. Exiting.");
    }
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[CRITICAL ERROR]: {ex.Message}");
    Console.ResetColor();
}

static async Task StartServerMode()
{
    Console.Title = "Aether Server (Listening on 5000)";
    Console.WriteLine("Initializing Network Listener...");

    var server = new AetherServer(5000);
    // Listen indefinitely
    await server.StartAsync();
}

static async Task StartClientMode()
{
    Console.Title = "Aether Client";
    Console.Write("Enter Server IP (Default: 127.0.0.1): ");

    string ip = Console.ReadLine() ?? "127.0.0.1";
    if (string.IsNullOrWhiteSpace(ip)) ip = "127.0.0.1";

    using var client = new AetherClient();

    Console.WriteLine($"Connecting to {ip}:5000...");
    await client.ConnectAsync(ip, 5000);

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("CONNECTED! Type your message and press ENTER.");
    Console.ResetColor();

    while (true)
    {
        Console.Write("> ");
        string message = Console.ReadLine() ?? "";

        if (string.IsNullOrEmpty(message)) continue;
        if (message == "exit") break;

        byte[] data = Encoding.UTF8.GetBytes(message);
        await client.SendDataAsync(data);
    }
}