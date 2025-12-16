using MicroServer.Model;

namespace MicroServer.Tests;

public class BinarySerializationTests
{
    [Fact]
    public void Deserialized_Should_Be_Equal_To_Source()
    {
        var source = new UserProfile
        {
            CreatedAt = DateTime.UtcNow,
            Id = 123,
            UserName = "Test user"
        };
        
        var stream = new MemoryStream();
        source.SerializeToBinary(stream);
        
        stream.Position = 0;
        var deserialized = UserProfile.DeserializeFromBinary(stream);
        
        Assert.Equal(source.CreatedAt, deserialized.CreatedAt);
    }
}