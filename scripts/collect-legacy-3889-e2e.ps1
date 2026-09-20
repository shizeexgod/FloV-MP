[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$GtaDir,
    [Parameter(Mandatory = $true)]
    [string]$ClientDir,
    [string]$Connector,
    [string]$Server = '127.0.0.1:7788',
    [string]$OutDir,
    [ValidateRange(10, 600)]
    [int]$TimeoutSec = 60,
    [switch]$TwoClientSyncConfirmed,
    [switch]$KeepConnector
)

$ErrorActionPreference = 'Stop'

function Resolve-FullPath([string]$Path) {
    return [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $Path -ErrorAction Stop).Path)
}

function Read-Text([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return '' }
    return [IO.File]::ReadAllText($Path)
}

function Has-Pattern([string]$Text, [string[]]$Patterns) {
    foreach ($pattern in $Patterns) {
        if ($Text -match $pattern) { return $true }
    }
    return $false
}

function Quote-ProcessArgument([string]$Value) {
    return '"' + $Value + '"'
}

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$GtaDir = Resolve-FullPath $GtaDir
$ClientDir = Resolve-FullPath $ClientDir

if (-not $Connector) {
    $Connector = Join-Path $repoRoot 'launcher\native-dist\FloVMP.Connect.exe'
    if (-not (Test-Path -LiteralPath $Connector -PathType Leaf)) {
        $Connector = Join-Path $repoRoot 'launcher\src\FloVMP.Connect\bin\Release\net8.0-windows\FloVMP.Connect.exe'
    }
}
$Connector = Resolve-FullPath $Connector

if (-not $OutDir) {
    $stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmssZ')
    $OutDir = Join-Path $repoRoot "runtime\compat\legacy-3889\runs\$stamp"
}
$OutDir = [IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$verifyScript = Join-Path $scriptRoot 'verify-legacy-3889.ps1'
$preflightText = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -File $verifyScript -GtaDir $GtaDir 2>&1 | Out-String).Trim()
$preflightExit = $LASTEXITCODE
$preflight = $null
try { $preflight = $preflightText | ConvertFrom-Json } catch { }
[IO.File]::WriteAllText((Join-Path $OutDir 'preflight.txt'), $preflightText + [Environment]::NewLine, [Text.UTF8Encoding]::new($false))

$stdoutPath = Join-Path $OutDir 'connector.stdout.log'
$stderrPath = Join-Path $OutDir 'connector.stderr.log'
$arguments = @('-connect', (Quote-ProcessArgument $Server), '--client', (Quote-ProcessArgument $ClientDir),
    '--gta', (Quote-ProcessArgument $GtaDir), '--platform', 'egs', '--allow-unsupported', '--keep-open')
$connectorProcess = Start-Process -FilePath $Connector -ArgumentList $arguments -WorkingDirectory (Split-Path -Parent $Connector) `
    -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru

$started = [DateTime]::UtcNow
$gameSeen = $false
$connected = $false
$resourceLoaded = $false
$spawned = $false
do {
    Start-Sleep -Milliseconds 500
    $stdout = Read-Text $stdoutPath
    $stderr = Read-Text $stderrPath
    $combined = $stdout + "`n" + $stderr
    $gameSeen = $gameSeen -or @((Get-Process -Name GTA5 -ErrorAction SilentlyContinue)).Count -gt 0
    $connected = $connected -or (Has-Pattern $combined @('Connected', 'connection established', 'server connection'))
    $resourceLoaded = $resourceLoaded -or (Has-Pattern $combined @('flovmp-client', 'Loading resource'))
    $spawned = $spawned -or (Has-Pattern $combined @('player spawned', 'spawned', 'spawn'))
    $elapsed = ([DateTime]::UtcNow - $started).TotalSeconds
} while ($elapsed -lt $TimeoutSec -and -not $connectorProcess.HasExited)

if (-not $KeepConnector -and -not $connectorProcess.HasExited) {
    Stop-Process -Id $connectorProcess.Id -Force -ErrorAction SilentlyContinue
}

$report = [ordered]@{
    schema = 1
    generatedUtc = [DateTime]::UtcNow.ToString('o')
    profile = 'legacy-3889-epic'
    server = $Server
    gtaDir = $GtaDir
    clientDir = $ClientDir
    connector = $Connector
    preflightExitCode = $preflightExit
    preflight = $preflight
    connectorExitCode = if ($connectorProcess.HasExited) { $connectorProcess.ExitCode } else { $null }
    timeoutSec = $TimeoutSec
    e2eGate = [ordered]@{
        gameWindowAlive = $gameSeen
        clientConnected = $connected
        resourceLoaded = $resourceLoaded
        playerSpawned = $spawned
        twoClientSync = [bool]$TwoClientSyncConfirmed
    }
    nativeAdapter = [ordered]@{
        id = 'flovmp-legacy-native-3889'
        status = 'missing'
        sha256 = $null
        path = $null
    }
    supportStatus = 'needs-native-adapter'
    notes = @(
        'The runner always uses --allow-unsupported for diagnostics; this does not enable release support.',
        'twoClientSync is manual: pass -TwoClientSyncConfirmed only after two independent clients visibly synchronize.',
        'The report never changes GTA files and does not copy Steam/RGL files into an Epic installation.'
    )
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutDir 'machine-report.json') -Encoding UTF8
$report | ConvertTo-Json -Depth 8

if (-not $gameSeen) { exit 2 }
