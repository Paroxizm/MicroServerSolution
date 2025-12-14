using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using MicroServer.Model;
using Serilog;

namespace MicroServer;

/// <summary>
/// Чтение команд из канала и их выполнение в хранилище 
/// </summary>
/// <param name="storage"></param>
/// <param name="channel"></param>
internal class StorageClient(
    SimpleStore storage,
    Channel<CommandDto> channel)
    : IDisposable
{
    private Task? _readTask;
    private CancellationToken _cancellationToken = CancellationToken.None;

    public void Start(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
        _readTask = Task.Run(DoReading, cancellationToken);
    }

    private static readonly byte[] EmptyAnswer = "(nil)\r\n"u8.ToArray();
    private static readonly byte[] OkAnswer = "OK\r\n"u8.ToArray();
    private static readonly byte[] BadFormatAnswer = "-ERR UnknownOrMalformedCommand\r\n"u8.ToArray();

    /// <summary>
    /// Количество чтений из канала
    /// </summary>
    public int ReadCommands;
    
    /// <summary>
    /// Количество успешно обработанных команд
    /// </summary>
    public int GoodCommands;
    
    /// <summary>
    /// Количество сбоев (неправильная команда или ошибка обработки)
    /// </summary>
    public int FailCommands;

    private async Task DoReading()
    {
        try
        {
            while (!_cancellationToken.IsCancellationRequested)
            {
                CommandDto? command = null;
                try
                {
                    command = await channel.Reader.ReadAsync(_cancellationToken);

                    Interlocked.Increment(ref ReadCommands);
                    
                    var result = command.Command switch
                    {
                        CommandType.None => null,
                        CommandType.Get => storage.Get(command.Key) ?? EmptyAnswer,
                        CommandType.Set => SetInStorage(command.Key, command.Data?.ToArray() ?? [], command.Ttl),
                        CommandType.Delete => DeleteFromStorage(command.Key),
                        CommandType.Stat => GetStorageStatistic(),
                        _ => null
                    };

                    if (result == null)
                    {
                        Interlocked.Increment(ref FailCommands);
                        command.SourceTask?.SetResult(BadFormatAnswer);
                        
                        Console.WriteLine(JsonSerializer.Serialize(command));
                    }
                    else
                    {
                        Interlocked.Increment(ref GoodCommands);
                        command.SourceTask?.SetResult(result);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "StorageClient error: {msg}", e.Message);
                    Interlocked.Increment(ref FailCommands);
                    
                    command?.SourceTask?.SetException([e]);
                }
            }
        }
        finally
        {
            if (_readTask is { IsCompleted: true }) 
                _readTask.Dispose();
        }
    }

    private byte[] GetStorageStatistic()
    {
        var (get, set, delete) = storage.GetStatistic();
        return Encoding.UTF8.GetBytes($"GET: {get:000000}; SET: {set:000000}; DELETE: {delete:000000};\r\n");
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