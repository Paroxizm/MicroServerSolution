using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// Таймаут между отправками сообщений
var timeout = int.Parse(args.FirstOrDefault(x => x.StartsWith("--timeout"))?.Split('=')[1] ?? "5000");

// Адрес и порт отправки
var address = args.FirstOrDefault(x => x.StartsWith("--address"))?.Split('=')[1] ?? "127.0.0.1";
var port = int.Parse(args.FirstOrDefault(x => x.StartsWith("--port"))?.Split('=')[1] ?? "40567");

// Количество пакетов, отправляемое в одном подключении.
// При его достижении клиент переподключается 
var packetsToSend = int.Parse(args.FirstOrDefault(x => x.StartsWith("--packets"))?.Split('=')[1] ?? "0");

// Заголовок окна (не обязательно)
var title = args.FirstOrDefault(x => x.StartsWith("--title"))?.Split('=')[1];

// Если параметр найден - запросы и ответы будут выводиться в консоль
var verbose = args.Any(x => x.StartsWith("--verbose"));

if(!string.IsNullOrEmpty(title))
    Console.Title = title;

Console.WriteLine($"TestClient [{(string.IsNullOrEmpty(title) ? "untitled" : title)}] started with:");
Console.WriteLine($" - timeout: [{timeout}]");
Console.WriteLine($" - address: [{address}]");
Console.WriteLine($" - port: [{port}]");
Console.WriteLine($" - packets per connection: [{(packetsToSend > 0 ? packetsToSend.ToString() : "unlimited")}]");

while (true)
{
    var packetsSend = 0;
    try
    {
        Console.WriteLine("Connecting...");
        var ipAddress = IPAddress.Parse(address);
        var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.IP);

        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, false);
        await socket.ConnectAsync([ipAddress], port);

        Console.WriteLine($"Connected as {socket.LocalEndPoint?.ToString() ?? "<not set>"}");

        var commandBuffer = ArrayPool<byte>.Shared.Rent(4096);
        var totalSent = 0;

        while (true)
        {
            try
            {
                var keyNumber = Random.Shared.Next(1, 50);
                
                var commandLength = CreateCommand(commandBuffer, keyNumber);

                if(verbose)
                    Console.WriteLine("[REQUEST]: " + Encoding.UTF8.GetString(commandBuffer.AsSpan().Slice(0, commandLength)).Trim());
                
                await socket.SendAsync(commandBuffer.AsMemory(0, commandLength));
                
                var received = await socket.ReceiveAsync(commandBuffer.AsMemory(0));

                if (received == 0)
                {
                    Console.WriteLine("Connection broken!");
                    break;
                }
                if(verbose)
                    Console.WriteLine("[RESPONSE]: " + Encoding.UTF8.GetString(commandBuffer.AsSpan().Slice(0, received)).Trim());
                
                packetsSend++;

                totalSent += commandLength;
                
                if (packetsToSend > 0 && packetsSend >= packetsToSend)
                {
                    Console.WriteLine($"Breaking by {nameof(packetsSend)} limit");
                    break;
                }

                if (packetsSend % 100 == 0)
                    Console.WriteLine($"Sent {totalSent: ### ### ##0} bytes, {packetsSend} commands");
                
                await Task.Delay(timeout);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                break;
            }
        }

        socket.Shutdown(SocketShutdown.Both);
        socket.Close();
        socket.Dispose();

        await Task.Delay(timeout * 10);
    }
    catch (Exception e)
    {
        Console.WriteLine(e.Message);
        Console.WriteLine("Wait 5 seconds");
        Console.WriteLine("  ------");
        await Task.Delay(Math.Max(timeout * 3, 5000));
    }
}


static int CreateCommand(in byte[] bytes, in int packet)
{
    var type = Random.Shared.Next(1, 4);

    var command = type switch
    {
        1 => CreateSetCommand(10, 100, packet, 1),
        2 => CreateGetCommand(packet),
        3 => CreateSetCommand(10, 100, packet, 1),
        4 => CreateDeleteCommand(packet),
        _ => CreateGetCommand(packet)
    };
    
    Buffer.BlockCopy(command, 0, bytes, 0, command.Length);
    return command.Length;
}

static byte[] CreateGetCommand(int p)
{
    return Encoding.UTF8.GetBytes($"GET K-{p:0000}\n");
}
    
// static byte[] CreateSetCommand(int minLength, int maxLength, int p, int ttl)
// {
//     var payload = Enumerable
//         .Range(0, Random.Shared.Next(minLength, maxLength))
//         .Select(x => x.ToString("X2"))
//         .Aggregate("", (c, n) => c + n);
//         
//     return Encoding.UTF8.GetBytes($"SET K-{p:0000} {payload.Length} {payload} {ttl}\n");
// }

static byte[] CreateSetCommand(int minId, int maxId, int p, int ttl)
{
    var profile = new UserProfileDto
    {
        CreatedAt = DateTime.UtcNow,
        Id = Random.Shared.Next(minId, maxId),
        UserName = "BOMBER USER"
    };
        
    var payload = JsonSerializer.Serialize(profile);

    return Encoding.UTF8.GetBytes($"SET K-{p:0000} {payload.Length} {payload} {ttl}{(char)0x0A}");
}

static byte[] CreateDeleteCommand(int p)
{
    return Encoding.UTF8.GetBytes($"DELETE K-{p:0000}\n");
}

internal class UserProfileDto
{
    [JsonInclude]
    public int Id { get; set; }
        
    [JsonInclude]
    public string UserName { get; set; } = string.Empty;
        
    [JsonInclude]
    public DateTime CreatedAt { get; set; }
}