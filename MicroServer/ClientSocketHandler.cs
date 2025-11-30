using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using MicroServer.Model;
using Serilog;

namespace MicroServer;

public class ClientSocketHandler(
    Socket client,
    ChannelWriter<CommandDto> channel,
    Action<Socket> onConnectionHandled)
{
    public bool IsAlive { get; private set; }
    
    public static int CommandsReceived => _commandsReceived;
    private static int _commandsReceived;

    public int ReadsCount => _readsCount;
    private int _readsCount;
    public int ReadTotal => _readTotal;
    private int _readTotal;
    public int CommandsCount => _commandsCount;
    private int _commandsCount;

    public string ClientName = string.Empty;

    public async Task StartClientAsync(CancellationToken token)
    {
        try
        {
            IsAlive = true;
            ClientName = client.RemoteEndPoint?.ToString() ?? "<not set>";

            var storage = ArrayPool<byte>.Shared.Rent(1024 * 1024*10);
            try
            {
                var receiveBuffer = ArrayPool<byte>.Shared.Rent(4096);
                try
                {
                    var writeHead = 0;
                    var readHead = 0;
                    while (!token.IsCancellationRequested)
                    {
                        receiveBuffer.AsSpan().Clear();

                        var gotBytes = await client.ReceiveAsync(receiveBuffer);

                        if (gotBytes == 0)
                        {
                            Log.Information("Client [{addr}] disconnected", ClientName);
                            break;
                        }

                        Interlocked.Add(ref _readTotal, gotBytes);
                        Interlocked.Increment(ref _readsCount);

                        receiveBuffer
                            .AsSpan(0, gotBytes)
                            .CopyTo(storage.AsSpan(writeHead));
                        writeHead += gotBytes;

                        var (processedBytes, commandsFound) = ProcessBuffer(storage.AsSpan(readHead, writeHead));

                        _commandsCount += commandsFound?.Count ?? 0;

                        if (commandsFound != null)
                        {
                            foreach (var range in commandsFound)
                            {
                                Interlocked.Increment(ref _commandsReceived);

                                var response = await ProcessCommandData(storage[range]);
                                await client.SendAsync(response);
                            }
                        }

                        if (processedBytes == writeHead)
                        {
                            // обработали всё хранилище
                            writeHead = 0;
                            readHead = 0;
                            storage.AsSpan().Clear();
                            continue;
                        }
                        
                        readHead += processedBytes;
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(receiveBuffer);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(storage);
            }
        }
        catch (Exception e)
        {
            Log.Error("Error while processing client: {msg}", e.Message);
        }
        finally
        {
            IsAlive = false;
            onConnectionHandled(client);
        }
    }

    private async Task<byte[]> ProcessCommandData(byte[] bytes)
    {
        try
        {
            var commandStruct = CommandParser.Parse(bytes.AsSpan());
            var tcs = new TaskCompletionSource<byte[]>();

            var commandDto = new CommandDto
            {
                Command = Enum.TryParse<CommandType>(
                    Encoding.UTF8.GetString(commandStruct.Command),
                    true, out var type)
                    ? type
                    : CommandType.None,
                Key = Encoding.UTF8.GetString(commandStruct.Key).Trim(),
                Data = commandStruct.Data.ToArray(),
                Ttl = int.TryParse(Encoding.UTF8.GetString(commandStruct.Ttl).Trim(), out var ttl) ? ttl : 60,
                SourceTask = tcs
            };

            await channel.WriteAsync(commandDto);

            await tcs.Task;

            return tcs.Task.Result;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return "-ERRInternalError"u8.ToArray();
        }
    }

    private static (int, ICollection<Range>?) ProcessBuffer(ReadOnlySpan<byte> buffer)
    {
        const byte delimiter = 0x0A;
        var initialLength = buffer.Length;

        var separatorIndex = buffer.IndexOf(delimiter);
        if (separatorIndex < 0)
            return (0, null);

        var foundCommands = new List<Range>();
        var commandSlice = buffer.Slice(0, separatorIndex);
        while (!commandSlice.IsEmpty)
        {
            foundCommands.Add(new Range(initialLength - buffer.Length, separatorIndex));

            buffer = buffer.Slice(Math.Min(separatorIndex + 1, buffer.Length));
            if (buffer.IsEmpty)
                break;

            separatorIndex = buffer.IndexOf(delimiter);
            commandSlice = buffer.Slice(0, Math.Min(separatorIndex, buffer.Length));
        }

        return (initialLength - buffer.Length, foundCommands);
    }
}