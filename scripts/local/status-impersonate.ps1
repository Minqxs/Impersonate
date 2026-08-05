[CmdletBinding()]
param([string]$TargetRepository = 'Minqxs/TaskIt')
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$stateDirectory = Join-Path $repositoryRoot '.artifacts\local'
function Get-ManagedProcess([string]$fileName, [string]$expectedName) {
    $file = Join-Path $stateDirectory $fileName
    if (!(Test-Path -LiteralPath $file)) { return $null }
    $candidate = Get-Process -Id ([int](Get-Content -LiteralPath $file -Raw)) -ErrorAction SilentlyContinue
    if ($candidate -and $candidate.ProcessName -eq $expectedName -and $candidate.Path -and [IO.Path]::GetFullPath($candidate.Path).StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) { return $candidate }
    return $null
}
$api = Get-ManagedProcess 'api.pid' 'Impersonate.Api'
$worker = Get-ManagedProcess 'worker.pid' 'Impersonate.Worker'
$preflight = $null
try { $preflight = Invoke-RestMethod -Uri "http://localhost:5001/api/development/preflight?targetRepository=$([Uri]::EscapeDataString($TargetRepository))" -TimeoutSec 5 } catch { }
[pscustomobject]@{ ApiRunning = $null -ne $api; ApiPid = $api.Id; WorkerRunning = $null -ne $worker; WorkerPid = $worker.Id; ApiHealth = if ($preflight) { 'Ready' } else { 'Unavailable' }; DatabaseConnectivity = [bool]$preflight.databaseConnected; GitHubMcpEnabled = [bool]$preflight.gitHubMcpEnabled; TargetRepositoryAllowlisted = [bool]$preflight.targetRepositoryAllowed; TokenAvailable = -not [string]::IsNullOrWhiteSpace($env:GITHUB_MCP_TOKEN) } | Format-List
