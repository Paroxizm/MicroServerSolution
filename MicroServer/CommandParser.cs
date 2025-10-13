namespace MicroServer;

public static class CommandParser
{
    public static CommandParts<byte> Parse(ReadOnlySpan<byte> input)
    {
        var command = GetFirstValue(input);
        input = input.Slice(command.Length + CountSpacesToSkip(input.Slice(command.Length)));
        
        var commandKey = GetFirstValue(input);
        input = input.Slice(commandKey.Length + CountSpacesToSkip(input.Slice(commandKey.Length)));
        
        var commandValue = input;
        
        if(command.Length == 0 || commandKey.Length == 0)
            return new CommandParts<byte>(
                ReadOnlySpan<byte>.Empty, 
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty
            );    
        
        return new CommandParts<byte>(
            command, 
            commandKey,
            commandValue.Length == 0 ? ReadOnlySpan<byte>.Empty : commandValue
            );
        
        int CountSpacesToSkip(ReadOnlySpan<byte> buffer)
        {
            var index = 0;
            while (index < buffer.Length && buffer[index] == 0x20)
                index++;
            return index;
        }
        
        // получение блока до первого пробела или до конца входного буфера
        ReadOnlySpan<byte> GetFirstValue(ReadOnlySpan<byte> buffer)
        {
            var spaceIndex = buffer.IndexOf((byte)0x20);
            return spaceIndex == -1 
                ? buffer 
                : buffer.Slice(0, spaceIndex);
        }
    }
}