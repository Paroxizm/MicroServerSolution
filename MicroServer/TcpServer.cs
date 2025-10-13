using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace MicroServer;

/*

Описание/Пошаговая инструкция выполнения домашнего задания:
Пункт 1. Создание класса TCP-сервера
Создайте класс TcpServer. В нем реализуйте метод StartAsync, который будет инициализировать Socket,
связывать его с локальным IP-адресом и портом (например, 127.0.0.1:8080)
и переводить в режим прослушивания (Listen).

Пункт 2. Реализация цикла приема подключений
В методе StartAsync после вызова Listen организуйте бесконечный асинхронный цикл (while(true)),
который ожидает новые подключения с помощью await serverSocket.AcceptAsync(). Для каждого принятого
клиентского сокета запускайте отдельную задачу для его обработки
(например, _ = ProcessClientAsync(clientSocket)).

Пункт 3. Чтение данных от клиента и парсинг
Реализуйте приватный асинхронный метод ProcessClientAsync(Socket clientSocket).
Внутри него организуйте цикл для чтения данных.
При чтении (await clientSocket.ReceiveAsync(...)) используйте буфер,
арендованный из ArrayPool.Shared. Полученные данные передавайте в статический метод CommandParser.Parse
из ДЗ №1. Результат парсинга (команду, ключ, значение) выводите в консоль.
Не забудьте возвращать буфер в пул после использования.

Пункт 4. Обработка отключения клиента
Модифицируйте цикл чтения данных в ProcessClientAsync.
Если вызов ReceiveAsync возвращает 0, это означает, что клиент закрыл соединение.
В этом случае необходимо прервать цикл, корректно закрыть сокет клиента (Shutdown, Close, Dispose)
и завершить задачу обработки.

Пункт 5. Запуск сервера
В Program.cs создайте экземпляр вашего TcpServer,
вызовите его метод StartAsync и обеспечьте работу приложения в фоновом режиме,
чтобы оно не завершилось сразу после запуска (например, с помощью Console.ReadLine()).




 */

public class TcpServer
{
    //private TcpListener? _listener;

    //private Thread? _thread;


    private Socket? _socket;

    private Timer? _timer;
    
    public async Task StartAsync()
    {
        _timer = new(x =>
        {
            Log.Information("Enqueued: {enqueued}", _commandsBuffer.Count);
            //Console.WriteLine("enqueued: " + _commandsBuffer.Count);
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
        
        _socket = new Socket(IPAddress.Any.AddressFamily, SocketType.Stream, ProtocolType.IP);
        _socket.Bind(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 40567));
        _socket.Listen();

        while (true)
        {
            var client = await _socket.AcceptAsync();
            var handler = new ConnectionHandler(client);
            handler.OnCommandReceived += OnCommandReceived;
            
            _ = handler.StartClientAsync();
        }
    }

    private ConcurrentQueue<string> _commandsBuffer = new();
    
    private void OnCommandReceived(string command)
    {
        _commandsBuffer.Enqueue(command);
    }
}

internal class ConnectionHandler : IDisposable
{
    private readonly Socket _client;

    public ConnectionHandler(Socket client)
    {
        _client = client;
    }

    public Action<string>? OnCommandReceived { get; set; }
    
    public async Task StartClientAsync()
    {
        try
        {
            var command = ArrayPool<byte>.Shared.Rent(1024*1024);
            var commandPos = 0;
            while (true)
            {
                var buffer = ArrayPool<byte>.Shared.Rent(1024);
                Array.Clear(buffer, 0, buffer.Length);

                var gotBytes = await _client.ReceiveAsync(buffer);

                if (gotBytes == 0)
                {
                    Log.Warning("Client disconnected");
                    _client.Shutdown(SocketShutdown.Both);
                    _client.Close();
                    _client.Dispose();

                    break;
                }

                var idx = Array.FindIndex(buffer, 0, x => x == 0x0A);
                if (idx >= 0)
                {
                    var start = 0;
                    do
                    {
                        Buffer.BlockCopy(buffer, start, command, commandPos, idx - start);

                        try
                        {
                            OnCommandReceived?.Invoke(Encoding.UTF8.GetString(command, 0, commandPos + idx));
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.Message);
                            Log.Error("commandPos: {commandPos}\nidx: {idx}\ncommand: {command}", 
                                commandPos, 
                                idx, 
                                command.Aggregate("", (c,n) => c + (char)n));
                        }

                        Array.Clear(command);
                        start = idx + 1;
                        idx = Array.FindIndex(buffer, start, x => x == 0x0A);
                        
                    } while (idx >= 0 && idx < gotBytes && start < gotBytes);
                    
                    if (gotBytes - start > 0) // tail present
                    {
                        Buffer.BlockCopy(buffer, start, command, 0, gotBytes - start);
                        commandPos = gotBytes - start;
                    }

                    Array.Clear(buffer);
                }
                else
                {
                    Buffer.BlockCopy(buffer, 0, command, commandPos, gotBytes);
                    commandPos += gotBytes;
                }

                ArrayPool<byte>.Shared.Return(buffer);
            }

            ArrayPool<byte>.Shared.Return(command);
        }
        catch (Exception e)
        {
            Log.Error(e, "Error while processing client: {msg}", e.Message);
        }
        finally
        {
            _client.Shutdown(SocketShutdown.Both);
            _client.Close();
            _client.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        OnCommandReceived = null;
        _client.Shutdown(SocketShutdown.Both);
        _client.Close();
        _client.Dispose();
    }
}