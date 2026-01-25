using Microsoft.Extensions.Configuration;
using NBomber;
using NBomber.CSharp;
using Task = System.Threading.Tasks.Task;

namespace MicroServer.Bomber;

public static class Bomber
{
    public class CustomScenarioSettings
    {
        public required string Address { get; init; } = "127.0.0.1";

        public required int Port { get; init; } = 40567;

        public required int ClientCount { get; init; } = 10;
    }


    public static void Run()
    {
        Thread.Sleep(3000);

        var clientPool = new ClientPool<ClientInstance>();

        CustomScenarioSettings? settings = null;


        var scenario = Scenario.Create("microserver_client_pool", async ctx =>
                {

                    if (settings == null)
                        return Response.Fail();

                    var client =  clientPool.GetClient(ctx.ScenarioInfo.InstanceNumber);

                    //await client.ConnectAsync();
                    var result = await client.RunDataCommand(ctx.ScenarioCancellationToken);
                    //client.Close();

                    return !result ? Response.Fail() : Response.Ok();
                })
                .WithInit(async ctx =>
                {
                    settings = ctx.GlobalCustomSettings.Get<CustomScenarioSettings>();
                    if (settings == null)
                        throw new Exception("Failed to get global settings! Initialization failed.");

                    for (var i = 0; i < settings.ClientCount; i++)
                    {
                        var clientItem = new ClientInstance(settings.Address, settings.Port);
                        await clientItem.ConnectAsync();
                        await Task.Delay(10);

                        clientPool.AddClient(clientItem);
                    }
                    
                    //return Task.CompletedTask;

                })
            //прогрев и симуляция настроены в scenario-config.json
            //.WithLoadSimulations(
            //    Simulation.KeepConstant(copies: 10, during: TimeSpan.FromSeconds(30))
            //)
            .WithClean(ctx =>
            {
                var client = clientPool.GetClient(ctx.ScenarioInfo.InstanceNumber);
                client.Close();
                return Task.CompletedTask;
            })
            ;

        NBomberRunner
            .RegisterScenarios(scenario)
            .LoadConfig("scenario-config.json")
            .Run();
    }
}