using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using MicroServer.Model;
using Serilog;

namespace MicroServer;

public class TcpServer(
    ChannelWriter<CommandDto> writer,
    string address,
    int port,
    int maxConcurrentConnections,
    int maxCommandSize)
{
    private Socket? _socket;
    private readonly HandlersList _handlers = new();

    private readonly SemaphoreSlim _connectionLimiter = new (maxConcurrentConnections);
    
    public List<ClientSocketHandler> GetSnapshot() => _handlers.Snapshot();
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.IP);
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        _socket.Listen();

        try
        {
            
            
            while (!cancellationToken.IsCancellationRequested)
            {
                var clientSocket = await _socket.AcceptAsync(cancellationToken);
                Telemetry.ClientsAccepted();
                
                await _connectionLimiter.WaitAsync(cancellationToken);
                Telemetry.ClientConnected();
                
                var handler = new ClientSocketHandler(clientSocket, writer, maxCommandSize, OnConnectionHandled);
                _handlers.AddHandler(handler);

                _ = handler.StartClientAsync(cancellationToken);

                Log.Information("Client [{client}] connected", clientSocket.RemoteEndPoint?.ToString() ?? "(null)");
            }
        }
        catch (OperationCanceledException)
        {
            // stopping
        }
        finally
        {
            if (_socket.Connected)
                _socket.Shutdown(SocketShutdown.Both);

            _socket.Close();
            _socket.Dispose();

            _handlers.Dispose();
        }
    }

    private void OnConnectionHandled(Socket clientSocket, bool disconnectedByServer)
    {
        try
        {
            clientSocket.Shutdown(SocketShutdown.Both);
            clientSocket.Close();
            clientSocket.Dispose();
        }
        catch (Exception e)
        {
            Console.WriteLine("Error on complete socket: " + e.Message);
        }
        finally
        {
            Telemetry.ClientDisconnected();
            if(disconnectedByServer)
                Telemetry.ClientDisconnectedByServer();
            _connectionLimiter.Release();
        }
    }
    
    public void Cleanup()
    {
        _handlers.Purge();
    }
}