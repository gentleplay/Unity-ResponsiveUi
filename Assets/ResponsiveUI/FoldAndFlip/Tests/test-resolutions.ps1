# Exhaustive resolution + product-state matrix tests (no Unity required).
# Run: powershell -File Assets/ResponsiveUI/FoldAndFlip/Tests/test-resolutions.ps1

$ErrorActionPreference = "Stop"
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
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

function Assert-Eq($actual, $expected, $msg) {
    Assert-True ($actual -eq $expected) "$msg (got=$actual expected=$expected)"
}

# --- Expected overlay-matched specs ---
$Expected = @{
    "Pixel Fold-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2092 }
        Opened = @{ Name = "Inner"; W = 1840; H = 2208 }
        HalfOpened = @{ Name = "Inner"; W = 1840; H = 2208 }
    }
    "Galaxy Z Flip-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 720; H = 748 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Galaxy Z Fold6-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 968; H = 2376 }
        Opened = @{ Name = "Inner"; W = 1856; H = 2160 }
        HalfOpened = @{ Name = "Inner"; W = 1856; H = 2160 }
    }
    "Huawei Mate X3-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2504 }
        Opened = @{ Name = "Inner"; W = 2224; H = 2496 }
        HalfOpened = @{ Name = "Inner"; W = 2224; H = 2496 }
    }
    "Honor Magic V2-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1060; H = 2376 }
        Opened = @{ Name = "Inner"; W = 2156; H = 2344 }
        HalfOpened = @{ Name = "Inner"; W = 2156; H = 2344 }
    }
    "OnePlus Open-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1116; H = 2484 }
        Opened = @{ Name = "Inner"; W = 2268; H = 2440 }
        HalfOpened = @{ Name = "Inner"; W = 2268; H = 2440 }
    }
    "Xiaomi Mix Fold3-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 1916; H = 2160 }
        HalfOpened = @{ Name = "Inner"; W = 1916; H = 2160 }
    }
    "Motorola Razr+-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 1056; H = 1066 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Galaxy Z Fold7-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 1968; H = 2184 }
        HalfOpened = @{ Name = "Inner"; W = 1968; H = 2184 }
    }
    "Galaxy Z Flip7-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 948; H = 1048 }
        Opened = @{ Name = "Main"; W = 1080; H = 2520 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2520 }
    }
    "Pixel 9 Pro Fold-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2424 }
        Opened = @{ Name = "Inner"; W = 2076; H = 2152 }
        HalfOpened = @{ Name = "Inner"; W = 2076; H = 2152 }
    }
    "Oppo Find N5-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1140; H = 2616 }
        Opened = @{ Name = "Inner"; W = 2248; H = 2480 }
        HalfOpened = @{ Name = "Inner"; W = 2248; H = 2480 }
    }
    "Honor Magic V3-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1060; H = 2376 }
        Opened = @{ Name = "Inner"; W = 2156; H = 2344 }
        HalfOpened = @{ Name = "Inner"; W = 2156; H = 2344 }
    }
    "Motorola Razr 50 Ultra-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 1272 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Huawei Mate XT-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1008; H = 2232 }
        Opened = @{ Name = "Inner"; W = 2232; H = 3184 }
        HalfOpened = @{ Name = "Inner"; W = 2232; H = 3184 }
    }
    "Vivo X Fold3 Pro-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1172; H = 2748 }
        Opened = @{ Name = "Inner"; W = 2200; H = 2480 }
        HalfOpened = @{ Name = "Inner"; W = 2200; H = 2480 }
    }
    "Xiaomi Mix Fold4-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 2224; H = 2488 }
        HalfOpened = @{ Name = "Inner"; W = 2224; H = 2488 }
    }
    "Galaxy Z Fold8 Ultra-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 2256; H = 2504 }
        HalfOpened = @{ Name = "Inner"; W = 2256; H = 2504 }
    }
    "Galaxy Z Fold8-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1248; H = 1972 }
        Opened = @{ Name = "Inner"; W = 1848; H = 2448 }
        HalfOpened = @{ Name = "Inner"; W = 1848; H = 2448 }
    }
    "Galaxy Z Flip8-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 948; H = 1048 }
        Opened = @{ Name = "Main"; W = 1080; H = 2520 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2520 }
    }
    "Galaxy Z TriFold-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 1584; H = 2160 }
        HalfOpened = @{ Name = "Inner"; W = 1584; H = 2160 }
    }
    "Galaxy Z Flip7 FE-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 720; H = 748 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Galaxy Z Fold Special Edition-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1080; H = 2520 }
        Opened = @{ Name = "Inner"; W = 1968; H = 2184 }
        HalfOpened = @{ Name = "Inner"; W = 1968; H = 2184 }
    }
    "Galaxy Z Flip6-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 720; H = 748 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Galaxy Z Flip5-like" = @{
        Kind = "FlipablePhone"
        Closed = @{ Name = "Cover"; W = 720; H = 748 }
        Opened = @{ Name = "Main"; W = 1080; H = 2640 }
        HalfOpened = @{ Name = "Main"; W = 1080; H = 2640 }
    }
    "Galaxy Z Fold5-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 904; H = 2316 }
        Opened = @{ Name = "Inner"; W = 1812; H = 2176 }
        HalfOpened = @{ Name = "Inner"; W = 1812; H = 2176 }
    }
    "Galaxy Z Fold2-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 816; H = 2260 }
        Opened = @{ Name = "Inner"; W = 1768; H = 2208 }
        HalfOpened = @{ Name = "Inner"; W = 1768; H = 2208 }
    }
    "iPhone Duo-like" = @{
        Kind = "FoldablePhone"
        Closed = @{ Name = "Cover"; W = 1398; H = 2034 }
        Opened = @{ Name = "Inner"; W = 2007; H = 2853 }
        HalfOpened = @{ Name = "Inner"; W = 2007; H = 2853 }
    }
}

