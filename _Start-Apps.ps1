function Start-Apps
{
    param(
        [string]$address,
        [int]$port,
        [int]$timeout,
        [int]$clientsToRun,
        [bool]$startServer,
        [bool]$startClients,
        [string]$verbose
    )

    if ($startServer)
    {
        Start-Process -FilePath .\MicroServer\bin\Debug\net9.0\MicroServer.exe -ArgumentList "--address=$address","--port=$port","--otel=http://localhost:4317","--mcc=30"
    }

    if ($startClients)
    {
        for ($i = 1; $i -le $clientsToRun; $i++) {

            Start-Process -FilePath .\MicroServer.TestClient\bin\Debug\net9.0\MicroServer.TestClient.exe `
                -ArgumentList "--timeout=$timeout","--address=$address","--port=$port","--title=""Client #$i""", "$verbose"

        }
    }
}