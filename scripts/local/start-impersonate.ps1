[CmdletBinding()]
param([string]$TargetRepository = 'Minqxs/TaskIt', [switch]$SkipBuild, [switch]$SkipMigrations, [switch]$NoBrowser)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$stateDirectory = Join-Path $repositoryRoot '.artifacts\local'
. (Join-Path $PSScriptRoot 'process-lifecycle.ps1')
if ([string]::IsNullOrWhiteSpace($env:GITHUB_MCP_TOKEN)) { Write-Host 'Set GITHUB_MCP_TOKEN in this PowerShell session and rerun the script.'; exit 1 }
foreach ($command in @('dotnet', 'git', 'node', 'npm')) { if (!(Get-Command $command -ErrorAction SilentlyContinue)) { throw "Required command is unavailable: $command" } }
$settingsPath = Join-Path $repositoryRoot 'src\backend\Impersonate.Worker\appsettings.Development.json'
$settings = Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json
$allowed = @($settings.Delivery.GitHubMcp.AllowedRepositories | ForEach-Object { $_.Trim().TrimEnd('/').ToLowerInvariant() })
if (!$allowed.Contains($TargetRepository.Trim().TrimEnd('/').ToLowerInvariant())) { throw 'github_mcp_repository_not_allowed' }
& (Join-Path $PSScriptRoot 'stop-impersonate.ps1')
Assert-NoImpersonateLocalProcesses
New-Item -ItemType Directory -Path $stateDirectory -Force | Out-Null
$env:DOTNET_ENVIRONMENT = 'Development'; $env:ASPNETCORE_ENVIRONMENT = 'Development'; $env:ASPNETCORE_URLS = 'http://localhost:5001'
Push-Location $repositoryRoot
try {
    if (!$SkipBuild) { & dotnet restore '.\Impersonate.sln'; if ($LASTEXITCODE) { throw 'Restore failed.' }; & dotnet build '.\Impersonate.sln' '--no-restore'; if ($LASTEXITCODE) { throw 'Build failed.' } }
    if (!$SkipMigrations) { & dotnet ef database update --project '.\src\backend\Impersonate.Infrastructure' --startup-project '.\src\backend\Impersonate.Api'; if ($LASTEXITCODE) { throw 'Migration failed.' } }
    $apiOut = Join-Path $stateDirectory 'api.out.log'; $apiErr = Join-Path $stateDirectory 'api.err.log'
    $api = Start-Process -FilePath (Join-Path $repositoryRoot 'src\backend\Impersonate.Api\bin\Debug\net10.0\Impersonate.Api.exe') -WorkingDirectory (Join-Path $repositoryRoot 'src\backend\Impersonate.Api\bin\Debug\net10.0') -WindowStyle Hidden -RedirectStandardOutput $apiOut -RedirectStandardError $apiErr -PassThru
    Save-ImpersonateProcessMetadata 'api' $api
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) { Start-Sleep -Milliseconds 500; try { $ready = (Invoke-WebRequest 'http://localhost:5001/health' -TimeoutSec 2).StatusCode -eq 200 } catch { }; if ($ready) { break }; if ($api.HasExited) { throw 'API exited during startup.' } }
    if (!$ready) { throw 'API readiness timed out.' }
    $workerOut = Join-Path $stateDirectory 'worker.out.log'; $workerErr = Join-Path $stateDirectory 'worker.err.log'
    $worker = Start-Process -FilePath (Join-Path $repositoryRoot 'src\backend\Impersonate.Worker\bin\Debug\net10.0\Impersonate.Worker.exe') -WorkingDirectory (Join-Path $repositoryRoot 'src\backend\Impersonate.Worker\bin\Debug\net10.0') -WindowStyle Hidden -RedirectStandardOutput $workerOut -RedirectStandardError $workerErr -PassThru
    Save-ImpersonateProcessMetadata 'worker' $worker
    Start-Sleep -Seconds 2
    if ($worker.HasExited) { throw 'Worker exited during startup.' }
    $preflight = Invoke-RestMethod -Uri "http://localhost:5001/api/development/preflight?targetRepository=$([Uri]::EscapeDataString($TargetRepository))" -TimeoutSec 10
    if (!$preflight.databaseConnected -or !$preflight.migrationsCurrent -or !$preflight.dataProtectionWritable -or !$preflight.gitAvailable -or !$preflight.gitHubMcpEnabled -or !$preflight.targetRepositoryAllowed -or !$preflight.tokenAvailable -or !$preflight.officialServerConfigured -or !$preflight.requiredToolsConfigured) { throw 'Local development preflight failed.' }
    Write-Host "API PID: $($api.Id) - http://localhost:5001"; Write-Host "Worker PID: $($worker.Id)"; Write-Host "GitHub MCP: enabled; transport=$($settings.Delivery.GitHubMcp.Transport); server=$($settings.Delivery.GitHubMcp.ServerId); repository=$TargetRepository; token available=true"
    if (!$NoBrowser) { Start-Process 'http://localhost:5173' }
} catch { & (Join-Path $PSScriptRoot 'stop-impersonate.ps1'); throw } finally { Pop-Location }
