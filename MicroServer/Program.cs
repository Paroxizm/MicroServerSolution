using System.Text;
using System.Threading.Channels;
using MicroServer;
using MicroServer.Model;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

Console.WriteLine(args.Aggregate("", (c,n) => c + n + Environment.NewLine));

// адрес сервера 
var address = args.FirstOrDefault(x => x.StartsWith("--address"))?.Split('=')[1] ?? "127.0.0.1";
// порт, на котором слушает сервер
var port = int.Parse(args.FirstOrDefault(x => x.StartsWith("--port"))?.Split('=')[1] ?? "40567");
// количество параллельных писателей в хранилище 
var storageClientsCount = int.Parse(args.FirstOrDefault(x => x.StartsWith("--storage-clients"))?.Split('=')[1] ?? "5");

// максимальное количество одновременных подключений
var maxConcurrentConnections = int.Parse(args.FirstOrDefault(x => x.StartsWith("--mcc"))?.Split('=')[1] ?? "30");
// максимальное количество одновременных подключений
var maxCommandSize = int.Parse(args.FirstOrDefault(x => x.StartsWith("--mcs"))?.Split('=')[1] ?? "4096");

// максимальное количество одновременных подключений
var otelServer = args.FirstOrDefault(x => x.StartsWith("--otel"))?.Split('=')[1] ?? "http://localhost:4317";

var useOtel = !string.IsNullOrEmpty(otelServer) && otelServer.ToLowerInvariant() is not "false" and not "no";

Console.Title = $"MicroServer at [{address}:{port}]";

if(useOtel)
    Telemetry.Start(otelServer, "micro-server");

var loggerConf = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Debug();

if(useOtel)
    loggerConf
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = otelServer;
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "micro-server",
            ["service.instance.id"] = "core",
            ["service.version"] = "1.0.0"
        };
    });
    

Log.Logger = loggerConf.CreateLogger();

Log.Information("Server starting with:");
Log.Information(" - address: [{address}]", address);
Log.Information(" - port: [{port}]", port);
Log.Information(" - mcc: [{mcc}]", maxConcurrentConnections);
Log.Information(" - mcs: [{mcs}]", maxCommandSize);
Log.Information(" - use telemetry: [{useOtel}]", useOtel);
Log.Information(" - otel server: [{otelServer}]", otelServer);

var tokenSource = new CancellationTokenSource();

var storage = new SimpleStore();
var channel = Channel.CreateUnbounded<CommandDto>();

var storageClients =
    Enumerable.Range(0, storageClientsCount)
        .Select(_ => new StorageClient(storage, channel))
        .ToList();

Parallel.ForEach(storageClients, client =>
{
    client.Start(tokenSource.Token);
    Telemetry.CommandProcessed();
});

try
{
    var listener = new TcpServer(channel.Writer, address, port, maxConcurrentConnections, maxCommandSize);

    _ = listener.StartAsync(tokenSource.Token);

    Log.Information("MicroServer started");
    
    while (!tokenSource.IsCancellationRequested)
    {
        if (Console.KeyAvailable
            && Console.ReadKey(true).Key == ConsoleKey.Q)
            break;

        try
        {
            listener.Cleanup();
            //if (!useOtel)
            //{ 
                Console.Clear();
                Console.SetCursorPosition(0, 0);
                Console.WriteLine(FormatStatus(listener, address, port, storageClients, maxConcurrentConnections, storage));
            //}

            await Task.Delay(5000, tokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception e)
        {
            Log.Error(e, "Error: {msg}", e.Message);
            break;
        }
    }

    channel.Writer.Complete();
    if (!tokenSource.IsCancellationRequested)
        tokenSource.Cancel();
}
catch (OperationCanceledException)
{
    //
}
catch (Exception ex)
{
    Log.Error(ex, "An error occured: {msg}", ex.Message);
}
finally
{
    Log.Information("MicroServer is stopped");
    Log.CloseAndFlush();
    Telemetry.Stop();
}

return 0;


// форматирование текущего состояния сервера в строку
static string FormatStatus(
    TcpServer tcpServer, string address, int port,
    List<StorageClient> storageClients, int mcc,
    SimpleStore storage)
{
    var result = new StringBuilder();
    result.AppendLine("-".PadRight(80, '-'));

    var connections = tcpServer.GetSnapshot();

    // вывод статистики соединений
    result.AppendLine($"Listening at [{address}:{port}]".PadRight(80));
    result.AppendLine($"Got commands: {ClientSocketHandler.CommandsReceived}".PadRight(80));
    result.AppendLine($"Actual handlers: [{connections.Count}/{mcc}]".PadRight(80));
    result.AppendLine($"Storage clients: [{storageClients.Count}]".PadRight(80));
    foreach (var client in storageClients)
    {
        result.AppendLine(
            $" > read: {client.ReadCommands:0000}, " +
            $"good: {client.GoodCommands:00000}, " +
            $"fail: {client.FailCommands:00000}".PadRight(80));
    }

    var stat = storage.GetStatistic();
    result.AppendLine(
        $"Storage status: operations={stat.get + stat.set + stat.delete} get={stat.get}; set={stat.set}; delete={stat.delete}"
            .PadRight(80));

    foreach (var handler in connections)
    {
        result.AppendLine(
            $" > {(handler.IsAlive ? "[ ]" : "[x]")} [{handler.ClientName}] " +
            $"reads: {handler.ReadsCount:0000}, " +
            $"commands: {handler.CommandsCount:0000}, " +
            $"received: {handler.ReadTotal:### ### ##0} bytes".PadRight(80));
    }

    result.AppendLine("-".PadRight(80, '-'));

    return result.ToString();
}