function Parse-Catalog {
    $devices = @{}
    $profilesDir = Join-Path $pkg "Editor\Devices\Profiles"
    Get-ChildItem $profilesDir -Filter "*.json" | ForEach-Object {
        $j = Get-Content $_.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        $short = [string]$j.shortName
        $screens = @()
        foreach ($s in $j.screens) {
            $pw = if ($s.portraitWidth) { [int]$s.portraitWidth } else { [int]$s.width }
            $ph = if ($s.portraitHeight) { [int]$s.portraitHeight } else { [int]$s.height }
            $lw = if ($s.landscapeWidth) { [int]$s.landscapeWidth } else { $ph }
            $lh = if ($s.landscapeHeight) { [int]$s.landscapeHeight } else { $pw }
            $screens += @{
                Name = [string]$s.name
                W = $pw
                H = $ph
                LW = $lw
                LH = $lh
                Cover = [bool]$s.coverOrOuter
            }
        }
        if ($screens.Count -gt 0) {
            $devices[$short] = $screens
        }
    }
    return $devices
}

function Preferred-Index($screens, $preferInner) {
    if (-not $preferInner) {
        for ($i = 0; $i -lt $screens.Count; $i++) {
            if ($screens[$i].Cover) { return $i }
        }
        return 0
    }
    for ($i = 0; $i -lt $screens.Count; $i++) {
        if (-not $screens[$i].Cover) { return $i }
    }
    return 0
}

function Resolve-ForProduct($screens, $product) {
    $preferInner = $product -ne "Closed"
    $idx = Preferred-Index $screens $preferInner
    return $screens[$idx]
}

Write-Host "=== 1) Catalog parse + expected sizes ===" -ForegroundColor Cyan
$catalog = Parse-Catalog
foreach ($name in $Expected.Keys) {
    Assert-True ($catalog.ContainsKey($name)) "catalog has $name"
    if (-not $catalog.ContainsKey($name)) { continue }
    foreach ($product in @("Closed", "Opened", "HalfOpened")) {
        $exp = $Expected[$name][$product]
        $got = Resolve-ForProduct $catalog[$name] $product
        Assert-Eq $got.Name $exp.Name "$name $product screen name"
        Assert-Eq $got.W $exp.W "$name $product width"
        Assert-Eq $got.H $exp.H "$name $product height"
    }
}

