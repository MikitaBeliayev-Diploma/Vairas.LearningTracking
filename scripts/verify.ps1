param([switch]$SkipIntegration,[switch]$IncludeBroker,[switch]$AcceptMicrosoftEula)
$ErrorActionPreference='Stop'
$solution=Split-Path $PSScriptRoot
Push-Location $solution
try {
    $run=Join-Path $solution ('evidence/run-'+(Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Force -Path $run | Out-Null
    dotnet restore Vairas.LearningTracking.slnx
    if($LASTEXITCODE -ne 0){throw 'Restore or NuGet audit failed.'}
    dotnet build Vairas.LearningTracking.slnx -c Release --no-restore
    if($LASTEXITCODE -ne 0){throw 'Build failed.'}
    dotnet tool restore
    if($LASTEXITCODE -ne 0){throw 'EF tool restore failed.'}
    dotnet ef migrations has-pending-model-changes --project Vairas.LearningTracking.Persistance --startup-project Vairas.LearningTracking.Persistance --configuration Release --no-build
    if($LASTEXITCODE -ne 0){throw 'EF model and snapshot differ.'}
    dotnet test Vairas.LearningTracking.Tests -c Release --no-build --no-restore --filter 'TestCategory=Unit' --settings coverage.runsettings --collect:'XPlat Code Coverage' --logger 'trx;LogFileName=unit.trx' --results-directory (Join-Path $run 'unit')
    if($LASTEXITCODE -ne 0){throw 'Unit tests failed.'}
    $coverage=Get-ChildItem (Join-Path $run 'unit') -Filter coverage.cobertura.xml -Recurse | Select-Object -First 1
    if(-not $coverage){throw 'Fresh unit coverage report is missing.'}
    [xml]$report=Get-Content -LiteralPath $coverage.FullName
    $metrics=@{}
    foreach($name in @('Vairas.LearningTracking.Business','Vairas.LearningTracking.Domain')){
        $package=$report.coverage.packages.package | Where-Object {$_.name -eq $name}
        if(-not $package){throw "Coverage package missing: $name"}
        $percent=[Math]::Round(100*[double]$package.'line-rate',2)
        $metrics[$name]=$percent
        if($percent -lt 70){throw "Unit coverage below 70% for ${name}: $percent"}
    }
    $integration='NOT RUN'
    if(-not $SkipIntegration){
        dotnet test Vairas.LearningTracking.Tests -c Release --no-build --no-restore --filter 'TestCategory=Integration' --logger 'trx;LogFileName=integration.trx' --results-directory (Join-Path $run 'integration')
        if($LASTEXITCODE -ne 0){throw 'Integration tests failed.'}
        $integration='PASSED'
    }
    $audit=dotnet list Vairas.LearningTracking.slnx package --vulnerable --include-transitive --format json
    if($LASTEXITCODE -ne 0){throw 'Dependency vulnerability query failed.'}
    $audit | Set-Content -Encoding UTF8 (Join-Path $run 'dependency-audit.json')
    $data=($audit -join "`n") | ConvertFrom-Json
    foreach($project in $data.projects){foreach($framework in $project.frameworks){foreach($package in @($framework.topLevelPackages)+@($framework.transitivePackages)){if($package.vulnerabilities){throw 'A vulnerable dependency was reported.'}}}}
    $manifest=Get-Content source-manifest.json -Raw | ConvertFrom-Json
    foreach($file in $manifest.files){if((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $solution $file.path)).Hash.ToLowerInvariant() -ne $file.sha256){throw "Frozen source changed: $($file.path)"}}
    $broker='NOT RUN'
    if($IncludeBroker){& (Join-Path $PSScriptRoot 'test-broker.ps1') -AcceptMicrosoftEula:$AcceptMicrosoftEula;$broker='PASSED'}
    $hasServices=Test-Path (Join-Path $solution '../Vairas.SchoolManagement/Vairas.SchoolManagement.Tests/Vairas.SchoolManagement.Tests.csproj')
    @{unitCoverage=$metrics;integration=$integration;broker=$broker;siblingServiceSources=$hasServices;verifiedAt=(Get-Date).ToUniversalTime().ToString('o')} | ConvertTo-Json -Depth 6 | Set-Content -Encoding UTF8 (Join-Path $run 'verification-summary.json')
    $rows=($metrics.GetEnumerator() | Sort-Object Name | ForEach-Object {"<tr><td>$($_.Key)</td><td>$($_.Value)%</td></tr>"}) -join ''
    "<!doctype html><html lang='en'><meta charset='utf-8'><title>Learning unit coverage</title><h1>Unit-only coverage</h1><table><tr><th>Assembly</th><th>Line coverage</th></tr>$rows</table><p>Business and Domain each require 70%. DTO-only contracts and automatic properties are excluded; rule and handler logic remains measured.</p>" | Set-Content -Encoding UTF8 (Join-Path $run 'coverage.html')
    Write-Output "Verification passed. Evidence: $run"
} finally {Pop-Location}
