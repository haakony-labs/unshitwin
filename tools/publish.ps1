# Publishes a Release build to a fixed per-user folder (default: %LOCALAPPDATA%\Programs\UnshitWindows).
# Stops a running copy first (its exe would be locked) and starts the new one afterwards if it was running.
#
# Default: framework-dependent single file (~0.4 MB), needs the .NET 10 Desktop Runtime installed.
# -SelfContained: bundles .NET into the exe (~50 MB, compressed), runs without any .NET install.
param(
    [string]$OutDir = (Join-Path $env:LOCALAPPDATA 'Programs\UnshitWindows'),
    [switch]$SelfContained,
    [switch]$Start
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\src\UnshitWindows\UnshitWindows.csproj'

$running = Get-Process UnshitWindows -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process
    $running | Wait-Process -Timeout 10
}

$publishArgs = @($project, '-c', 'Release', '-r', 'win-x64', '-p:PublishSingleFile=true', '-p:DebugType=none', '-o', $OutDir)
if ($SelfContained) {
    $publishArgs += '--self-contained', 'true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-p:EnableCompressionInSingleFile=true'
}
else {
    $publishArgs += '--self-contained', 'false'
}
dotnet publish @publishArgs
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)" }

$exe = Join-Path $OutDir 'UnshitWindows.exe'
"Published $exe ($([math]::Round((Get-Item $exe).Length / 1MB, 1)) MB)"
if ($Start -or $running) {
    Start-Process $exe
    "Started it."
}
