param([switch]$AcceptMicrosoftEula)
$ErrorActionPreference='Stop'
if(-not $AcceptMicrosoftEula){throw 'Local Microsoft SQL Server and Service Bus emulator require explicit EULA acceptance. Review README and rerun with -AcceptMicrosoftEula.'}
$solution=Split-Path $PSScriptRoot
$project='vairas-learning-broker-'+([Guid]::NewGuid().ToString('N').Substring(0,10))
$compose=Join-Path $solution 'tests/servicebus/docker-compose.yaml'
$env:ACCEPT_EULA='Y'
$env:MSSQL_SA_PASSWORD='Synthetic!'+[Guid]::NewGuid().ToString('N')
try {
    dotnet restore (Join-Path $solution 'Vairas.LearningTracking.slnx')
    if($LASTEXITCODE -ne 0){throw 'Broker test dependency restore failed.'}
    docker compose -p $project -f $compose up -d
    if($LASTEXITCODE -ne 0){throw 'Broker emulator startup failed.'}
    $ready=$false
    for($attempt=0;$attempt -lt 45;$attempt++){
        try{$response=Invoke-WebRequest 'http://localhost:15300/health' -UseBasicParsing -TimeoutSec 3;if($response.StatusCode -eq 200){$ready=$true;break}}catch{}
        Start-Sleep -Seconds 2
    }
    if(-not $ready){throw 'Broker emulator did not become healthy.'}
    $env:VAIRAS_TEST_SERVICEBUS_CONNECTION='Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;'
    dotnet test (Join-Path $solution 'Vairas.LearningTracking.Tests') -c Release --no-restore --filter 'TestCategory=Broker' --logger 'trx;LogFileName=broker.trx' --results-directory (Join-Path $solution 'evidence/broker')
    if($LASTEXITCODE -ne 0){throw 'Broker tests failed.'}
} finally {
    docker compose -p $project -f $compose down -v
    Remove-Item Env:MSSQL_SA_PASSWORD,Env:VAIRAS_TEST_SERVICEBUS_CONNECTION,Env:ACCEPT_EULA -ErrorAction SilentlyContinue
}
