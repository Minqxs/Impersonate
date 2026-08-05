[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$stateDirectory = Join-Path $repositoryRoot '.artifacts\local'
foreach ($name in @('api.pid', 'worker.pid')) {
    $file = Join-Path $stateDirectory $name
    if (!(Test-Path -LiteralPath $file)) { continue }
    $processId = [int](Get-Content -LiteralPath $file -Raw)
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($process -and $process.Path -and [IO.Path]::GetFullPath($process.Path).StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase) -and $process.ProcessName -in @('Impersonate.Api', 'Impersonate.Worker')) {
        Stop-Process -Id $processId
        $process.WaitForExit(10000) | Out-Null
    }
    Remove-Item -LiteralPath $file -Force -ErrorAction SilentlyContinue
}
Write-Host 'Local Impersonate processes stopped.'
