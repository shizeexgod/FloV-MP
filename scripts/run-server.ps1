<#
.SYNOPSIS
    Runs the assembled FloV:MP server from runtime\server.

.DESCRIPTION
    The working directory MUST be the server folder: altv-server.exe resolves
    server.toml, modules\, data\, resources\ relative to CWD.

    NOTE: keep this file ASCII-only (Windows PowerShell 5.1 codepage parsing).

.PARAMETER Runtime
    Server folder. Default: <repo>\runtime\server.

.PARAMETER TimeoutSeconds
    If > 0, stop the server after N seconds (headless boot test with no live
    client). 0 = run until Ctrl+C.

.EXAMPLE
    powershell -File scripts/run-server.ps1
    powershell -File scripts/run-server.ps1 -TimeoutSeconds 15
#>
[CmdletBinding()]
param(
    [string]$Runtime     = "",
    [int]$TimeoutSeconds = 0
)

$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $Runtime) { $Runtime = Join-Path (Split-Path $scriptDir -Parent) "runtime\server" }
$exe = Join-Path $Runtime "altv-server.exe"
if (-not (Test-Path $exe)) {
    throw "Server not assembled: $exe not found. Run scripts/assemble-runtime.ps1 first."
}

Push-Location $Runtime
try {
    if ($TimeoutSeconds -gt 0) {
        Write-Host "== Starting server for $TimeoutSeconds s (boot test) ==" -ForegroundColor Cyan
        $p = Start-Process -FilePath $exe -PassThru -NoNewWindow
        Start-Sleep -Seconds $TimeoutSeconds
        if (-not $p.HasExited) {
            Write-Host "== Timeout reached, stopping server ==" -ForegroundColor Cyan
            $p.CloseMainWindow() | Out-Null
            Start-Sleep -Seconds 2
            if (-not $p.HasExited) { $p.Kill() }
        }
        Write-Host "exit code: $($p.ExitCode)"
    }
    else {
        Write-Host "== Starting server (Ctrl+C to stop) ==" -ForegroundColor Cyan
        & $exe
    }
}
finally {
    Pop-Location
}
