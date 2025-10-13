namespace MicroServer;

/// <summary>
/// Структура команды
/// </summary>
/// <param name="command">Команда</param>
/// <param name="key">Ключ</param>
/// <param name="value">Значение</param>
/// <typeparam name="T">Тип данных</typeparam>
public readonly ref struct CommandParts<T>(ReadOnlySpan<T> command, ReadOnlySpan<T> key, ReadOnlySpan<T> value)
{
    public ReadOnlySpan<T> Command { get; } = command;
    public ReadOnlySpan<T> Key { get; } = key;
    public ReadOnlySpan<T> Value { get; } = value;

    public void Deconstruct(out ReadOnlySpan<T> command, out ReadOnlySpan<T> key, out ReadOnlySpan<T> value)
    {
        command = Command;
        key = Key;
        value = Value;
    }
}