using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

var packetNumber = 2;

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
                var commandLength = CreateCommand(commandBuffer, packetNumber);

                await socket.SendAsync(commandBuffer.AsMemory(0, commandLength));

                packetsSend++;

                totalSent += commandLength;
                if (timeout > 1000)
                    Console.WriteLine($"{packetNumber:00000} - sent {commandLength} bytes");
                else if (packetNumber % 100 == 0)
                {
                    Console.WriteLine($"sent {totalSent} bytes");
                    totalSent = 0;
                }

                packetNumber++;

                if (packetsToSend > 0 && packetsSend >= packetsToSend)
                {
                    Console.WriteLine($"Breaking by {nameof(packetsSend)} limit");
                    packetNumber = 0;
                    break;
                }

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
    byte[] command;


    if (packet % 2 == 0)
    {
        command = CreateGetCommand(packet);
    }
    else if (packet % 3 == 0)
    {
        command = CreateSetCommand(10, 100, packet);
    }
    else if (packet % 5 == 0)
    {
        command = CreateSetCommand(10, 500, packet);
    }

    else if (packet % 7 == 0)
    {
        command = CreateDeleteCommand(packet);
    }

    else if (packet % 11 == 0)
    {
        command = CreateSetCommand(10, 800, packet);
    }
    else
        command = CreateGetCommand(packet);

    Buffer.BlockCopy(command, 0, bytes, 0, command.Length);
    return command.Length;
    
    byte[] CreateGetCommand(int p)
    {
        return Encoding.UTF8.GetBytes($"GET K-{p:0000}\n");
    }
    
    byte[] CreateSetCommand(int minLength, int maxLength, int p)
    {
        return Encoding.UTF8.GetBytes(
            Enumerable
                .Range(0, Random.Shared.Next(minLength, maxLength))
                .Select(x => x.ToString("X2"))
                .Aggregate($"SET K-{p:0000} ", (c, n) => c + n) + "\n"
        );
    }
    
    byte[] CreateDeleteCommand(int p)
    {
        return Encoding.UTF8.GetBytes($"DELETE K-{p:0000}\n");
    }
    
    
}