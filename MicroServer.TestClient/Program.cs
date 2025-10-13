using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

var step = 5;

while (true)
{
    try
    {
        var socket = new Socket(IPAddress.Loopback.AddressFamily, SocketType.Stream, ProtocolType.IP);

        await socket.ConnectAsync([IPAddress.Loopback], 40567);

        var commandBuffer = ArrayPool<byte>.Shared.Rent(1024); 
        while (true)
        {
            try
            {
                var commandLength = CreateCommand(commandBuffer, step++);

                await socket.SendAsync(commandBuffer.AsMemory(0, commandLength), SocketFlags.None);
               
                Console.WriteLine($"{step} - sent {commandLength} bytes");
                
                await Task.Delay(500);
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
        Console.WriteLine(e);
        Console.WriteLine("Wait 5 seconds");
        await Task.Delay(5000);
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