namespace MicroServer;

internal class HandlersList : IDisposable
{
    private readonly List<ClientSocketHandler> _handlers = [];
    
    private readonly ReaderWriterLockSlim _lock = new ();
    
    public List<ClientSocketHandler> Snapshot()
    {
        _lock.EnterReadLock();
        var result = _handlers.ToList();
        _lock.ExitReadLock();
        
        return result;
    }


    public void AddHandler(ClientSocketHandler handler)
    {
        _lock.EnterWriteLock();
        _handlers.Add(handler);
        _lock.ExitWriteLock();
        
    }

    public int Purge()
    {
        _lock.EnterWriteLock();
        var removed = _handlers.RemoveAll(x => !x.IsAlive);
        _lock.ExitWriteLock();
        
        return removed;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.EnterWriteLock();
        _handlers.Clear();
        _lock.ExitWriteLock();
        
        _lock.Dispose();
    }
}