Write-Host "`n=== 1b) Explicit portrait / landscape pairs ===" -ForegroundColor Cyan
foreach ($name in $catalog.Keys) {
    foreach ($s in $catalog[$name]) {
        Assert-True ($s.W -gt 0 -and $s.H -gt 0) "$name $($s.Name) portrait size set"
        Assert-True ($s.LW -gt 0 -and $s.LH -gt 0) "$name $($s.Name) landscape size set"
        Assert-Eq $s.LW $s.H "$name $($s.Name) landscapeWidth == portraitHeight"
        Assert-Eq $s.LH $s.W "$name $($s.Name) landscapeHeight == portraitWidth"
        if ($s.W -ne $s.H) {
            Assert-True ($s.W -le $s.H) "$name $($s.Name) portrait is upright (W<=H)"
            Assert-True ($s.LW -ge $s.LH) "$name $($s.Name) landscape is wide (W>=H)"
        }
    }
}

Write-Host "`n=== 2) .device files match catalog ===" -ForegroundColor Cyan
$fileMap = @{
    "Pixel Fold-like" = "Pixel Fold-like.device"
    "Galaxy Z Flip-like" = "Galaxy Z Flip-like.device"
    "Galaxy Z Fold6-like" = "Galaxy Z Fold6-like.device"
    "Huawei Mate X3-like" = "Huawei Mate X3-like.device"
    "Honor Magic V2-like" = "Honor Magic V2-like.device"
    "OnePlus Open-like" = "OnePlus Open-like.device"
    "Xiaomi Mix Fold3-like" = "Xiaomi Mix Fold3-like.device"
    "Motorola Razr+-like" = "Motorola Razr+-like.device"
    "Galaxy Z Fold7-like" = "Galaxy Z Fold7-like.device"
    "Galaxy Z Flip7-like" = "Galaxy Z Flip7-like.device"
    "Pixel 9 Pro Fold-like" = "Pixel 9 Pro Fold-like.device"
    "Oppo Find N5-like" = "Oppo Find N5-like.device"
    "Honor Magic V3-like" = "Honor Magic V3-like.device"
    "Motorola Razr 50 Ultra-like" = "Motorola Razr 50 Ultra-like.device"
    "Huawei Mate XT-like" = "Huawei Mate XT-like.device"
    "Vivo X Fold3 Pro-like" = "Vivo X Fold3 Pro-like.device"
    "Xiaomi Mix Fold4-like" = "Xiaomi Mix Fold4-like.device"
    "Galaxy Z Fold8 Ultra-like" = "Galaxy Z Fold8 Ultra-like.device"
    "Galaxy Z Fold8-like" = "Galaxy Z Fold8-like.device"
    "Galaxy Z Flip8-like" = "Galaxy Z Flip8-like.device"
    "Galaxy Z TriFold-like" = "Galaxy Z TriFold-like.device"
    "Galaxy Z Flip7 FE-like" = "Galaxy Z Flip7 FE-like.device"
    "Galaxy Z Fold Special Edition-like" = "Galaxy Z Fold Special Edition-like.device"
    "Galaxy Z Flip6-like" = "Galaxy Z Flip6-like.device"
    "Galaxy Z Flip5-like" = "Galaxy Z Flip5-like.device"
    "Galaxy Z Fold5-like" = "Galaxy Z Fold5-like.device"
    "Galaxy Z Fold2-like" = "Galaxy Z Fold2-like.device"
    "iPhone Duo-like" = "iPhone Duo-like.device"
}
# iOS skins (generate-device-skins.ps1 New-IosOrientationBlock) use navigationBarHeight 0 and
# carry top + bottom pixel insets instead: portrait safe height = H - (top + bottom).
$iosPortraitInsets = @{
    "iPhone Duo-like" = @{ Cover = 177 + 102; Inner = 63 + 102 }
}
foreach ($name in $fileMap.Keys) {
    $path = Join-Path $pkg "Editor\Devices\$($fileMap[$name])"
    $j = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json
    $screens = $catalog[$name]
    Assert-Eq $j.screens.Count $screens.Count "$name screen count (.device vs catalog)"
    for ($i = 0; $i -lt $screens.Count; $i++) {
        $s = $j.screens[$i]
        $c = $screens[$i]
        Assert-Eq $s.presentation.name $c.Name "$name[$i] name"
        Assert-Eq ([int]$s.width) $c.W "$name[$i] width"
        Assert-Eq ([int]$s.height) $c.H "$name[$i] height"
        # Safe area portrait must match width and height-nav (iOS: height - top/bottom insets)
        $nav = [int]$s.navigationBarHeight
        if ($iosPortraitInsets.ContainsKey($name)) { $nav += [int]$iosPortraitInsets[$name][$c.Name] }
        $portrait = $s.orientations | Where-Object { [int]$_.orientation -eq 1 } | Select-Object -First 1
        Assert-Eq ([int]$portrait.safeArea.width) $c.W "$name[$i] portrait safe width"
        Assert-Eq ([int]$portrait.safeArea.height) ([Math]::Max(1, $c.H - $nav)) "$name[$i] portrait safe height"
    }
}

