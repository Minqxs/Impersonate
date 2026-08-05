[CmdletBinding()]
param([string]$TargetRepository = 'Minqxs/TaskIt')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
. (Join-Path $PSScriptRoot 'process-lifecycle.ps1')
$owned = @(Get-ImpersonateOwnedProcesses)
$api = ($owned | Where-Object Role -eq 'api' | Select-Object -First 1).Process
$worker = ($owned | Where-Object Role -eq 'worker' | Select-Object -First 1).Process
$preflight = $null
try { $preflight = Invoke-RestMethod -Uri "http://localhost:5001/api/development/preflight?targetRepository=$([Uri]::EscapeDataString($TargetRepository))" -TimeoutSec 5 } catch { }
[pscustomobject]@{ ApiRunning = $null -ne $api; ApiPid = $api.Id; WorkerRunning = $null -ne $worker; WorkerPid = $worker.Id; ApiHealth = if ($preflight) { 'Ready' } else { 'Unavailable' }; DatabaseConnectivity = [bool]$preflight.databaseConnected; GitHubMcpEnabled = [bool]$preflight.gitHubMcpEnabled; TargetRepositoryAllowlisted = [bool]$preflight.targetRepositoryAllowed; TokenAvailable = -not [string]::IsNullOrWhiteSpace($env:GITHUB_MCP_TOKEN) } | Format-List
