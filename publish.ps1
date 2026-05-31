#requires -Version 7.0
<#
.SYNOPSIS
  Publishes Ztar for distribution: a self-contained, single-file Ztar.exe that runs without
  a separate .NET install, plus the cross-platform console extractor stub.

.PARAMETER Runtime
  Target runtime identifier. Default: win-x64.

.PARAMETER OutDir
  Output directory. Default: dist.

.EXAMPLE
  ./publish.ps1
  ./publish.ps1 -Runtime win-arm64 -OutDir release
#>
param(
    [string]$Runtime = "win-x64",
    [string]$OutDir = "dist",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$out = Join-Path $root $OutDir

Write-Host "Ztar publish -> $out (runtime=$Runtime, config=$Configuration)" -ForegroundColor Cyan

# Clean previous output.
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Path $out | Out-Null

# Main app (GUI + CLI). Self-contained single file so end users need no .NET runtime.
# The MSBuild PublishStub target publishes + embeds the extractor stub automatically.
Write-Host "Publishing Ztar.exe (self-contained single file)..." -ForegroundColor Yellow
& dotnet publish (Join-Path $root "Ztar.Main/Ztar.Main.csproj") `
    -c $Configuration -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o $out
if ($LASTEXITCODE -ne 0) { throw "Publish Ztar.Main failed." }

# Cross-platform console extractor stub (framework-dependent single file).
Write-Host "Publishing console stub..." -ForegroundColor Yellow
$stubOut = Join-Path $out "console-stub"
& dotnet publish (Join-Path $root "Ztar.StubConsole/Ztar.StubConsole.csproj") `
    -c $Configuration -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -o $stubOut
if ($LASTEXITCODE -ne 0) { throw "Publish Ztar.StubConsole failed." }

# Trim publish noise: keep just the runnable artifacts.
Get-ChildItem $out -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

$exe = Join-Path $out "Ztar.exe"
if (Test-Path $exe) {
    $size = (Get-Item $exe).Length / 1MB
    Write-Host ("Done. Ztar.exe = {0:N1} MB" -f $size) -ForegroundColor Green
    Write-Host "Output: $out"
} else {
    throw "Ztar.exe not found after publish."
}
