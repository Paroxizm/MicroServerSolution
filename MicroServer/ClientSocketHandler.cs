using System.Buffers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;
using Serilog;

namespace MicroServer;

internal class ClientSocketHandler(
    Socket client,
    ChannelWriter<CommandDto> channel) : IDisposable
{
    public bool IsAlive { get; private set; }
    private bool _isClientDisposed;

    public int ReadsCount;
    public int ReadTotal;
    public int CommandsCount;

    public string ClientName = string.Empty;

    public async Task StartClientAsync(CancellationToken token)
    {
        try
        {
            IsAlive = true;
            ClientName = client.RemoteEndPoint?.ToString() ?? "<not set>";

            var storage = ArrayPool<byte>.Shared.Rent(1024 * 1024);
            var receiveBuffer = ArrayPool<byte>.Shared.Rent(1096);

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

                Interlocked.Add(ref ReadTotal, gotBytes);
                Interlocked.Increment(ref ReadsCount);

                receiveBuffer
                    .AsSpan(0, gotBytes)
                    .CopyTo(storage.AsSpan(writeHead));
                writeHead += gotBytes;

                var (processedBytes, commandsFound) = ProcessBuffer(storage.AsSpan(readHead, writeHead));

                CommandsCount += commandsFound?.Count ?? 0;

                if (commandsFound != null)
                {
                    foreach (var range in commandsFound)
                    {
                        var response = await ProcessCommandData(storage[range]);
                        
                        
                        // var operationResult = await onCommand.Invoke(storage[range]);
                        // if (operationResult is not { Length: > 0 }) 
                        //     continue;
                        //
                        // // SEND DATA LENGTH
                        // //await client.SendAsync(BitConverter.GetBytes(operationResult.Length));
                            
                        // SEND CONTENT
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

            ArrayPool<byte>.Shared.Return(receiveBuffer);
            ArrayPool<byte>.Shared.Return(storage);
        }
        catch (Exception e)
        {
            Log.Error("Error while processing client: {msg}", e.Message);
        }
        finally
        {
            client.Shutdown(SocketShutdown.Both);
            client.Close();
            client.Dispose();
            _isClientDisposed = true;
            IsAlive = false;
        }
    }

    private async Task<byte[]> ProcessCommandData(byte[] bytes)
    {
        var commandStruct = CommandParser.Parse(bytes.AsSpan());

        var tcs = new TaskCompletionSource<byte[]>();
        
        var commandDto = new CommandDto()
        {
            Command = Enum.TryParse<CommandType>(
                        Encoding.UTF8.GetString(commandStruct.Command),
                        true, out var type) ? type : CommandType.None,
            Key = Encoding.UTF8.GetString(commandStruct.Key),
            Data = commandStruct.Data.ToArray(),
            Ttl = int.TryParse(Encoding.UTF8.GetString(commandStruct.Ttl), out var ttl) ? ttl : 60,
            BackLink = tcs
        };
        
        await channel.WriteAsync(commandDto);
        await tcs.Task;
        
        return tcs.Task.Result;
    }

    private (int, ICollection<Range>?) ProcessBuffer(ReadOnlySpan<byte> buffer)
    {
        const byte delimiter = 0x0A;
        var initialLength = buffer.Length;
        //var commandsFound = 0;

        var separatorIndex = buffer.IndexOf(delimiter);
        if (separatorIndex < 0)
            return (0, null);

        var foundCommands = new List<Range>();
        var commandSlice = buffer.Slice(0, separatorIndex);
        while (!commandSlice.IsEmpty)
        {
            //foundCommands.Add(new Memory<byte>(commandSlice.ToArray()));

            foundCommands.Add(new Range(initialLength - buffer.Length, separatorIndex));

            // var operationResult = onCommand?.Invoke(commandSlice);
            // if (operationResult == null)
            // {
            //     // error or not subscribed
            // }
            // else
            // {
            //     #warning F&F
            //     client.SendAsync(operationResult).Wait();
            // }

            //commandsFound++;

            buffer = buffer.Slice(Math.Min(separatorIndex + 1, buffer.Length));
            if (buffer.IsEmpty)
                break;

            separatorIndex = buffer.IndexOf(delimiter);
            commandSlice = buffer.Slice(0, Math.Min(separatorIndex, buffer.Length));
        }

        return (initialLength - buffer.Length, foundCommands);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        //onCommand = null;
        if (_isClientDisposed)
            return;

        client.Shutdown(SocketShutdown.Both);
        client.Close();
        client.Dispose();
    }
}