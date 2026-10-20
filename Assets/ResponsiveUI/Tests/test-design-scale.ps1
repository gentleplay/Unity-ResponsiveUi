# Design-scale math tests for ResponsiveUI (no Unity required).
# Mirrors ResponsiveRectState.ComputeCanvasRatio / Unscale / Apply localScale rule.
# Run: powershell -File Assets/ResponsiveUI/Tests/test-design-scale.ps1

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
    if ([math]::Abs($cx - $cy) -lt 1e-5) {
        return @($dx, $dy)
    }
    $designLandscape = $dx -gt $dy
    $currentLandscape = $cx -gt $cy
    if ($designLandscape -eq $currentLandscape) {
        return @($dx, $dy)
    }
    return @($dy, $dx)
}

function Get-NormalizeComponent([float]$value) {
    if ([float]::IsNaN($value) -or [float]::IsInfinity($value) -or [math]::Abs($value) -lt 1e-5) {
        return 1.0
    }
    if ([math]::Abs($value - 1) -lt 1e-5) {
        return 1.0
    }
    return [float]$value
}

function Get-ResidualRatio([float]$dx, [float]$dy, [float]$ax, [float]$ay) {
    $dx = Get-NormalizeComponent $dx
    $dy = Get-NormalizeComponent $dy
    $rx = Get-NormalizeComponent $(if ([math]::Abs($ax) -lt 1e-5) { $dx } else { $dx / $ax })
    $ry = Get-NormalizeComponent $(if ([math]::Abs($ay) -lt 1e-5) { $dy } else { $dy / $ay })
    return @($rx, $ry)
}

# Modes: 0 Width, 1 Height, 2 UniformMin, 3 UniformMax, 4 IndependentXY, 5 Auto
function Resolve-AutoScaleMode([int]$mode, [float]$cx, [float]$cy, [float]$dx, [float]$dy) {
    if ($mode -ne 5) { return $mode }
    if ($cx -le 0 -or $cy -le 0 -or $dx -le 0 -or $dy -le 0) { return 0 }
    $liveAspect = $cx / $cy
    $designAspect = $dx / $dy
    if ($liveAspect -gt $designAspect) { return 1 } else { return 0 }
}

function Get-CanvasRatioFromDesign([float]$cx, [float]$cy, [float]$dx, [float]$dy, [int]$mode) {
    if ($dx -le 0 -or $dy -le 0) { return @(1.0, 1.0) }
    if ($cx -le 0 -or $cy -le 0) { return @(1.0, 1.0) }

    $mode = Resolve-AutoScaleMode $mode $cx $cy $dx $dy
    $rx = $cx / $dx
    $ry = $cy / $dy

    switch ($mode) {
        1 { return @($ry, $ry) }              # Height
        2 { $s = [math]::Min($rx, $ry); return @($s, $s) }  # UniformMin
        3 { $s = [math]::Max($rx, $ry); return @($s, $s) }  # UniformMax
        4 { return @($rx, $ry) }              # IndependentXY
        default { return @($rx, $rx) }        # Width
    }
}

function Get-CanvasRatio([float]$cx, [float]$cy, [float]$dx, [float]$dy, [int]$mode) {
    $design = Orient-SizeToMatch $dx $dy $cx $cy
    return Get-CanvasRatioFromDesign $cx $cy $design[0] $design[1] $mode
}

function Get-AppliedLocalScale([float]$ox, [float]$oy, [float]$rx, [float]$ry, [bool]$scaleOn) {
    if (-not $scaleOn) { return @([float]$ox, [float]$oy) }
    $lx = [float]$ox * [float]$rx
    $ly = [float]$oy * [float]$ry
    return @($lx, $ly)
}

function Get-Unscale([float]$lx, [float]$ly, [float]$sx, [float]$sy) {
    $sx = Get-NormalizeComponent $sx
    $sy = Get-NormalizeComponent $sy
    if (([math]::Abs($sx - 1) -lt 1e-5) -and ([math]::Abs($sy - 1) -lt 1e-5)) {
        return @([float]$lx, [float]$ly)
    }
    return @([float]($lx / $sx), [float]($ly / $sy))
}

