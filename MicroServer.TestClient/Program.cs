using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

var step = 2;

var timeout = int.Parse(args.FirstOrDefault(x => x.StartsWith("--timeout"))?.Split('=')[1] ?? "5000");

var address = args.FirstOrDefault(x => x.StartsWith("--address"))?.Split('=')[1] ?? "127.0.0.1";
var port = int.Parse(args.FirstOrDefault(x => x.StartsWith("--port"))?.Split('=')[1] ?? "40567");

Console.WriteLine($"TestClient started with:");
Console.WriteLine($" - timeout: [{timeout}]");
Console.WriteLine($" - address: [{address}]");
Console.WriteLine($" - port: [{port}]");

while (true)
{
    try
    {
        Console.WriteLine("Connecting...");
        var ipAddress = IPAddress.Parse(address); 
        var socket = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.IP);

        await socket.ConnectAsync([ipAddress], port);
        
        Console.WriteLine("Connected");
        
        var commandBuffer = ArrayPool<byte>.Shared.Rent(1024); 
        while (true)
        {
            try
            {
                var commandLength = CreateCommand(commandBuffer, step++);

                await socket.SendAsync(commandBuffer.AsMemory(0, commandLength), SocketFlags.None);
               
                Console.WriteLine($"{step:00000} - sent {commandLength} bytes");
                
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
        command = "1-GET KEY VALUE\n2-GET KEY VALUE\n"u8.ToArray();
    }
    else if (packet % 3 == 0)
    {
        command = "1-GET KEY VALUE\n2-GET KEY VALUE\n3-GET KEY VALUE\n"u8.ToArray();
        
    }
    else if (packet % 5 == 0)
    {
        command = "1-GET KEY VALUE\n2-GET KEY VALUE\n3-GET KEY VALUE\n4-GET KEY VALUE\n5-GET KEY VALUE\n"u8.ToArray();
    }
    
    else if (packet % 7 == 0)
    {
        command = "1-GET KEY VALUE 2-GET KEY VALUE 3-GET KEY VALUE 4-GET KEY VALUE 5-GET KEY VALUE\n"u8.ToArray();
    }
    
    else if (packet % 11 == 0)
    {
        command = "1-GET KEY VALUE 2-GET KEY VALUE 3-GET KEY VALUE 4-GET KEY VALUE 5-GET KEY VALUE"u8.ToArray();
    }
    else
        command = "1-GET KEY VALUE\n"u8.ToArray();
        
    Buffer.BlockCopy(command, 0, bytes, 0, command.Length);
    return command.Length;
}