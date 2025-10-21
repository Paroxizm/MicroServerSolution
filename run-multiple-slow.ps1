. (".\_Elevate.ps1")
. (".\_Start-Apps.ps1")

$port = 40567
$timeout = 3000
$address = "127.0.0.1"
$clientsToRun = 5
$verbose = "--verbose"

$startServer = $true
$startClients = $true

Start-Apps $address $port $timeout $clientsToRun $startServer $startClients $verbose