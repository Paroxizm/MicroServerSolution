using System.Text.Json;
using BenchmarkDotNet.Attributes;
using MicroServer.Model;

namespace MicroServer.Benchmarks;

[MemoryDiagnoser]
public class BinarySerializationBenchmarks
{
    // размеры входных данных
    //[Params(10, 100, 1_000)]
    [Params(10, 100)]
    //[Params(10)]
    public int N;

    private UserProfile[] _data = [];

    [GlobalSetup]
    public void GlobalSetup()
    {
        _data = new UserProfile[N];
        for (var i = 0; i < N; i++)
            _data[i] = new UserProfile
            {
                Id = i,
                UserName = $"User {i}",
                CreatedAt = DateTime.UtcNow
            };
    }

    [Benchmark]
    public void JsonSerialization()
    {
        foreach (var profile in _data)
        {
            _stream.Position = 0;
            JsonSerializer.Serialize(_stream, profile);
        }
    }

    private readonly MemoryStream _stream = new (new byte[1024*1024]);
    
    [Benchmark]
    public void BinarySerialization()
    {
        foreach (var profile in _data)
        {
            _stream.Position = 0;
            profile.SerializeToBinary(_stream);
        }
    }
}