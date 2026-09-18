<#
    publish.ps1 — Publish script for ZeroZip (Dual Mode: Full & Lite)
    Adheres to AgentOption .NET Publish Release standard & ZeroUniverse rules.
#>
[CmdletBinding()]
param(
    [ValidateSet('Full', 'Lite', 'All')]
    [string]$Mode = 'Lite',
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'
$Root = if (![string]::IsNullOrEmpty($PSScriptRoot)) { $PSScriptRoot } else { (Get-Location).Path }
$MainProj = Join-Path $Root "ZeroZip.Main\ZeroZip.Main.csproj"
$StubProj = Join-Path $Root "ZeroZip.StubConsole\ZeroZip.StubConsole.csproj"
$Dist = Join-Path $Root "publish"

if (Test-Path $Dist) {
    Remove-Item $Dist -Recurse -Force -ErrorAction SilentlyContinue
}

if ($Mode -eq 'Full' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroZip FULL (Self-Contained Single File)..." -ForegroundColor Cyan
    $outFull = Join-Path $Dist "full"
    
    # 1. Main app (GUI + CLI)
    dotnet publish $MainProj -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $outFull
        
    # 2. Console extractor stub
    $stubFull = Join-Path $outFull "console-stub"
    dotnet publish $StubProj -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -o $stubFull
        
    Get-ChildItem $outFull -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "  ✔ Full build generated at: $outFull\ZeroZip.exe" -ForegroundColor Green
}

if ($Mode -eq 'Lite' -or $Mode -eq 'All') {
    Write-Host ">>> Publishing ZeroZip LITE (Framework-Dependent Single File)..." -ForegroundColor Cyan
    $outLite = Join-Path $Dist "lite"
    
    # 1. Main app (GUI + CLI)
    dotnet publish $MainProj -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true `
        -o $outLite
        
    # 2. Console extractor stub
    $stubLite = Join-Path $outLite "console-stub"
    dotnet publish $StubProj -c $Configuration -r $Runtime --self-contained false `
        -p:PublishSingleFile=true `
        -o $stubLite
        
    Get-ChildItem $outLite -Filter *.pdb -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue
    Write-Host "  ✔ Lite build generated at: $outLite\ZeroZip.exe" -ForegroundColor Green
}

Write-Host ">>> ZeroZip publish completed successfully!" -ForegroundColor Green
