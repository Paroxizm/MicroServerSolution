namespace MicroServer;

public class SimpleStore
{
    private readonly Dictionary<string, byte[]> _storage = [];
    
    /// <summary>
    /// Добавляет или обновляет значение по ключу
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    public void Set(string key, byte[] value)
    {
        _storage[key] = value;
    }

    /// <summary>
    /// Возвращает значение по ключу или null, если ключ не найден.
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    public byte[]? Get(string key)
    {
        return _storage.GetValueOrDefault(key);
    }

    /// <summary>
    /// Удаляет ключ и значение
    /// </summary>
    /// <param name="key"></param>
    public void Delete(string key)
    {
        _storage.Remove(key);
    }
}