using MicroServer.Model;

namespace MicroServer;

public static class CommandParser
{
    public static CommandStruct<byte> Parse(ReadOnlySpan<byte> input)
    {
        var commandSpan = GetFirstValue(input);
        input = input.Slice(commandSpan.Length + CountSpacesToSkip(input.Slice(commandSpan.Length)));
        
        var keySpan = GetFirstValue(input);
        input = input.Slice(keySpan.Length + CountSpacesToSkip(input.Slice(keySpan.Length)));

        var lengthSpan = ReadOnlySpan<byte>.Empty;
        var dataSpan = ReadOnlySpan<byte>.Empty;
        var ttlSpan = ReadOnlySpan<byte>.Empty;
        
        if (!input.IsEmpty)
        {
            lengthSpan = GetFirstValue(input);
            
            input = input.Slice(lengthSpan.Length + CountSpacesToSkip(input.Slice(lengthSpan.Length)));
           
            var length = int.TryParse(lengthSpan, out var len) ? len : 0;

            if (length > 0 && input.Length >= length)
            {
                dataSpan = input.Slice(0, len);
                input = input.Slice(dataSpan.Length + CountSpacesToSkip(input.Slice(dataSpan.Length)));
            }
            else input = Span<byte>.Empty;
            
            if(!input.IsEmpty)
                ttlSpan = GetFirstValue(input);
        }

        if(commandSpan.Length == 0)
            return new CommandStruct<byte>(
                ReadOnlySpan<byte>.Empty, 
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty,
                ReadOnlySpan<byte>.Empty
            );    
        
        return new CommandStruct<byte>(
            commandSpan, 
            keySpan,
            lengthSpan,
            dataSpan,
            ttlSpan
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