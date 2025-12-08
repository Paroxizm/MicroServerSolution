using Microsoft.Extensions.Configuration;
using NBomber;
using NBomber.CSharp;

namespace MicroServer.Bomber;

public class Bomber
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

        var scenario = Scenario.Create("microserver_client_pool", async ctx =>
            {
                var client = clientPool.GetClient(ctx.ScenarioInfo.InstanceNumber);

                var result = await client.RunDataCommand();
                return !result ? Response.Fail() : Response.Ok();
            })
            .WithInit(async ctx =>
            {
                var config = ctx.GlobalCustomSettings.Get<CustomScenarioSettings>();
                if (config == null)
                    throw new Exception("Failed to get global settings! Initialization failed.");

                for (var i = 0; i < config.ClientCount; i++)
                {
                    var clientItem = new ClientInstance(config.Address, config.Port);
                    await clientItem.ConnectAsync();
                    await Task.Delay(10);

                    clientPool.AddClient(clientItem);
                }
            })
            //прогрев и симуляция настроены в scenario-config.json
            .WithClean(_ =>
            {
                clientPool.DisposeClients(client => client.Close());
                return Task.CompletedTask;
            });

        NBomberRunner
            .RegisterScenarios(scenario)
            .LoadConfig("scenario-config.json")
            .Run();
    }
}