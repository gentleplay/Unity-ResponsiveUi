# Camera Scale Projection contracts (no Unity required).
# Independent aspect-framing math — not a grep that ScaleFieldOfView exists.
# The old FOV bug (designFov × pixel canvas ratio) still compiled and still
# passed round-trip tests because Scale/Unscale shared the same wrong formula.
# Run: powershell -File Assets/ResponsiveUI/Tests/test-camera-projection.ps1

$ErrorActionPreference = "Stop"
$failed = 0
$passed = 0
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")

function Assert-True($cond, $msg) {
    if ($cond) {
        Write-Host "PASS  $msg" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "FAIL  $msg" -ForegroundColor Red
        $script:failed++
    }
}

function Assert-Approx($actual, $expected, $msg, $eps = 1e-4) {
    Assert-True ([math]::Abs($actual - $expected) -le $eps) "$msg (got=$actual expected=$expected)"
}

function Orient-SizeToMatch([float]$dx, [float]$dy, [float]$cx, [float]$cy) {
    if ([math]::Abs($cx - $cy) -lt 1e-5) { return @($dx, $dy) }
    $designLandscape = $dx -gt $dy
    $currentLandscape = $cx -gt $cy
    if ($designLandscape -eq $currentLandscape) { return @($dx, $dy) }
    return @($dy, $dx)
}

# Modes: 0 Width, 1 Height, 2 UniformMin, 3 UniformMax, 4 IndependentXY, 5 Auto
function Get-AspectFramedOrtho([float]$designOrtho, [float]$cx, [float]$cy, [float]$dx, [float]$dy, [int]$mode) {
    $design = Orient-SizeToMatch $dx $dy $cx $cy
    $designAspect = $design[0] / $design[1]
    $liveAspect = $cx / $cy
    if ($mode -eq 5) {
        $mode = if ($liveAspect -gt $designAspect) { 1 } else { 0 }
    }
    $widthMatched = $designOrtho * ($designAspect / $liveAspect)
    $heightMatched = $designOrtho
    switch ($mode) {
        1 { return $heightMatched }
        2 { return [math]::Max($widthMatched, $heightMatched) }
        3 { return [math]::Min($widthMatched, $heightMatched) }
        default { return $widthMatched }
    }
}

function Get-AspectFramedFov([float]$designFov, [float]$cx, [float]$cy, [float]$dx, [float]$dy, [int]$mode) {
    $tanHalf = [math]::Tan($designFov * 0.5 * [math]::PI / 180.0)
    $liveTan = Get-AspectFramedOrtho $tanHalf $cx $cy $dx $dy $mode
    return (2.0 * [math]::Atan($liveTan) * 180.0 / [math]::PI)
}

function Get-PixelRatioScaled([float]$designValue, [float]$cx, [float]$dx) {
    return $designValue * ($cx / $dx)
}

Write-Host "=== Camera projection contracts ===" -ForegroundColor Cyan

function Read-JsonProfile([string]$rel) {
    $path = Join-Path $pkg $rel
    Assert-True (Test-Path $path) "catalog profile exists $rel"
    return (Get-Content $path -Raw | ConvertFrom-Json)
}

$phone = Read-JsonProfile "PhonePad\Editor\Devices\Profiles\iphone-17-pro-max.json"
$fold = Read-JsonProfile "FoldAndFlip\Editor\Devices\Profiles\galaxy-z-fold8-like.json"
$cover = @($fold.screens) | Where-Object { $_.coverOrOuter } | Select-Object -First 1
Assert-True ($null -ne $cover) "galaxy-z-fold8-like has a Cover screen"

$designW = [double]$phone.landscapeWidth
$designH = [double]$phone.landscapeHeight
$liveW = [double]$cover.landscapeWidth
$liveH = [double]$cover.landscapeHeight
$portraitW = [double]$phone.portraitWidth
$portraitH = [double]$phone.portraitHeight
$designOrtho = 0.87
$designFov = 60.0
Assert-True ($designW -gt $designH) "iphone-17-pro-max landscape is wider than tall"
Assert-True ($liveW -gt $liveH) "Fold8 Cover landscape is wider than tall"
Assert-True ([math]::Abs(($designW / $designH) - ($liveW / $liveH)) -gt 0.2) `
    "Cover aspect must differ from Phone Design Canvas so Width/Height modes diverge"

$aspectOrtho = Get-AspectFramedOrtho $designOrtho $liveW $liveH $designW $designH 0
$pixelOrtho = Get-PixelRatioScaled $designOrtho $liveW $designW
Assert-True ([math]::Abs($aspectOrtho - $pixelOrtho) -gt 0.3) `
    "Fold8 Cover Width ortho must not equal width pixel ratio (aspect=$aspectOrtho pixel=$pixelOrtho)"

$heightOrtho = Get-AspectFramedOrtho $designOrtho $liveW $liveH $designW $designH 1
Assert-Approx $heightOrtho $designOrtho "Height mode keeps authored ortho"
$autoOrtho = Get-AspectFramedOrtho $designOrtho $liveW $liveH $designW $designH 5
$minOrtho = Get-AspectFramedOrtho $designOrtho $liveW $liveH $designW $designH 2
Assert-Approx $autoOrtho $minOrtho "Auto ortho matches UniformMin fit on Cover vs Pro Max"

