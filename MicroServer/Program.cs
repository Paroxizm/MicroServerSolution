// See https://aka.ms/new-console-template for more information

using System.Threading.Channels;
using MicroServer;
using Serilog;

var address = args.FirstOrDefault(x => x.StartsWith("--address"))?.Split('=')[1] ?? "127.0.0.1";
var port = int.Parse(args.FirstOrDefault(x => x.StartsWith("--port"))?.Split('=')[1] ?? "40567");

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();

Log.Information("Server starting with:");
Log.Information(" - address: [{address}]", address);
Log.Information(" - port: [{port}]", port);

var tokenSource = new CancellationTokenSource();

var storage = new SimpleStore();
var channel = Channel.CreateUnbounded<CommandDto>();

var storageClients = new List<StorageClient>{
        new (storage, channel),
        new (storage, channel),
        new (storage, channel),
        new (storage, channel),
        new (storage, channel)
    };

Parallel.ForEach(storageClients, client => client.Start(tokenSource.Token));

try
{
    var listener = new TcpServer(channel.Writer, address, port);
    
    await listener.StartAsync(tokenSource.Token);

    Log.Information("MicroServer is started");

    while (!tokenSource.IsCancellationRequested)
    {
        await Task.Delay(1000, tokenSource.Token);
    }
}
catch (Exception ex)
{
    Log.Error(ex, "An error occured: {msg}", ex.Message);
}
finally
{
    Log.Information("MicroServer is stopped");
    Log.CloseAndFlush();
}
