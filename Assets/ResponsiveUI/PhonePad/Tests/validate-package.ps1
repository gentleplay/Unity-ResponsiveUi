# Structural + packaging gate for Assets/ResponsiveUI/PhonePad (no Unity required).
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\PhonePad\Tests\validate-package.ps1

$ErrorActionPreference = "Stop"
$here = $PSScriptRoot
$pkg = Resolve-Path (Join-Path $here "..")
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

Write-Host "Package root: $pkg" -ForegroundColor Cyan
Write-Host "=== Package structure ===" -ForegroundColor Cyan

$required = @(
    "README.md",
    "..\Docs\PRODUCTION.md",
    "..\Docs\STRUCTURE.md",
    "Editor\com.futuredigitalplus.phonepad.simulator.editor.asmdef",
    "Editor\PhonePadPackageSetup.cs",
    "Editor\PhonePadDeviceCatalog.cs",
    "Editor\PhonePadDeviceCatalogWindow.cs",
    "Editor\PhonePadDeviceCatalogIO.cs",
    "Editor\PhonePadDeviceSimulatorSkin.cs",
    "Editor\PhonePadPhotorealFrameImport.cs",
    "Editor\PhonePadPhotorealFrameMap.g.cs",
    "Editor\PhonePadSimulatorListUtil.cs",
    "Tests\audit-profiles.ps1"
)

foreach ($rel in $required) {
    Assert-True (Test-Path (Join-Path $pkg $rel)) "exists $rel"
}