Write-Host "`n=== 3) Stale-cover race simulation ===" -ForegroundColor Cyan
# Mimic: user publishes Opened while Simulator still reports Cover
foreach ($name in @("Galaxy Z Fold8-like", "Pixel Fold-like", "Galaxy Z Flip-like")) {
    $screens = $catalog[$name]
    $product = "Opened"
    $want = Resolve-ForProduct $screens $product
    $stale = Resolve-ForProduct $screens "Closed"   # Simulator still on cover/single
    # Session logic: product stays Opened; metrics come from expected screen when cover mismatches
    $wantCover = $false
    $cover = [bool]$stale.Cover
    $w = $stale.W; $h = $stale.H; $screenName = $stale.Name
    if ($cover -ne $wantCover) {
        $w = $want.W; $h = $want.H; $screenName = $want.Name
    }
    Assert-Eq $screenName $want.Name "$name race keeps Opened screen ($screenName)"
    Assert-Eq $w $want.W "$name race width"
    Assert-Eq $h $want.H "$name race height"
}

Write-Host "`n=== 4) PublishProductState metric override (stale args ignored) ===" -ForegroundColor Cyan
foreach ($name in $Expected.Keys) {
    $screens = $catalog[$name]
    # Caller passes stale Closed metrics while requesting HalfOpened
    $stale = $Expected[$name].Closed
    $want = $Expected[$name].HalfOpened
    $resolved = Resolve-ForProduct $screens "HalfOpened"
    Assert-Eq $resolved.W $want.W "$name HalfOpened publish width (ignore stale $($stale.W))"
    Assert-Eq $resolved.H $want.H "$name HalfOpened publish height"
}

Write-Host "`n=== 5) Simulator uses catalog metrics (no hardcoded presets) ===" -ForegroundColor Cyan
$sim = Get-Content (Join-Path $pkg "Runtime\FoldableSimulator.cs") -Raw
$resolverSrc = Get-Content (Join-Path $pkg "Runtime\FoldableProductStateResolver.cs") -Raw
Assert-True ($sim -match 'GetProductMetrics') "Simulator uses shared product metrics"
Assert-True ($sim -match 'ApplyCatalogPairForKind') "Simulator catalog pair helper"
Assert-True ($sim -match 'SyncOverlayFromDetector') "Simulator overlay follows detector after Flat restore"
Assert-True ($sim -notmatch 'DEVELOPMENT_BUILD') "Simulator does not apply on Android DEVELOPMENT_BUILD"
Assert-True ($sim -notmatch 'public void SetPreset') "removed SetPreset hardcodes stay absent"
Assert-True ($resolverSrc -match '720 : 1080') "resolver Flip Cover/Main widths"
Assert-True ($resolverSrc -match '748 : 2640') "resolver Flip heights"
Assert-True ($resolverSrc -match '675 : 1350') "resolver Dual widths"
Assert-True ($resolverSrc -match '832 : 1848') "resolver ZFold widths"
Assert-True ($sim -match 'SyncMetricsForProduct') "Simulator swaps Cover/Main on product change"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableDeviceCatalogWindow.cs")) "Device Catalog window exists"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceCatalogWindow.cs") -Raw) -match 'Use in Simulator') "Device Catalog syncs Simulator"

