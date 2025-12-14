using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MicroServer.Bomber;

public class ClientInstance
{
    private readonly int _port;
    private readonly Socket _socket;
    private readonly IPAddress _address;

    public ClientInstance(string address, int port)
    {
        _port = port;
        _address = IPAddress.Parse(address);
        _socket = new Socket(_address.AddressFamily, SocketType.Stream, ProtocolType.IP);
        _socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, false);
    }

    public Task ConnectAsync()
    {
        return _socket.ConnectAsync([_address], _port);
    }

    public async Task<bool> RunDataCommand()
    {
        byte[]? commandBuffer = null;
        try
        {
            commandBuffer = ArrayPool<byte>.Shared.Rent(4096);

            var commandLen = CreateCommand(commandBuffer, Random.Shared.Next(100, 5000));

            var sentLen = await _socket.SendAsync(commandBuffer.AsMemory(0, commandLen));

            if (sentLen != commandLen)
                return false;

            var received = await _socket.ReceiveAsync(commandBuffer.AsMemory(0));

            if (received == 0)
                return false;

            var receivedData = Encoding.UTF8.GetString(commandBuffer, 0, received);

            var result = receivedData.Length > 0 && receivedData[0] != '-';
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return false;
        }
        finally
        {
            if (commandBuffer != null)
                ArrayPool<byte>.Shared.Return(commandBuffer);
        }
    }

    public void Close()
    {
        try
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _socket.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }


    private static int CreateCommand(in byte[] bytes, in int packet)
    {
        var type = Random.Shared.Next(1, 4);

        var command = type switch
        {
            1 => CreateSetCommand(5, 10, packet, 1),
            2 => CreateGetCommand(packet),
            3 => CreateSetCommand(5, 10, packet, 1),
            4 => CreateDeleteCommand(packet),
            _ => CreateGetCommand(packet)
        };

        Buffer.BlockCopy(command, 0, bytes, 0, command.Length);
        return command.Length;
    }

    private static byte[] CreateGetCommand(int p)
    {
        return Encoding.UTF8.GetBytes($"GET K-{p:0000}{(char)0x0A}");
    }

    private class UserProfileDto
    {
        [JsonInclude]
        public int Id { get; set; }
        
        [JsonInclude]
        public string UserName { get; set; } = string.Empty;
        
        [JsonInclude]
        public DateTime CreatedAt { get; set; }
    }
    
    private static byte[] CreateSetCommand(int minLength, int maxLength, int p, int ttl)
    {
        var profile = new UserProfileDto
        {
            CreatedAt = DateTime.UtcNow,
            Id = Random.Shared.Next(minLength, maxLength),
            UserName = "BOMBER USER"
        };
        
        var payload = JsonSerializer.Serialize(profile);

        return Encoding.UTF8.GetBytes($"SET K-{p:0000} {payload.Length} {payload} {ttl}{(char)0x0A}");
    }

    private static byte[] CreateDeleteCommand(int p)
    {
        return Encoding.UTF8.GetBytes($"DELETE K-{p:0000}{(char)0x0A}");
    }
}