# Inspect a generated ResponsiveUI-*.unitypackage when present.
# Skip (exit 0) when no export exists — generating one requires Unity batchmode.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\validate-export-manifest.ps1

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$project = Resolve-Path (Join-Path $here "..\..\..")
$failed = 0
$passed = 0

function Assert-True($cond, $msg) {
    if ($cond) {
        Write-Host "PASS  $msg" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "FAIL  $msg" -ForegroundColor Red
        $script:failed++
    }
}

$searchDirs = @(
    (Join-Path $project "Temp"),
    (Join-Path $project "Assets\ResponsiveUI")
)
$pkg = $null
foreach ($dir in $searchDirs) {
    if (-not (Test-Path $dir)) { continue }
    $found = Get-ChildItem $dir -Filter "ResponsiveUI-*.unitypackage" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($found) {
        $pkg = $found
        break
    }
}

if (-not $pkg) {
    Write-Host "SKIP  no ResponsiveUI-*.unitypackage (export first to validate payload)" -ForegroundColor Yellow
    exit 0
}

Write-Host "Export: $($pkg.FullName)" -ForegroundColor Cyan
$extract = Join-Path ([IO.Path]::GetTempPath()) ("rui-export-" + [guid]::NewGuid().ToString("n"))
New-Item -ItemType Directory -Path $extract | Out-Null
try {
    tar -xf $pkg.FullName -C $extract 2>$null
    if ($LASTEXITCODE -ne 0) {
        $tgz = Join-Path $extract "pkg.tgz"
        Copy-Item $pkg.FullName $tgz
        tar -xzf $tgz -C $extract
    }

    $paths = Get-ChildItem $extract -Recurse -Filter pathname -File |
        ForEach-Object { (Get-Content $_.FullName -Raw).Trim().Replace('\', '/') }

    Assert-True ($paths.Count -gt 20) "export lists production assets ($($paths.Count) paths)"

    $hasTests = $false
    $hasPhonePadDevice = $false
    $hasOverlays = $false
    $hasPackageJson = $false
    $hasSharedEditor = $false
    $hasRuntime = $false
    foreach ($path in $paths) {
        if ($path -match '/Tests(/|$)') { $hasTests = $true }
        if ($path -match '/PhonePad/' -and ($path -match '\.device(\.off)?$')) { $hasPhonePadDevice = $true }
        if ($path -match '/PhonePad/Editor/Devices/DeviceOverlays/') { $hasOverlays = $true }
        if ($path -match 'package\.json$') { $hasPackageJson = $true }
        if ($path -match '/SharedEditor/') { $hasSharedEditor = $true }
        if ($path -match '/Runtime/ResponsiveUI\.cs$') { $hasRuntime = $true }
    }

    Assert-True (-not $hasTests) "export excludes Tests/"
    Assert-True (-not $hasPhonePadDevice) "export excludes generated PhonePad .device skins"
    Assert-True (-not $hasOverlays) "export excludes PhonePad DeviceOverlays"
    Assert-True $hasPackageJson "export includes package.json"
    Assert-True $hasSharedEditor "export includes SharedEditor"
    Assert-True $hasRuntime "export includes Runtime/ResponsiveUI.cs"
}
finally {
    Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Export manifest: $passed passed, $failed failed." -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
exit 0