function Convert-ScalerCanvasToDesign(
    [float]$cx, [float]$cy,
    [float]$rx, [float]$ry,
    [float]$dx, [float]$dy) {
    $deltaX = [math]::Abs($cx - $rx) / $rx
    $deltaY = [math]::Abs($cy - $ry) / $ry
    if ($deltaX -le 0.01 -and $deltaY -le 0.01) {
        return @($dx, $dy)
    }
    return @(($cx * $dx / $rx), ($cy * $dy / $ry))
}

Write-Host "=== Responsive UI design-scale math ===" -ForegroundColor Cyan

$phone = Get-Content (Join-Path $pkg "PhonePad\Editor\Devices\Profiles\iphone-17-pro-max.json") -Raw | ConvertFrom-Json
$compact = Get-Content (Join-Path $pkg "PhonePad\Editor\Devices\Profiles\iphone-16e.json") -Raw | ConvertFrom-Json
$pad = Get-Content (Join-Path $pkg "PhonePad\Editor\Devices\Profiles\ipad-pro-13-m5.json") -Raw | ConvertFrom-Json
$pw = [double]$phone.portraitWidth
$ph = [double]$phone.portraitHeight
$lw = [double]$phone.landscapeWidth
$lh = [double]$phone.landscapeHeight
$cw = [double]$compact.landscapeWidth
$ch = [double]$compact.landscapeHeight
$padW = [double]$pad.landscapeWidth
$padH = [double]$pad.landscapeHeight

# Width mode: half of catalog Phone portrait → ratio 0.5
$r = Get-CanvasRatio ($pw / 2) ($ph / 2) $pw $ph 0
Assert-Approx $r[0] 0.5 "Width mode ratio X"
Assert-Approx $r[1] 0.5 "Width mode ratio Y (uniform)"

# Height mode uses height ratio
$r = Get-CanvasRatio 1000 ($ph / 2) $pw $ph 1
Assert-Approx $r[0] 0.5 "Height mode ratio from half catalog height"
Assert-Approx $r[1] 0.5 "Height mode Y matches X"

# UniformMin / Max
$r = Get-CanvasRatio ($pw / 2) 2000 $pw $ph 2
Assert-Approx $r[0] 0.5 "UniformMin picks min(0.5, taller height ratio)"
$r = Get-CanvasRatio ($pw / 2) 2000 $pw $ph 3
Assert-Approx $r[0] (2000.0 / $ph) "UniformMax picks max"

# IndependentXY
$r = Get-CanvasRatio ($pw / 2) ($ph / 2) $pw $ph 4
Assert-Approx $r[0] 0.5 "IndependentXY X"
Assert-Approx $r[1] 0.5 "IndependentXY Y"

# Hand-calculated table: current 800x1200, design 1000x2000 (no orient swap)
$r = Get-CanvasRatio 800 1200 1000 2000 0
Assert-Approx $r[0] 0.8 "Width table X"
Assert-Approx $r[1] 0.8 "Width table Y"
$r = Get-CanvasRatio 800 1200 1000 2000 1
Assert-Approx $r[0] 0.6 "Height table X"
Assert-Approx $r[1] 0.6 "Height table Y"
$r = Get-CanvasRatio 800 1200 1000 2000 2
Assert-Approx $r[0] 0.6 "UniformMin table"
$r = Get-CanvasRatio 800 1200 1000 2000 3
Assert-Approx $r[0] 0.8 "UniformMax table"
$r = Get-CanvasRatio 800 1200 1000 2000 4
Assert-Approx $r[0] 0.8 "IndependentXY table X"
Assert-Approx $r[1] 0.6 "IndependentXY table Y"
# Auto: 800x1200 is wider than 1000x2000 → Height = UniformMin
$r = Get-CanvasRatio 800 1200 1000 2000 5
Assert-Approx $r[0] 0.6 "Auto wider than design uses Height"
$rMin = Get-CanvasRatio 800 1200 1000 2000 2
Assert-Approx $r[0] $rMin[0] "Auto matches UniformMin on wider live"
# Auto: taller 16:9 vs Phone landscape → Width = UniformMin
$r = Get-CanvasRatio $cw $ch $lw $lh 5
$rMin = Get-CanvasRatio $cw $ch $lw $lh 2
Assert-Approx $r[0] $rMin[0] "Auto matches UniformMin on compact Phone vs Pro Max"