Write-Host "`n=== 6) Session pipeline source checks ===" -ForegroundColor Cyan
$session = Get-Content (Join-Path $pkg "Editor\FoldableEditorSession.cs") -Raw
Assert-True ($session -match 'ResolveScreenForProduct') "session has ResolveScreenForProduct"
Assert-True ($session -match 'stale Cover WxH after switching to Expand') "session documents stale-cover guard"
Assert-True ($session -match 'Session product state wins') "BuildLayout keeps product state"
Assert-True ($session -match 'preferInner = product != FoldableProductState\.Closed') "Closed→cover Opened/Half→inner"
Assert-True ($session -match 'IsLandscape') "session tracks landscape for all states"

$resolver = Get-Content (Join-Path $pkg "Runtime\FoldableProductStateResolver.cs") -Raw
$coverHeuristics = Get-Content (Join-Path $pkg "Runtime\FoldableCoverHeuristics.cs") -Raw
Assert-True ($resolver -match 'if \(layout\.productState == FoldableProductState\.Undefined\)') "Enrich does not overwrite explicit product state"
Assert-True ($resolver -match 'ShouldPreserveHalfOpened') "Enrich keeps HalfOpened when FoldingFeature is missing"
Assert-True ($coverHeuristics -match 'isCoverScreen = false') "Enrich clears cover for Opened/HalfOpened"
Assert-True ($coverHeuristics -match 'CoverAreaRatio') "cover detection uses area ratio"
Assert-True ($coverHeuristics -match 'peakMaxMetrics|IsCurrentMuchSmallerThanReference') "cover detection uses session peak / reference"
Assert-True ($coverHeuristics -match 'IsSmallerThanKindOpenMetrics') "cover detection uses Device Kind open size"
Assert-True ($resolver -notmatch 'IsFoldFormFactor') "no unsafe no-FoldingFeature=Closed form-factor shortcut"
Assert-True ($resolver -match 'DualScreen') "dual-screen single-pane Closed detection"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\FoldableProxy.cs") -Raw) -match 'peakMaxMetrics') "proxy maps peak metrics"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\FoldableProxy.cs") -Raw) -match 'largestDisplayMetrics') "proxy maps largest display"

Write-Host "`n=== 6b) Native product-state detection (mirrored Enrich) ===" -ForegroundColor Cyan
function Test-MuchSmaller($cw, $ch, $rw, $rh) {
    $cur = [long]$cw * $ch
    $ref = [long]$rw * $rh
    if ($ref -gt 0 -and $cur -gt 0 -and $cur -lt ($ref * 0.70)) { return $true }
    $cShort = [Math]::Min($cw, $ch); $cLong = [Math]::Max($cw, $ch)
    $rShort = [Math]::Min($rw, $rh); $rLong = [Math]::Max($rw, $rh)
    $narrower = $cShort -lt ($rShort * 0.75)
    if ($narrower -and ($cLong -le ($rLong * 1.2)) -and ($cur -lt ($ref * 0.85))) { return $true }
    return ($narrower -and $cLong -lt ($rLong * 0.9))
}