$aspectFov = Get-AspectFramedFov $designFov $liveW $liveH $designW $designH 0
$pixelFov = Get-PixelRatioScaled $designFov $liveW $designW
Assert-True ($aspectFov -gt $designFov) "Width aspect framing widens FOV on Cover vs Pro Max (got $aspectFov)"
Assert-True ($pixelFov -lt $designFov) "width pixel ratio shrinks FOV (got $pixelFov)"
Assert-True ([math]::Abs($aspectFov - $pixelFov) -gt 10) `
    "Fold8 Cover Width FOV must not equal width pixel ratio (aspect=$aspectFov pixel=$pixelFov)"

$sameAspectFov = Get-AspectFramedFov $designFov ($designW * 2) ($designH * 2) $designW $designH 0
Assert-Approx $sameAspectFov $designFov "same aspect keeps FOV"
$sameAspectPixel = Get-PixelRatioScaled $designFov ($designW * 2) $designW
Assert-Approx $sameAspectPixel 120.0 "sanity: 2x canvas pixel ratio doubles FOV"

$mismatchOrtho = Get-AspectFramedOrtho 5.0 $designW $designH $portraitW $portraitH 0
Assert-Approx $mismatchOrtho 5.0 "opposite-orientation design size must not crush ortho"

$cameraState = Get-Content (Join-Path $pkg "Runtime\ResponsiveCameraState.cs") -Raw
$fovScale = [regex]::Match(
    $cameraState,
    '(?s)public static float ScaleFieldOfView\(.*?public static float UnscaleFieldOfView').Value
Assert-True ($fovScale.Length -gt 80) "ScaleFieldOfView method is present"
Assert-True ($fovScale -match "FovToTan") "ScaleFieldOfView goes through tan of half-FOV"
Assert-True ($fovScale -match "ScaleOrthographicSize\(tanHalf") "ScaleFieldOfView uses ortho aspect framing"
Assert-True ($fovScale -notmatch "ComputeCanvasRatio") "ScaleFieldOfView must not use widget pixel canvas ratio"
Assert-True ($fovScale -notmatch "designFov\s*\*") "FOV must not multiply degrees by a canvas ratio"

Assert-True ($cameraState -match "TanToFov") "FOV unscale goes through atan"
Assert-True ($cameraState -match "designAspect / liveAspect") "projection scale uses aspect, not pixels"
Assert-True ($cameraState -notmatch "fieldOfView\s*\*") "Camera.fieldOfView must not be multiplied by a canvas ratio"

$cameraTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveCameraTests.cs") -Raw
Assert-True ($cameraTests -match "IndependentAspectFramedFov") "Unity FOV tests use an independent tan/aspect expected"
Assert-True ($cameraTests -match "IndependentAspectFramedOrtho") "Unity ortho tests use an independent aspect expected"
Assert-True ($cameraTests -match "PixelRatioScaledFov") "Unity tests reject width pixel-ratio projection"
Assert-True ($cameraTests -match "ChangingScaleModeWithoutRecapture_JumpsLiveOrtho") "Unity tests lock the Scale Mode jump without recapture"
Assert-True ($cameraTests -match "RecaptureScaleProjection_ScaleModeChange_KeepsLiveFov") "Unity tests lock Scale Mode recapture for FOV"
Assert-True ($cameraTests -notmatch "float expected = ResponsiveCameraState\.ScaleFieldOfView") `
    "Unity Apply FOV expected must not come from production ScaleFieldOfView"
Assert-True ($cameraTests -match "ResponsiveCatalogDevices") "Unity camera tests load catalog profiles"
Assert-True ($cameraTests -match "BuiltinDesignCanvasSizes_MatchCatalogReferenceProfiles") `
    "Unity tests lock builtin Design Canvas sizes to catalog reference profiles"
Assert-True ($cameraTests -notmatch "1334f") "Unity camera tests must not hardcode iPhone 6 pixels"
Assert-True ($cameraTests -notmatch "iPhone 6") "Unity camera tests must not name a device that is not in the catalog"

$widgetTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveDesignScaleTests.cs") -Raw
Assert-True ($widgetTests -match "Apply_ScaleModeChangeWithoutRecapture_JumpsLiveTarget") `
    "Unity widget tests lock the Scale Mode jump without recapture"

$slotTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveSlotSeedTests.cs") -Raw
Assert-True ($slotTests -match "RecaptureSlotFromLive_ChangingScaleMode_KeepsLiveTarget") `
    "Unity widget tests lock Scale Mode recapture keeping the live Target"
Assert-True ($slotTests -notmatch "1334f") "Unity slot tests must not hardcode iPhone 6 pixels"

$chromeTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveUiInspectorChromeTests.cs") -Raw
Assert-True ($chromeTests -match "InspectorSectionKey_DiffersPerComponent") `
    "Unity Inspector chrome tests lock per-component foldout keys"
Assert-True ($chromeTests -match "PropertySectionKey_IncludesInstanceIdAndPath") `
    "Unity Inspector chrome tests lock per-slot foldout keys"
Assert-True ($chromeTests -match "FieldLabelWidthFor_WideRow_UsesStandardWidth") `
    "Unity Inspector chrome tests lock the label column width"
Assert-True ($chromeTests -match "DrawFoldout_DoesNotKeepCallerIndent") `
    "Unity Inspector chrome tests restore indent after foldout arrows"
Assert-True ($chromeTests -match "NestedContentRect_OffsetsByFoldoutArrow") `
    "Unity Inspector chrome tests nest foldout bodies under titles"
Assert-True ($chromeTests -match "HeaderFoldoutRect_Tinted_InsetsPastAccent") `
    "Unity Inspector chrome tests keep section arrows inside the tint bar"
Assert-True ($chromeTests -match "HeaderTintInset_Untinted_IsHeaderPad") `
    "Unity Inspector chrome tests keep boxed and unboxed arrows on one pad"
Assert-True ($chromeTests -notmatch "1334f") "Unity Inspector chrome tests must not hardcode iPhone 6 pixels"

Write-Host ""
Write-Host "Camera projection: $passed passed, $failed failed"
if ($failed -gt 0) { exit 1 }
exit 0
