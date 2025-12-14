using System.Text.Json;
using MicroServer.Model;
using Serilog;

namespace MicroServer;

public class SimpleStore : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new();
    private readonly Dictionary<string, CacheItem> _storage = [];

    private int _setCount;
    private int _getCount;
    private int _deleteCount;
    
    public (int get, int set, int delete) GetStatistic() => (_getCount, _setCount, _deleteCount);

    /// <summary>
    /// Добавляет или обновляет значение по ключу
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    /// <param name="ttl"></param>
    public void Set(string key, UserProfile value, int ttl = 60)
    {
        _lock.EnterWriteLock();
        try
        {
            var serialized = JsonSerializer.SerializeToUtf8Bytes(value);
            
            _storage[key] = new CacheItem
            {
                Data = serialized,
                ExpireAt = DateTime.UtcNow.AddSeconds(ttl)
            };
            
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
    public UserProfile? Get(string key)
    {
        var removeAsExpired = false;
        _lock.EnterReadLock();
        try
        {
            var value = _storage.GetValueOrDefault(key);
            Interlocked.Increment(ref _getCount);

            if (value == null)
                return null;

            if (value.ExpireAt >= DateTime.UtcNow)
            {
                try
                {
                    var profile = JsonSerializer.Deserialize<UserProfile>(value.Data);
                    return profile;
                }
                catch (Exception e)
                {
                    Log.Error("Item deserialization error: {msg}", e.Message);
                    return null;
                }
            }

            removeAsExpired = true;
            return null;

        }
        finally
        {
            _lock.ExitReadLock();
            
            if(removeAsExpired)
                Delete(key);
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
                    x => x.Value.Data != null 
                        ? (byte[])x.Value.Data.Clone()
                        : []);
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