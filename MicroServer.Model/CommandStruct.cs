namespace MicroServer.Model;

/// <summary>
/// Структура команды
/// </summary>
/// <param name="command">Команда</param>
/// <param name="key">Ключ</param>
/// <param name="data">Значение</param>
/// <typeparam name="T">Тип данных</typeparam>
public readonly ref struct CommandStruct<T>(ReadOnlySpan<T> command, ReadOnlySpan<T> key, ReadOnlySpan<T> length, ReadOnlySpan<T> data, ReadOnlySpan<T> ttl)
{
    public ReadOnlySpan<T> Command { get; } = command;
    public ReadOnlySpan<T> Key { get; } = key;
    public ReadOnlySpan<T> Length { get; } = length;
    public ReadOnlySpan<T> Data { get; } = data;
    public ReadOnlySpan<T> Ttl { get; } = ttl;

    public void Deconstruct(out ReadOnlySpan<T> command, out ReadOnlySpan<T> key,  out ReadOnlySpan<T> length, out ReadOnlySpan<T> data, out ReadOnlySpan<T> ttl)
    {
        command = Command;
        key = Key;
        length =  Length;
        data =  Data;
        ttl =  Ttl;
    }
}