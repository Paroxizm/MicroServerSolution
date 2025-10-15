using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace MicroServer;

public class TcpServer(string address, int port)
{
    // stub for storage
    private int _gotCommands;
    
    private Socket? _socket;

    private readonly HandlersList _handlers = new();
   
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var timer = new Timer(TimerCallback, _handlers, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

        _socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.IP);
        _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
        _socket.Listen();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _socket.AcceptAsync(cancellationToken);
                var handler = new ClientSocketHandler(client);
                handler.OnCommandReceived += OnCommandReceived;
                _handlers.AddHandler(handler);
                _ = handler.StartClientAsync(cancellationToken);
                
                Log.Information("Client [{client}] connected", client.RemoteEndPoint?.ToString() ??  "(null)");
            }
        }
        finally
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _socket.Dispose();
            
            await timer.DisposeAsync();
            _handlers.Dispose();
        }
    }
 
    /// <summary>
    /// Отображение статистики и чистка данных о неактивных клиентах
    /// </summary>
    /// <param name="state"></param>
    private void TimerCallback(object? state)
    {
        if(state is not HandlersList handlers)
            return;
            
        // очистка отключенных соединений
        var purged = handlers.Purge();
        if(purged > 0)
            Log.Debug("Purged handlers: [{purged}]", purged);

        var connections = handlers.Snapshot();
        
        // вывод статистики соединений
        Log.Information("Got commands: {enqueued}", _gotCommands);
        Log.Debug("Actual handlers: [{cnt}]", connections.Count);
            
        foreach (var handler in connections)
        {
            Log.Debug(
                " > {state} [{endpoint}] reads: {reads:0000}, commands: {commands:0000}, received: {received:### ### ##0}",
                handler.IsAlive ? "[ ]" : "[x]",
                handler.ClientName,
                handler.ReadsCount, handler.CommandsCount, handler.ReadTotal);
        }
    }

    /// <summary>
    /// Фиксация данных, полученных от клиентов
    /// </summary>
    /// <param name="commandBuffer">Буфер с единичной командой</param>
    private void OnCommandReceived(ReadOnlySpan<byte> commandBuffer)
    {
        var (command, key, value) = CommandParser.Parse(commandBuffer);
        Interlocked.Increment(ref _gotCommands);
        
        Log.Debug("{cmd} : {key} : {value}",
            Encoding.UTF8.GetString(command),
            Encoding.UTF8.GetString(key),
            Encoding.UTF8.GetString(value)
        );
    }
}