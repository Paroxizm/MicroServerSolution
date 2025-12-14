namespace MicroServer.Model;

[GenerateBinarySerializer]
public partial class UserProfile
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}