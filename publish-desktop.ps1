param(
    [string]$OutputDirectory = "artifacts\SoftrackerDesktop"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedOutput = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
$stagingRoot = Join-Path $repositoryRoot ".wfhtmp\desktop-publish"
$desktopStage = Join-Path $stagingRoot "desktop"
$webOutput = Join-Path $resolvedOutput "webapp"
$allowedRoot = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd('\') + '\'

if (-not $resolvedOutput.StartsWith($allowedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutputDirectory must stay inside the repository workspace."
}

if (Test-Path -LiteralPath $stagingRoot) {
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
}
if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

New-Item -ItemType Directory -Path $desktopStage -Force | Out-Null
New-Item -ItemType Directory -Path $webOutput -Force | Out-Null

dotnet publish (Join-Path $repositoryRoot "WFHMonitor\WFHMonitor.csproj") `
    -c Release -r win-x64 --self-contained true -o $webOutput
if ($LASTEXITCODE -ne 0) {
    throw "Publishing WFHMonitor failed with exit code $LASTEXITCODE."
}

dotnet publish (Join-Path $repositoryRoot "WFHMonitor.Desktop\WFHMonitor.Desktop.csproj") `
    -c Release -r win-x64 --self-contained true -o $desktopStage
if ($LASTEXITCODE -ne 0) {
    throw "Publishing the desktop host failed with exit code $LASTEXITCODE."
}

Copy-Item -Path (Join-Path $desktopStage "*") -Destination $resolvedOutput -Recurse -Force
Remove-Item -LiteralPath $stagingRoot -Recurse -Force

Write-Host "Desktop package created: $resolvedOutput"
Write-Host "Run: $(Join-Path $resolvedOutput 'Softracker.exe')"
