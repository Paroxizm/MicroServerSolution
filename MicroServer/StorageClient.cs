using System.Text;
using System.Threading.Channels;

namespace MicroServer;

internal class StorageClient
    (SimpleStore storage, Channel<CommandDto> channel)
    : IDisposable
{
    // private readonly SimpleStore _storage;
    // private readonly Channel<CommandDto> _channel;
    private Task? _readTask;
    private CancellationToken _cancellationToken = CancellationToken.None;

    // public StorageClient(SimpleStore storage, Channel<CommandDto> channel)
    // {
    //     _storage = storage;
    //     _channel = channel;
    // }

    public void Start(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _readTask = Task.Run(DoReading, cancellationToken);
    }

    private static readonly byte[] EmptyAnswer = "(nil)\n\r"u8.ToArray();
    private static readonly byte[] OkAnswer = "OK\n\r"u8.ToArray();
    private static readonly byte[] NotSupportedCommandAnswer = "-ERRCommandNotSupported\n\r"u8.ToArray();
    private static readonly byte[] BadFormatAnswer = "-ERRBadCommandFormat\n\r"u8.ToArray();
    private async Task DoReading()
    {
        while (!_cancellationToken.IsCancellationRequested)
        {
            var command = await channel.Reader.ReadAsync(_cancellationToken);

            Console.WriteLine("GOT COMMAND: " + command.Command);
            
            if (command.Command == CommandType.None)
            {
                command.BackLink?.SetResult(BadFormatAnswer);
                return;
            }

            var result = command.Command switch
            {
                CommandType.None => BadFormatAnswer,
                CommandType.Get => storage.Get(command.Key) ?? EmptyAnswer,
                CommandType.Set => SetInStorage(command.Key, command.Data?.ToArray() ?? [], command.Ttl),
                CommandType.Delete => DeleteFromStorage(command.Key),
                CommandType.Stat => GetStorageStatistic(),
                _ => NotSupportedCommandAnswer
            };

            command.BackLink?.SetResult(result);
        }
    }

    private byte[] GetStorageStatistic()
    {
        var (get, set, delete) = storage.GetStatistic();
        return Encoding.UTF8.GetBytes($"GET: {get}; SET: {set}; DELETE: {delete};");
    }

    private byte[] DeleteFromStorage(string key)
    {
        storage.Delete(key);
        return OkAnswer;
    }

    private byte[] SetInStorage(string key, byte[] value, int ttl)
    {
        storage.Set(key, value, ttl);
        return OkAnswer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _readTask?.Dispose();
    }
}