function Resolve-NativeProduct($kind, $state, $separating, $cw, $ch, $mw, $mh, $rearActive = $false, $peakW = 0, $peakH = 0, $dispW = 0, $dispH = 0) {
    # Live Jetpack FLAT / HALF_OPENED own posture — never size-probe into Closed.
    if ($state -eq "Flat") {
        if ($kind -eq "DualScreen" -and -not $separating) { return "Closed" }
        return "Opened"
    }
    if ($state -eq "HalfOpened") { return "HalfOpened" }

    $isCover = [bool]$rearActive
    if (-not $isCover) {
        $refW = $mw; $refH = $mh; $best = [long]$mw * $mh
        $peakA = [long]$peakW * $peakH
        if ($peakA -gt $best) { $best = $peakA; $refW = $peakW; $refH = $peakH }
        $dispA = [long]$dispW * $dispH
        if ($dispA -gt $best) { $best = $dispA; $refW = $dispW; $refH = $dispH }
        if ($best -gt 0 -and (Test-MuchSmaller $cw $ch $refW $refH)) { $isCover = $true }
    }
    if (-not $isCover -and $state -eq "Undefined") {
        if ($kind -eq "FoldablePhone" -and (Test-MuchSmaller $cw $ch 1848 2448)) { $isCover = $true }
        elseif ($kind -eq "FlipablePhone" -and (Test-MuchSmaller $cw $ch 1080 2640)) { $isCover = $true }
        elseif ($cw -gt 0 -and $ch -gt 0) {
            $long = [Math]::Max($cw, $ch)
            $short = [Math]::Min($cw, $ch)
            if ($long -le 1200 -and $short -le 900 -and (($long / [Math]::Max(1, $short)) -lt 1.35)) {
                $isCover = $true
            }
        }
    }
    if ($isCover) { return "Closed" }
    if ($kind -eq "DualScreen" -and -not $separating) { return "Closed" }
    return "Opened"
}

Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Undefined" $false 832 2268 832 2268) "Closed" "Fold cover via Device Kind open-size ratio"
Assert-Eq (Resolve-NativeProduct "Unknown" "Undefined" $false 832 2268 832 2268) "Opened" "Fold-sized panel without Device Kind is NOT Closed"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Undefined" $false 832 2268 832 2268 $false 1812 2176) "Closed" "Fold cover via session peak"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Undefined" $false 968 2376 968 2376 $false 0 0 1856 2160) "Closed" "Fold cover via largest display"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Undefined" $false 832 2268 832 2268 $true) "Closed" "Fold rear WindowArea ACTIVE → Closed"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Flat" $false 1812 2176 1812 2176) "Opened" "Fold Expand (FLAT)"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Flat" $false 832 2268 832 2268 $false 1812 2176) "Opened" "Live FLAT is not size-probed to Closed (stale feature cleared in Java)"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "Undefined" $false 832 2268 832 2268) "Closed" "Missing FoldingFeature + cover WxH after Expand is Closed"
Assert-Eq (Resolve-NativeProduct "FoldablePhone" "HalfOpened" $false 1812 2176 1812 2176) "HalfOpened" "Fold Expand Half Opened"
Assert-Eq (Resolve-NativeProduct "FlipablePhone" "Undefined" $false 720 748 720 748) "Closed" "Flip cover via kind open-size or small panel"
Assert-Eq (Resolve-NativeProduct "FlipablePhone" "Flat" $false 1080 2640 1080 2640) "Opened" "Flip Opened (FLAT)"
Assert-Eq (Resolve-NativeProduct "FlipablePhone" "HalfOpened" $false 1080 2640 1080 2640) "HalfOpened" "Flip Half Opened"
Assert-Eq (Resolve-NativeProduct "DualScreen" "Flat" $false 675 1800 1350 1800) "Closed" "Dual single pane Flat"
Assert-Eq (Resolve-NativeProduct "DualScreen" "Undefined" $false 675 1800 675 1800) "Closed" "Dual single pane no FoldingFeature"
Assert-Eq (Resolve-NativeProduct "DualScreen" "Flat" $true 1350 1800 1350 1800) "Opened" "Dual spanned"
Assert-Eq (Resolve-NativeProduct "DualScreen" "HalfOpened" $false 1350 1800 1350 1800) "HalfOpened" "Dual Flex"
Assert-Eq (Resolve-NativeProduct "Unknown" "Undefined" $false 1080 2400 1080 2400) "Opened" "regular phone not forced Closed"

