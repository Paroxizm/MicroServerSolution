using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Serilog;

namespace MicroServer;

public class TcpServer(ChannelWriter<CommandDto> writer, 
    //SimpleStore storage, 
    string address, int port)
{
    // stub for storage
    private int _gotCommands;

    private Socket? _socket;

    private readonly HandlersList _handlers = new();

    //private Channel<CommandDto> _channel = Channel.CreateBounded<CommandDto>(new BoundedChannelOptions(100));

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
                var handler = new ClientSocketHandler(client, writer);
                _handlers.AddHandler(handler);
                _ = handler.StartClientAsync(cancellationToken);

                Log.Information("Client [{client}] connected", client.RemoteEndPoint?.ToString() ?? "(null)");
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
        if (state is not HandlersList handlers)
            return;

        // очистка отключенных соединений
        var purged = handlers.Purge();
        if (purged > 0)
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

    private static readonly byte[] EmptyAnswer = "(nil)\n\r"u8.ToArray();
    private static readonly byte[] OkAnswer = "OK\n\r"u8.ToArray();
    private static readonly byte[] NotSupportedCommandAnswer = "-ERRCommandNotSupported\n\r"u8.ToArray();
    private static readonly byte[] BadFormatAnswer = "-ERRBadCommandFormat\n\r"u8.ToArray();

    /// <summary>
    /// Фиксация данных, полученных от клиентов
    /// </summary>
    /// <param name="commandBuffer">Буфер с единичной командой</param>
    // private async ValueTask<byte[]?> OnCommandReceived(byte[] commandBuffer)
    // {
    //     //var (command, key, arg1, arg2, arg3) = CommandParser.Parse(commandBuffer);
    //     Interlocked.Increment(ref _gotCommands);
    //
    //     // Log.Debug("{cmd} : {key} : {value}",
    //     //     Encoding.UTF8.GetString(command),
    //     //     Encoding.UTF8.GetString(key),
    //     //     Encoding.UTF8.GetString(arg1)
    //     // );
    //     //
    //     // var commandValue = Encoding.UTF8.GetString(command);
    //     // var keyValue = Encoding.UTF8.GetString(key);
    //     //
    //     // if (command.IsEmpty || key.IsEmpty)
    //     //     return BadFormatAnswer;
    //     //
    //     // switch (commandValue)
    //     // {
    //     //     case "GET":
    //     //         return storage.Get(keyValue) ?? EmptyAnswer;
    //     //
    //     //     case "SET":
    //     //         storage.Set(keyValue, arg2.ToArray(), BitConverter.ToInt32(arg3));
    //     //         return OkAnswer;
    //     //
    //     //     case "DELETE":
    //     //         storage.Delete(keyValue);
    //     //         return OkAnswer;
    //     //
    //     //     default:
    //     //         return NotSupportedCommandAnswer;
    //     // }
    // }
}