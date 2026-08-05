$script:ImpersonateRoles = @{
    api = @{ Name = 'Impersonate.Api'; RelativePath = 'src\backend\Impersonate.Api\bin\Debug\net10.0\Impersonate.Api.exe' }
    worker = @{ Name = 'Impersonate.Worker'; RelativePath = 'src\backend\Impersonate.Worker\bin\Debug\net10.0\Impersonate.Worker.exe' }
}

function Get-ImpersonateRepositoryRoot { [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..')) }
function Get-ImpersonateStateDirectory { Join-Path (Get-ImpersonateRepositoryRoot) '.artifacts\local' }
function Get-ImpersonateExpectedPath([string]$Role) { [IO.Path]::GetFullPath((Join-Path (Get-ImpersonateRepositoryRoot) $script:ImpersonateRoles[$Role].RelativePath)) }
function Get-ImpersonateStatePath([string]$Role) { Join-Path (Get-ImpersonateStateDirectory) "$Role.process.json" }

function Get-ImpersonateOwnedProcesses {
    $root = Get-ImpersonateRepositoryRoot
    $result = @()
    foreach ($role in $script:ImpersonateRoles.Keys) {
        $expected = Get-ImpersonateExpectedPath $role
        $name = $script:ImpersonateRoles[$role].Name
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            if ($process.Path -and [IO.Path]::GetFullPath($process.Path).Equals($expected, [StringComparison]::OrdinalIgnoreCase)) {
                $result += [pscustomobject]@{ Role = $role; Process = $process; ExpectedPath = $expected; RepositoryRoot = $root }
            }
        }
    }
    return $result
}

function Save-ImpersonateProcessMetadata([string]$Role, [System.Diagnostics.Process]$Process, [string]$Launcher = 'terminal-bootstrap') {
    $directory = Get-ImpersonateStateDirectory
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    [pscustomobject]@{ Version = 1; Role = $Role; Pid = $Process.Id; StartTimeUtc = $Process.StartTime.ToUniversalTime().ToString('O'); ExecutablePath = Get-ImpersonateExpectedPath $Role; RepositoryRoot = Get-ImpersonateRepositoryRoot; Launcher = $Launcher } |
        ConvertTo-Json | Set-Content -LiteralPath (Get-ImpersonateStatePath $Role) -Encoding UTF8
}

function Test-ImpersonateMetadata([string]$Role, $Metadata, [System.Diagnostics.Process]$Process) {
    if (!$Metadata -or $Metadata.Role -ne $Role -or $Metadata.Launcher -ne 'terminal-bootstrap') { return $false }
    if (![IO.Path]::GetFullPath([string]$Metadata.RepositoryRoot).Equals((Get-ImpersonateRepositoryRoot), [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if (![IO.Path]::GetFullPath([string]$Metadata.ExecutablePath).Equals((Get-ImpersonateExpectedPath $Role), [StringComparison]::OrdinalIgnoreCase)) { return $false }
    if (!$Process.Path -or ![IO.Path]::GetFullPath($Process.Path).Equals((Get-ImpersonateExpectedPath $Role), [StringComparison]::OrdinalIgnoreCase)) { return $false }
    return [Math]::Abs(($Process.StartTime.ToUniversalTime() - [DateTime]::Parse([string]$Metadata.StartTimeUtc).ToUniversalTime()).TotalSeconds) -lt 1
}

function Test-ImpersonateExecutableUnlocked([string]$Path) {
    if (!(Test-Path -LiteralPath $Path)) { return $true }
    try { $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None); $stream.Dispose(); return $true } catch { return $false }
}

function Stop-ImpersonateOwnedProcess($Owned, [int]$GracefulTimeoutSeconds = 5, [int]$ForceTimeoutSeconds = 5) {
    $process = $Owned.Process
    if ($process.HasExited) { return }
    $null = $process.CloseMainWindow()
    if (!$process.WaitForExit($GracefulTimeoutSeconds * 1000)) {
        Stop-Process -Id $process.Id -ErrorAction Stop
        if (!$process.WaitForExit($GracefulTimeoutSeconds * 1000)) {
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
            if (!$process.WaitForExit($ForceTimeoutSeconds * 1000)) { throw "Failed to stop $($Owned.Role) PID $($process.Id)." }
        }
    }
    if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) { throw "Failed to verify shutdown for $($Owned.Role) PID $($process.Id)." }
    if (!(Test-ImpersonateExecutableUnlocked $Owned.ExpectedPath)) { throw "Executable remains locked for $($Owned.Role) PID $($process.Id)." }
}

function Stop-AllImpersonateLocalProcesses {
    $failures = @()
    $handled = @{}
    $protected = @{}
    foreach ($role in $script:ImpersonateRoles.Keys) {
        $statePath = Get-ImpersonateStatePath $role
        if (!(Test-Path -LiteralPath $statePath)) { continue }
        try {
            $metadata = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
            $process = Get-Process -Id ([int]$metadata.Pid) -ErrorAction SilentlyContinue
            if ($process) {
                if (!(Test-ImpersonateMetadata $role $metadata $process)) { $protected[$process.Id] = $true; throw "Stored identity for $role PID $($metadata.Pid) cannot be proven; state retained." }
                $owned = [pscustomobject]@{ Role = $role; Process = $process; ExpectedPath = Get-ImpersonateExpectedPath $role }
                Stop-ImpersonateOwnedProcess $owned
                $handled[$process.Id] = $true
            }
            Remove-Item -LiteralPath $statePath -Force
        } catch { $failures += $_.Exception.Message }
    }
    foreach ($owned in @(Get-ImpersonateOwnedProcesses)) {
        if ($handled.ContainsKey($owned.Process.Id) -or $protected.ContainsKey($owned.Process.Id)) { continue }
        Write-Host "Recovering repository-owned orphan: $($owned.Role) PID $($owned.Process.Id)."
        try { Stop-ImpersonateOwnedProcess $owned } catch { $failures += $_.Exception.Message }
    }
    if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
}

function Assert-NoImpersonateLocalProcesses {
    $remaining = @(Get-ImpersonateOwnedProcesses)
    if ($remaining.Count) { throw "Repository-owned Impersonate processes remain: $($remaining.Process.Id -join ', ')." }
    foreach ($role in $script:ImpersonateRoles.Keys) { if (!(Test-ImpersonateExecutableUnlocked (Get-ImpersonateExpectedPath $role))) { throw "Build-lock preflight failed for $role." } }
}