# Stretch visual intent: transformed size = parent span + authored sizeDelta * ratio
# parent 400, authored inset -40, ratio 0.5 -> visual 400 + (-40)*0.5 = 380
$span = 400.0
$authored = -40.0
$ratio = 0.5
$applied = $authored + $span * (1.0 / $ratio - 1.0)
$visual = ($span + $applied) * $ratio
Assert-Approx $applied 360.0 "stretch compensation sizeDelta"
Assert-Approx $visual 380.0 "stretch visual equals parent plus scaled inset"

# Orient swap: portrait design vs landscape current
$r = Get-CanvasRatio $lw $lh $pw $ph 0
# design becomes landscape to match; rx = 1
Assert-Approx $r[0] 1.0 "Orient swap Width ratio stays 1 for swapped pair"

# Game View widget (Scale to Fit) vs iPhone 17 Pro Max catalog — must not drive recapture
$r = Get-CanvasRatio 1221 563 $pw $ph 0
Assert-Approx $r[0] (1221.0 / $lw) "Game View widget vs catalog is not identity (do not use as live size)"
$u = Get-Unscale 450 -220 $r[0] $r[1]
Assert-Approx $u[0] (450.0 / (1221.0 / $lw)) "Wrong live size unscales 450"
$u = Get-Unscale 1 1 $r[0] $r[1]
Assert-Approx $u[0] ($lw / 1221.0) "Wrong live size unscales scale 1"

# Reference device landscape live size keeps authored pixels
$r = Get-CanvasRatio $lw $lh $pw $ph 0
$u = Get-Unscale 450 -220 $r[0] $r[1]
Assert-Approx $u[0] 450 "Reference landscape keeps authored X"
Assert-Approx $u[1] -220 "Reference landscape keeps authored Y"
$u = Get-Unscale 1 1 $r[0] $r[1]
Assert-Approx $u[0] 1.0 "Reference landscape keeps origin scale 1"

# CanvasScaler reference units map into Design Canvas units before ratio math.
$mapped = Convert-ScalerCanvasToDesign 2537.036 1167.6754 $cw $ch $lw $lh
Assert-Approx $mapped[0] $lw "Scaler reference maps to design identity X"
Assert-Approx $mapped[1] $lh "Scaler reference maps to design identity Y"
$mapped = Convert-ScalerCanvasToDesign 2295 1290 $cw $ch $lw $lh
Assert-Approx $mapped[0] (2295.0 * $lw / $cw) "Off-reference scaler canvas preserves residual X"
Assert-Approx $mapped[1] (1290.0 * $lh / $ch) "Off-reference scaler canvas preserves residual Y"

# Unset / invalid → identity
$r = Get-CanvasRatio 800 600 0 0 0
Assert-Approx $r[0] 1.0 "Unset design size -> identity X"
Assert-Approx $r[1] 1.0 "Unset design size -> identity Y"

# Square canvas must not treat landscape design as portrait
$r = Get-CanvasRatio 1000 1000 2000 1000 0
Assert-Approx $r[0] 0.5 "Square canvas keeps landscape design Width ratio"

# Nested residual: desired 0.5 with ancestor 0.5 -> local 1
$r = Get-ResidualRatio 0.5 0.5 0.5 0.5
Assert-Approx $r[0] 1.0 "Nested residual X is identity"
Assert-Approx $r[1] 1.0 "Nested residual Y is identity"
$r = Get-ResidualRatio 0.5 0.5 1 1
Assert-Approx $r[0] 0.5 "Root residual keeps desired X"
Assert-Approx $r[1] 0.5 "Root residual keeps desired Y"

