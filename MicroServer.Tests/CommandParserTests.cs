using System.Collections;
using System.Text;
using FluentAssertions;

namespace MicroServer.Tests;

/// <summary>
/// Тесты парсинга команд <see cref="CommandParser"/>
/// </summary>
public class CommandParserTests
{
    /// <summary>
    /// Корректный разбор команды SET с четырьмя аргументами.
    /// CMD KEY LEN VALUE
    /// </summary>
    [Theory]
    [ClassData(typeof(CorrectSetSamples))]
    public void Should_Parse_Command_Set_With_Four_Arguments(
        byte[] inputBuffer,
        byte[] expectedCommand, byte[] expectedKey, 
        byte[] expectedLength,
        byte[] expectedValue, byte[] _)
    {
        var (command,
             key,
             length,
             value,
             _)
            = CommandParser.Parse(inputBuffer.AsSpan());

        command.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedCommand);

        key.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedKey);
        
        length.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedLength);
        
        value.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedValue);
    }

    /// <summary>
    /// Корректный разбор команды SET с пятью аргументами
    /// CMD KEY LEN VALUE TTL
    /// </summary>
    [Theory]
    [ClassData(typeof(CorrectSetSamples))]
    public void Should_Parse_Command_Set_With_Five_Arguments(
        byte[] inputBuffer,
        byte[] expectedCommand, byte[] expectedKey, 
        byte[] expectedLength,
        byte[] expectedValue,
        byte[] expectedTtl
        )
    {
        var (command,
                key,
                length,
                value,
                ttl)
            = CommandParser.Parse(inputBuffer.AsSpan());

        command.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedCommand);

        key.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedKey);
        
        length.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedLength);
        
        value.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedValue);
        
        ttl.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedTtl);
    }
    
    /// <summary>
    /// Корректный разбор команды GET с двумя аргументами.
    /// </summary>
    [Theory]
    [ClassData(typeof(CorrectGetSamples))]
    public void Should_Parse_Command_Get_With_Two_Arguments(
        byte[] inputBuffer,
        byte[] expectedCommand, byte[] expectedKey, byte[] _)
    {
        var (command, key, _, value, _) = CommandParser.Parse(inputBuffer.AsSpan());

        command.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedCommand);

        key.ToArray()
            .Should().NotBeEmpty()
            .And.BeEquivalentTo(expectedKey);

        value.ToArray().Should().BeEmpty();
    }

    /// <summary>
    /// Обработка некорректной команды (например, без ключа).
    /// </summary>
    [Theory]
    [ClassData(typeof(MissingKeySamples))]
    public void Should_Fail_If_Command_Get_Key_Missing(byte[] inputBuffer)
    {
        var (command, key, _, value, _) = CommandParser.Parse(inputBuffer.AsSpan());

        command.ToArray().Should().BeEmpty();
        key.ToArray().Should().BeEmpty();
        value.ToArray().Should().BeEmpty();
    }

    /// <summary>
    /// Обработка команды с лишними пробелами между аргументами.
    /// </summary>
    [Theory]
    [ClassData(typeof(MultiSpacedSamples))]
    public void Should_Parse_With_Multiple_Spaces(byte[] inputBuffer,
        byte[] expectedCommand, byte[] expectedKey, 
        byte[] expectedLength, byte[] expectedValue, byte[] expectedTtl)
    {
        var (command, key, length, value, ttl) = CommandParser.Parse(inputBuffer.AsSpan());

        command.ToArray()
            .Should().BeEquivalentTo(expectedCommand);

        key.ToArray()
            .Should().BeEquivalentTo(expectedKey);
        
        length.ToArray()
            .Should().BeEquivalentTo(expectedLength);
        
        value.ToArray()
            .Should().BeEquivalentTo(expectedValue);
        
        ttl.ToArray()
            .Should().BeEquivalentTo(expectedTtl);
    }
}

public abstract class CommandSamples : IEnumerable<object[]>
{
    internal static readonly List<string[]> CorrectSetCommands =
    [
        ["SET KEY1 6 VALUE1 1", "SET", "KEY1", "6", "VALUE1", "1"],
        ["SET KEY2 10 VALUE2 EXT 100", "SET", "KEY2", "10", "VALUE2 EXT", "100"]
    ];

    internal static readonly List<string[]> CorrectGetCommands =
    [
        ["GET KEY1", "GET", "KEY1", ""],
        ["GET KEY2", "GET", "KEY2", ""]
    ];


    internal static readonly List<string[]> MissingKeyCommands =
    [
        ["GET"],
        ["GET "],
        ["SET"],
        ["SET "]
    ];

    internal static readonly List<string[]> MultiSpacedCommands =
    [
        ["SET  KEY1 6 VALUE1 10", "SET", "KEY1", "6", "VALUE1", "10"],
        ["SET KEY1  6 VALUE1 100", "SET", "KEY1", "6", "VALUE1", "100"],
        ["SET   KEY1  6 VALUE1 200", "SET", "KEY1", "6", "VALUE1", "200"],
        ["SET KEY2 10 VALUE2 EXT 300", "SET", "KEY2", "10", "VALUE2 EXT", "300"]
    ];

    protected abstract List<string[]> CommandSet { get; }

    /// <inheritdoc />
    public IEnumerator<object[]> GetEnumerator()
        => CommandSet
            .Select(x =>
                x.Select(object (b) => Encoding.UTF8.GetBytes(b)).ToArray())
            .GetEnumerator();


    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public class CorrectSetSamples : CommandSamples
{
    protected override List<string[]> CommandSet => CorrectSetCommands;
}

public class MissingKeySamples : CommandSamples
{
    protected override List<string[]> CommandSet => MissingKeyCommands;
}

public class MultiSpacedSamples : CommandSamples
{
    /// <inheritdoc />
    protected override List<string[]> CommandSet => MultiSpacedCommands;
}

public class CorrectGetSamples : CommandSamples
{
    /// <inheritdoc />
    protected override List<string[]> CommandSet => CorrectGetCommands;
}