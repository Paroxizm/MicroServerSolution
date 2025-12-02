namespace MicroServer.Model;

/// <summary>
/// Элмент кешированных данных
/// </summary>
internal class CacheItem
{
    public byte[]? Data { get; init; }
    public required DateTime ExpireAt { get; init; }
}