$asm = Get-Content (Join-Path $pkg "Editor\com.futuredigitalplus.phonepad.simulator.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True ($asm.name -eq "com.futuredigitalplus.phonepad.simulator.editor") "asmdef package id"
Assert-True ($asm.rootNamespace -eq "FDPlus.PhonePad.Editor") "asmdef rootNamespace"

$export = Get-Content (Join-Path (Split-Path $pkg -Parent) "Editor\Package\ResponsiveUiPackageExport.cs") -Raw
Assert-True ($export -match "ResponsiveUiEditorMenus\.ExportUnityPackage") "unified export menu wired"
Assert-True ($export -match "DeviceOverlays") "export excludes PhonePad DeviceOverlays"
Assert-True (-not (Test-Path (Join-Path $pkg "Editor\PhonePadPackageExport.cs"))) "module export class removed"

$skin = Get-Content (Join-Path $pkg "Editor\PhonePadDeviceSimulatorSkin.cs") -Raw
Assert-True ($skin -match "overlayPath") "PhonePad skins write presentation.overlayPath"
Assert-True ($skin -match "EnsureBezelOverlay|WriteBezelOverlayPng") "PhonePad generates bezel overlay PNGs"
Assert-True ($skin -match "DeviceOverlays") "PhonePad overlays live under DeviceOverlays"
Assert-True ($skin -match "PhonePadPhotorealFrameImport\.GetPresentation") "skins prefer local photoreal overlays when present"

$photo = Get-Content (Join-Path $pkg "Editor\PhonePadPhotorealFrameImport.cs") -Raw
Assert-True ($photo -match "ImportPhotorealFramesMenu|ImportAll") "photoreal import entry exists"
Assert-True ($photo -match "device-frames-media") "photoreal import cites Device Frames Media"
Assert-True ($photo -match "ClearPhotorealFramesMenu|ClearImportedOverlays") "photoreal clear entry exists"
Assert-True ($photo -match "PhonePadDeviceCatalog\.EnsureOverlayPngMeta") "photoreal overlays share the skin overlay importer settings"
Assert-True ($photo -match "DisplayCancelableProgressBar") "photoreal download can be cancelled"
Assert-True (-not (Test-Path (Join-Path $pkg "Editor\PhonePadPathUtil.cs"))) "path helpers come from ResponsiveUiEditorPath (no PhonePad wrapper)"

$setup = Get-Content (Join-Path $pkg "Editor\PhonePadPackageSetup.cs") -Raw
Assert-True ((Get-Content (Join-Path $pkg "README.md") -Raw) -match "com\.futuredigitalplus\.phonepad\.simulator") "README package id"
Assert-True ($setup -match "EnableDefaultSimulatorDevices") "setup can enable default simulator skins"
Assert-True ($setup -notmatch "InitializeOnLoad") "PhonePad first-run owned by root ResponsiveUiPackageSetup"
Assert-True ($setup -match 'Version = "1\.0\.0"') "PhonePad module version 1.0.0"
Assert-True ($setup -match "ModuleRelativePath") "setup knows ModuleRelativePath"
Assert-True ($setup -notmatch "MenuItem.*ValidateSetup") "PhonePad Validate menu removed (unified Validate)"

$rootSetup = Get-Content (Join-Path (Split-Path $pkg -Parent) "Editor\Package\ResponsiveUiPackageSetup.cs") -Raw
Assert-True ($rootSetup -match "InitializeOnLoad") "root setup owns first-run"
Assert-True ($rootSetup -match "EnableDesignReferenceDevices") "root first-run enables only Design Canvas reference skins"

# Menu paths live in the kit's shared editor assembly (SharedEditor/ResponsiveUiEditorMenus.cs).
Assert-True (-not (Test-Path (Join-Path $pkg "Editor\PhonePadEditorMenus.cs"))) "no module copy of the menu paths"
$menus = Get-Content (Join-Path (Split-Path $pkg -Parent) "SharedEditor\ResponsiveUiEditorMenus.cs") -Raw
Assert-True ($menus -match 'Root = "Responsive UI"') "PhonePad menus under Responsive UI"
Assert-True ($menus -match "Phone & Pad Catalog") "menu path includes Phone & Pad Catalog"
Assert-True ($menus -match "Enable Phone & Pad Simulator Skins") "menu path includes Enable skins"
Assert-True ($menus -match "Import Photoreal Frames \(Local\)") "menu path includes Import Photoreal Frames"
Assert-True ($menus -match "Clear Local Photoreal Frames") "menu path includes Clear Local Photoreal Frames"
Assert-True ($menus -notmatch '"Phone & Pad"') "no former Phone & Pad top-level root"

$nsHits = Select-String -Path (Join-Path $pkg "Editor\*.cs") -Pattern "namespace PowerUp\.PhonePad" -SimpleMatch:$false
Assert-True ($null -eq $nsHits -or $nsHits.Count -eq 0) "no former PowerUp.PhonePad namespace"

$prefHits = Select-String -Path (Join-Path $pkg "Editor\*.cs") -Pattern "com\.powerup\.phonepad"
Assert-True ($null -eq $prefHits -or $prefHits.Count -eq 0) "no former com.powerup.phonepad prefs"

Assert-True (Test-Path (Join-Path $pkg "Tests\verify-official-specs.ps1")) "maint verify script under Tests"
Assert-True (-not (Test-Path (Join-Path $pkg "Editor\Devices\verify-official-specs.ps1"))) "verify script not under Editor/Devices"

$profiles = Join-Path $pkg "Editor\Devices\Profiles"
Assert-True (Test-Path $profiles) "profiles folder exists"
$jsonCount = @(Get-ChildItem $profiles -Filter "*.json" -ErrorAction SilentlyContinue).Count
Assert-True ($jsonCount -gt 0) "at least one device profile JSON ($jsonCount)"

# Child scripts call `exit`; run them out-of-process with file redirects so
# stdout pipe buffering cannot deadlock this gate.
function Invoke-NestedGate([string]$scriptPath) {
    $outFile = Join-Path ([IO.Path]::GetTempPath()) ("phonepad-gate-{0}-out.log" -f [guid]::NewGuid().ToString("n"))
    $errFile = Join-Path ([IO.Path]::GetTempPath()) ("phonepad-gate-{0}-err.log" -f [guid]::NewGuid().ToString("n"))
    try {
        # The same shell as this one: Windows PowerShell, or pwsh on Windows / macOS / Linux.
        $shellName = if ($PSVersionTable.PSEdition -eq "Desktop") { "powershell" } else { "pwsh" }
        $shell = (Get-Command $shellName -CommandType Application -ErrorAction SilentlyContinue |
            Select-Object -First 1).Source
        if (-not $shell) {
            $shell = @("powershell.exe", "pwsh.exe", "pwsh") |
                ForEach-Object { Join-Path $PSHOME $_ } |
                Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
                Select-Object -First 1
        }
        $p = Start-Process -FilePath $shell `
            -ArgumentList @(
                "-NoProfile",
                "-ExecutionPolicy", "Bypass",
                "-File", $scriptPath) `
            -Wait -PassThru -NoNewWindow `
            -RedirectStandardOutput $outFile `
            -RedirectStandardError $errFile
        if (Test-Path -LiteralPath $outFile) {
            Get-Content -LiteralPath $outFile | ForEach-Object { Write-Host $_ }
        }
        if (Test-Path -LiteralPath $errFile) {
            Get-Content -LiteralPath $errFile | ForEach-Object { Write-Host $_ }
        }
        return [int]$p.ExitCode
    } finally {
        Remove-Item -LiteralPath $outFile, $errFile -Force -ErrorAction SilentlyContinue
    }
}

Write-Host ""
Write-Host "=== Profile/schema/resolution audit (delegated) ===" -ForegroundColor Cyan
$auditScript = Join-Path $here "audit-profiles.ps1"
Assert-True (Test-Path $auditScript) "exists audit-profiles.ps1"
if (Test-Path $auditScript) {
    Assert-True ((Invoke-NestedGate $auditScript) -eq 0) "PhonePad profile audit suite"
}

Write-Host ""
Write-Host "PhonePad package gate: $passed passed, $failed failed." -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
exit 0
