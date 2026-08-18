# Builds the ARCHNEXUS desktop executable: React build -> embedded in the assembly -> single exe.
# Usage:  powershell -ExecutionPolicy Bypass -File scripts\build-desktop.ps1

$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$frontend = Join-Path $repo 'frontend'
$desktop = Join-Path $repo 'Archlab.Desktop'
$webroot = Join-Path $desktop 'wwwroot'
$output = Join-Path $repo 'dist-desktop'

Write-Host '==> Building the React frontend' -ForegroundColor Cyan
Push-Location $frontend
try {
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "npm run build failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}

Write-Host '==> Staging the build into the desktop project' -ForegroundColor Cyan
if (Test-Path $webroot) { Remove-Item $webroot -Recurse -Force }
New-Item -ItemType Directory -Path $webroot | Out-Null
Copy-Item (Join-Path $frontend 'dist\*') $webroot -Recurse -Force

Write-Host '==> Publishing the executable' -ForegroundColor Cyan
dotnet publish $desktop -c Release -o $output --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE" }

$exe = Join-Path $output 'ArchNexus.exe'
$size = [math]::Round((Get-Item $exe).Length / 1MB, 1)

Write-Host ''
Write-Host "Pronto: $exe ($size MB)" -ForegroundColor Green
Write-Host 'O terminal precisa do WebView2 Runtime, que ja vem no Windows 11.' -ForegroundColor DarkGray
