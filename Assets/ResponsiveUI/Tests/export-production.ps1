# Export ResponsiveUI-1.0.0.unitypackage (production).
# Requires Unity Editor closed for this project (batchmode cannot share an open project).
#
# Usage (from repo root):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\export-production.ps1
#
# Or with Editor open: Responsive UI → Project → Export Unity Package

$ErrorActionPreference = "Stop"
$project = Resolve-Path (Join-Path $PSScriptRoot "..\..\..")
$outDir = Join-Path $project "Temp"
$log = Join-Path $outDir "responsiveui-export.log"
$unityCandidates = @(
    $env:UNITY_EDITOR,
    "C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Unity.exe"
) | Where-Object {
    $_ -and $_.EndsWith('.exe', [StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $_)
}

if (-not $unityCandidates) {
    Write-Error "Unity Editor not found. Set UNITY_EDITOR to Unity.exe or install 6000.3.21f1."
}

$unity = @($unityCandidates)[0]
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

Write-Host "Project: $project"
Write-Host "Unity:   $unity"
Write-Host "Log:     $log"

& $unity -batchmode -nographics -projectPath $project.Path `
    -executeMethod FDPlus.ResponsiveUI.Editor.ResponsiveUiPackageExport.ExportUnityPackageBatch `
    -logFile $log
$code = $LASTEXITCODE
Write-Host "Unity exit: $code"

$pkg = Get-ChildItem (Join-Path $outDir "ResponsiveUI-*.unitypackage") -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($code -ne 0 -or -not $pkg) {
    if (Test-Path $log) {
        Write-Host "--- log tail ---"
        Get-Content $log -Tail 40
    }
    exit 1
}

Write-Host "Exported: $($pkg.FullName) ($([math]::Round($pkg.Length/1MB, 2)) MB)"
exit 0