# Explicit portrait on a landscape canvas (no inferred swap)
$r = Get-CanvasRatioFromDesign 1000 500 500 1000 0
Assert-Approx $r[0] 2.0 "Explicit portrait Width X on 1000x500"
Assert-Approx $r[1] 2.0 "Explicit portrait Width Y on 1000x500"
$r = Get-CanvasRatioFromDesign 1000 500 500 1000 1
Assert-Approx $r[0] 0.5 "Explicit portrait Height X on 1000x500"
Assert-Approx $r[1] 0.5 "Explicit portrait Height Y on 1000x500"

# Normalize invalid / near-one components to identity
Assert-Approx (Get-NormalizeComponent 0) 1.0 "Zero ratio normalizes to 1"
Assert-Approx (Get-NormalizeComponent 1e-6) 1.0 "Tiny ratio normalizes to 1"
Assert-Approx (Get-NormalizeComponent 0.5) 0.5 "Finite ratio is kept"

$u = Get-Unscale 40 -10 0 0
Assert-Approx $u[0] 40 "Zero ratio unscale keeps live X"
Assert-Approx $u[1] -10 "Zero ratio unscale keeps live Y"

Write-Host "=== localScale = origin * ratio ===" -ForegroundColor Cyan

$local = Get-AppliedLocalScale 1 1 0.5 0.5 $true
Assert-Approx $local[0] 0.5 "ON: origin 1 * ratio 0.5"
$local = Get-AppliedLocalScale 2 1.5 0.5 0.5 $true
Assert-Approx $local[0] 1.0 "ON: origin 2 * 0.5"
Assert-Approx $local[1] 0.75 "ON: origin 1.5 * 0.5"
$local = Get-AppliedLocalScale 2 1.5 0.5 0.5 $false
Assert-Approx $local[0] 2.0 "OFF: restores origin X"
Assert-Approx $local[1] 1.5 "OFF: restores origin Y"

Write-Host "=== Capture un-scale (live -> design) ===" -ForegroundColor Cyan

$u = Get-Unscale 50 80 0.5 0.5
Assert-Approx $u[0] 100 "Unscale position X"
Assert-Approx $u[1] 160 "Unscale position Y"
$u = Get-Unscale 0.5 0.5 0.5 0.5
Assert-Approx $u[0] 1.0 "Unscale localScale -> origin X"
Assert-Approx $u[1] 1.0 "Unscale localScale -> origin Y"

Write-Host "=== OrientSize (background cover) ===" -ForegroundColor Cyan

function Orient-Size([float]$x, [float]$y, [string]$orientation) {
    if ($x -le 0 -or $y -le 0) { return @($x, $y) }
    $landscape = $x -ge $y
    if ($orientation -eq "Landscape") {
        if ($landscape) { return @($x, $y) }
        return @($y, $x)
    }
    if ($landscape) { return @($y, $x) }
    return @($x, $y)
}

$o = Orient-Size $pw $ph "Landscape"
Assert-Approx $o[0] $lw "Portrait catalog → landscape X"
Assert-Approx $o[1] $lh "Portrait catalog → landscape Y"
$o = Orient-Size $padW $padH "Landscape"
Assert-Approx $o[0] $padW "Already landscape stays X"
Assert-Approx $o[1] $padH "Already landscape stays Y"
$o = Orient-Size $padW $padH "Portrait"
Assert-Approx $o[0] $padH "Landscape art → portrait X"
Assert-Approx $o[1] $padW "Landscape art → portrait Y"

Write-Host "=== Inspector chrome ===" -ForegroundColor Cyan
Assert-True (Test-Path (Join-Path $PSScriptRoot "Editor\ResponsiveUiInspectorChromeTests.cs")) `
    "Inspector chrome Unity tests exist"

Write-Host ""
Write-Host "Result: $passed passed, $failed failed" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
if ($failed -gt 0) { exit 1 }
exit 0
