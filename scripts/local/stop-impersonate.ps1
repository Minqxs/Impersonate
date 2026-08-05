[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'process-lifecycle.ps1')
Stop-AllImpersonateLocalProcesses
Assert-NoImpersonateLocalProcesses
foreach ($legacy in @('api.pid', 'worker.pid')) { Remove-Item -LiteralPath (Join-Path (Get-ImpersonateStateDirectory) $legacy) -Force -ErrorAction SilentlyContinue }
Write-Host 'Local Impersonate processes stopped.'
