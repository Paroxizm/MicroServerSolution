namespace MicroServer;

public class SimpleStore : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<string, byte[]> _storage = [];

    private int _setCount;
    private int _getCount;
    private int _deleteCount;

    public (int, int, int) GetStatistic() => (_getCount, _setCount, _deleteCount);

    /// <summary>
    /// Добавляет или обновляет значение по ключу
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Set(string key, byte[] value)
    {
        _lock.EnterWriteLock();
        try
        {
            _storage[key] = value;
            Interlocked.Increment(ref _setCount);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Возвращает значение по ключу или null, если ключ не найден.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public byte[]? Get(string key)
    {
        _lock.EnterReadLock();
        try
        {
            var value = _storage.GetValueOrDefault(key);
            Interlocked.Increment(ref _getCount);
            return value;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <summary>
    /// Удаляет ключ и значение
    /// </summary>
    /// <param name="key"></param>
    public void Delete(string key)
    {
        _lock.EnterWriteLock();
        try
        {
            _storage.Remove(key);
            Interlocked.Increment(ref _deleteCount);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    #if DEBUG
    public IReadOnlyDictionary<string, byte[]> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return _storage
                .Select(x => x)
                .ToDictionary(
                    x => x.Key, 
                    x => (byte[])x.Value.Clone());
            
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
    #endif

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.Dispose();
    }
}