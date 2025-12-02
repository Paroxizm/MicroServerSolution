namespace MicroServer.Model;

public class CommandDto
{
    public CommandType Command { get; init; } = CommandType.None;
    public string Key { get; init; } = string.Empty;
    public byte[]? Data { get; init; }
    public int Ttl { get; init; }
    public TaskCompletionSource<byte[]>? SourceTask { get; init; }
}