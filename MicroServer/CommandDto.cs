namespace MicroServer;

public class CommandDto
{
    public CommandType Command { get; set; } = CommandType.None;
    public string Key { get; set; } = string.Empty;
    public byte[]? Data { get; set; }
    public int Ttl { get; set; }
    public TaskCompletionSource<byte[]>? BackLink { get; set; }
}