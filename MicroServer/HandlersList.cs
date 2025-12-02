namespace MicroServer;

internal class HandlersList : IDisposable
{
    private readonly List<ClientSocketHandler> _handlers = [];
    
    private readonly ReaderWriterLockSlim _lock = new ();
    
    public List<ClientSocketHandler> Snapshot()
    {
        _lock.EnterReadLock();
        try
        {
            return _handlers.ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void AddHandler(ClientSocketHandler handler)
    {
        _lock.EnterWriteLock();
        try
        {
            _handlers.Add(handler);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Purge()
    {
        _lock.EnterWriteLock();
        try
        {
            _handlers.RemoveAll(x => !x.IsAlive);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            _handlers.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
        _lock.Dispose();
    }
}