Write-Host "`n=== 7) Posture math (HalfOpened tabletop/book) ===" -ForegroundColor Cyan
function New-Sim($state, $orient, $w, $h, $hinge, $thick, $sep = $false, $product = "") {
    [PSCustomObject]@{
        state = $state; orientation = $orient; width = $w; height = $h
        hingeNormalized = $hinge; hingeThicknessPx = $thick; isSeparating = $sep
        productState = $product
    }
}
function Resolve-Posture($s) {
    if ($s.productState -eq "Opened" -or $s.productState -eq "Closed") { return "Flat" }
    if ($s.state -eq "HalfOpened" -or $s.productState -eq "HalfOpened") {
        if ($s.orientation -eq "Horizontal") { return "Tabletop" }
        return "Book"
    }
    if ($s.isSeparating -and $s.state -eq "Flat") {
        if ($s.orientation -eq "Vertical") { return "Book" }
        return "DualPane"
    }
    return "Flat"
}
function Hinge-Y($h, $hinge, $thick) {
    return [Math]::Max(0, [Math]::Min([int][Math]::Round($h * $hinge) - [int]($thick / 2), $h - $thick))
}
Assert-Eq (Resolve-Posture (New-Sim "HalfOpened" "Horizontal" 1812 2176 0.5 24)) "Tabletop" "Fold HalfOpened → Tabletop"
Assert-Eq (Resolve-Posture (New-Sim "HalfOpened" "Vertical" 1812 2176 0.5 24)) "Book" "Fold HalfOpened+V → Book"
Assert-Eq (Resolve-Posture (New-Sim "HalfOpened" "Horizontal" 1080 2640 0.5 24)) "Tabletop" "Flip HalfOpened → Tabletop"
Assert-Eq (Resolve-Posture (New-Sim "Flat" "Vertical" 1350 1800 0.5 24 $true "Opened")) "Flat" "Dual Spanned Opened → Flat"
Assert-Eq (Resolve-Posture (New-Sim "Flat" "Vertical" 1350 1800 0.5 24 $true)) "Book" "Raw Flat+sep+V → Book"
Assert-Eq (Resolve-Posture (New-Sim "Flat" "Horizontal" 1080 2316 0.5 24 $false "Opened")) "Flat" "Generic Opened → Flat"
$foldHy = Hinge-Y 2176 0.5 24
Assert-True ($foldHy -ge 1050 -and $foldHy -le 1100) "ZFold Inner hinge Y mid ($foldHy)"
$flipHy = Hinge-Y 2640 0.5 24
Assert-True ($flipHy -ge 1290 -and $flipHy -le 1340) "ZFlip6 Main hinge Y mid ($flipHy)"
$pixelHx = [Math]::Max(0, [Math]::Min([int][Math]::Round(1840 * 0.5) - 12, 1840 - 24))
Assert-True ($pixelHx -ge 880 -and $pixelHx -le 940) "Pixel Inner book hinge X mid ($pixelHx)"

Write-Host "`n=== 8) Full device × state matrix dump ===" -ForegroundColor Cyan
$matrix = @()
foreach ($name in ($Expected.Keys | Sort-Object)) {
    foreach ($product in @("Closed", "Opened", "HalfOpened")) {
        $exp = $Expected[$name][$product]
        $got = Resolve-ForProduct $catalog[$name] $product
        $ok = ($got.Name -eq $exp.Name -and $got.W -eq $exp.W -and $got.H -eq $exp.H)
        $matrix += [PSCustomObject]@{
            Device = $name
            State = $product
            Screen = "$($got.Name) $($got.W)x$($got.H)"
            OK = $ok
        }
        Assert-True $ok "matrix $name / $product → $($got.Name) $($got.W)x$($got.H)"
    }
}
$matrix | Format-Table -AutoSize | Out-String | Write-Host

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
if ($failed -gt 0) { exit 1 }
exit 0
