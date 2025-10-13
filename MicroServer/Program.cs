// See https://aka.ms/new-console-template for more information

using MicroServer;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.File("log.txt", rollingInterval: RollingInterval.Day)
    .WriteTo.Console()
    .CreateLogger();


Log.Information("MicroServer is starting.");
try
{
    var listener = new TcpServer();
    await listener.StartAsync();
    
    while (true)
    {
        await Task.Delay(1000);
    }

}
finally
{
    Log.Information("MicroServer is stopped");
    Log.CloseAndFlush();
}
