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

# --- Start FloV:MP Auth Verification Proxy (7799) -----------------------
# altv-server.exe sends license verification requests to 127.0.0.1:7799
# Proxy validates all player tokens without connecting to dead altv backend.
$authProxy = [System.Net.HttpListener]::new()
$authProxy.Prefixes.Add("http://127.0.0.1:7799/")
$authCts = [System.Threading.CancellationTokenSource]::new()
try {
    $authProxy.Start()
    Write-Host "[FloV:MP] Auth proxy listening on http://127.0.0.1:7799" -ForegroundColor Green
    [System.Threading.Tasks.Task]::Run([Action]{
        while ($authProxy.IsListening -and -not $authCts.IsCancellationRequested) {
            try {
                $ctx = $authProxy.GetContext()
                $reader = [System.IO.StreamReader]::new($ctx.Request.InputStream)
                $body = $reader.ReadToEnd()
                $count = 1
                if ($body -match 'clientTokenHashes') {
                    $m = [regex]::Match($body, '"clientTokenHashes"\s*:\s*\[(.*?)\]')
                    if ($m.Success -and -not [string]::IsNullOrWhiteSpace($m.Groups[1].Value)) {
                        $trimmed = $m.Groups[1].Value.Trim()
                        if ($trimmed.Length -gt 0) {
                            $count = ($trimmed -split ',').Count
                        } else {
                            $count = 0
                        }
                    }
                }
                $arr = if ($count -gt 0) { (1..$count | ForEach-Object { "true" }) -join ", " } else { "" }
                $resp = "[$arr]"
                $bytes = [System.Text.Encoding]::UTF8.GetBytes($resp)
                $ctx.Response.ContentType = "application/json"
                $ctx.Response.StatusCode = 200
                $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
                $ctx.Response.Close()
            } catch {}
        }
    })
} catch {
    Write-Host "[FloV:MP] Auth proxy 7799 already active or handled: $($_.Exception.Message)" -ForegroundColor DarkGray
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
    $authCts.Cancel()
    try { $authProxy.Stop(); $authProxy.Close() } catch {}
    Pop-Location
}
