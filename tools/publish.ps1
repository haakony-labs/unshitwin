# Publishes a Release build to a fixed per-user folder (default: %LOCALAPPDATA%\Programs\UnshitWindows).
# Stops a running copy first (its exe would be locked) and starts the new one afterwards if it was running.
# Framework-dependent single file: needs the .NET 10 Desktop Runtime, which keeps the exe small.
param(
    [string]$OutDir = (Join-Path $env:LOCALAPPDATA 'Programs\UnshitWindows'),
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\UnshitWindows\UnshitWindows.csproj'

$running = Get-Process UnshitWindows -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process
    $running | Wait-Process -Timeout 10
}

dotnet publish $project -c Release -r win-x64 --self-contained false `
    -p:PublishSingleFile=true -p:DebugType=none -o $OutDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $OutDir 'UnshitWindows.exe'
"Published $exe"
if ($Start -or $running) {
    Start-Process $exe
    "Started it."
}
