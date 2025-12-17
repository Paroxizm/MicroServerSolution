using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MicroServer;

public static class Telemetry
{
    private static readonly ActivitySource Source = new("MicroServer");
    private static readonly Meter Meter = new("MicroServer.Metrics", "1.0.0");

    private static readonly Counter<long> CommandsProcessedCounter = 
        Meter.CreateCounter<long>("commands.processed", "commands", "Number of processed commands");

    private static readonly Counter<long> AcceptedClientsCounter =
        Meter.CreateCounter<long>("clients.accepted", "connections", "Number os accepted connections");

    private static readonly Gauge<long> ActiveConnectionsCounter = Meter.CreateGauge<long>(
        name: "clients.active",
        unit: "connections",
        description: "Number of active connections");

    private static readonly Histogram<double> DurationHistogram = Meter.CreateHistogram<double>("duration", "mcs", "Command duration in microseconds");

    private static TracerProvider? _tracerProvider;
    private static MeterProvider? _meterProvider;

    public static void Start(string serverUrl, string serviceName)
    {
        var resourceBuilder = ResourceBuilder.CreateDefault()
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: "1.0.0",
                    serviceInstanceId: "core");
        _tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MicroServer")
            .SetResourceBuilder(resourceBuilder)
            .AddConsoleExporter()
            .AddOtlpExporter(options => { options.Endpoint = new Uri(serverUrl); })
            .Build();


        _meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetResourceBuilder(resourceBuilder)
            .AddMeter("MicroServer.Metrics")
            .AddConsoleExporter()
            .AddOtlpExporter(otlp => { otlp.Endpoint = new Uri(serverUrl); })
            .Build();
    }

    public static void Stop()
    {
        _tracerProvider?.Dispose();
        _meterProvider?.Dispose();
    }

    public static Activity? StartActivity(string activityName)
    {
        return Source.StartActivity(activityName);
    }

    public static void CommandProcessed()
    {
        CommandsProcessedCounter.Add(1);
    }

    public static void ClientsAccepted()
    {
        AcceptedClientsCounter.Add(1);
    }

    public static void AddCommandDuration(double ms)
    {
        DurationHistogram.Record(ms);
    }

    private static long _currentConnections;

    public static void ClientConnected()
    {
        Interlocked.Increment(ref _currentConnections);
        ActiveConnectionsCounter.Record(_currentConnections);
    }

    public static void ClientDisconnected()
    {
        Interlocked.Decrement(ref _currentConnections);
        ActiveConnectionsCounter.Record(_currentConnections);
    }
    
    public static void ClientDisconnectedByServer()
    {
        //Interlocked.Decrement(ref _currentConnections);
        //ActiveConnectionsCounter.Record(_currentConnections);
    }
}