using System.Buffers;
using System.Net.Sockets;
using Serilog;

namespace MicroServer;

internal class ClientSocketHandler(Socket client) : IDisposable
{
    public Action<ReadOnlySpan<byte>>? OnCommandReceived { get; set; }
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

                receiveBuffer.AsSpan(0, gotBytes).CopyTo(storage.AsSpan(writeHead));
                writeHead += gotBytes;

                var (processedBytes, commandsFound) = ProcessBuffer(storage.AsSpan(readHead, writeHead));

                CommandsCount += commandsFound;
                
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

    private (int, int) ProcessBuffer(Span<byte> buffer)
    {
        const byte delimiter = 0x0A;
        var initialLength = buffer.Length;
        var commandsFound = 0;

        var separatorIndex = buffer.IndexOf(delimiter);
        if (separatorIndex < 0)
            return (0, 0);

        var commandSlice = buffer.Slice(0, separatorIndex);
        while (!commandSlice.IsEmpty)
        {
            OnCommandReceived?.Invoke(commandSlice);
            commandsFound++;

            buffer = buffer.Slice(Math.Min(separatorIndex + 1, buffer.Length));
            if (buffer.IsEmpty)
                break;

            separatorIndex = buffer.IndexOf(delimiter);
            commandSlice = buffer.Slice(0, Math.Min(separatorIndex, buffer.Length));
        }

        return (initialLength - buffer.Length, commandsFound);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        OnCommandReceived = null;
        if (_isClientDisposed)
            return;

        client.Shutdown(SocketShutdown.Both);
        client.Close();
        client.Dispose();
    }
}