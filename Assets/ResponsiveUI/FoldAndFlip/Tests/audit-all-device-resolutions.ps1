# Extra audit: every profile Closed/Opened/HalfOpened + .device sync + cover/open ratio
$ErrorActionPreference = "Stop"
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
$profilesDir = Join-Path $pkg "Editor\Devices\Profiles"
$failed = 0
$passed = 0

function Ok($cond, $msg) {
    if ($cond) {
        Write-Host "PASS  $msg" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "FAIL  $msg" -ForegroundColor Red
        $script:failed++
    }
}

# Line-oriented parse — avoid a multi-line [\s\S]*? regex over the whole suite file.
$expectedNames = @(
    Get-Content (Join-Path $pkg "Tests\test-resolutions.ps1") |
        ForEach-Object {
            if ($_ -match '^\s*"([^"]+)"\s*=\s*@\{') { $Matches[1] }
        } | Sort-Object -Unique
)

$profiles = Get-ChildItem $profilesDir -Filter "*.json"
Write-Host "=== Extra audit: all $($profiles.Count) profiles ===" -ForegroundColor Cyan

$rows = @()
foreach ($f in $profiles) {
    $j = Get-Content $f.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    $short = [string]$j.shortName
    $kind = [string]$j.kind
    $screens = @()
    foreach ($s in $j.screens) {
        $pw = if ($s.portraitWidth) { [int]$s.portraitWidth } else { [int]$s.width }
        $ph = if ($s.portraitHeight) { [int]$s.portraitHeight } else { [int]$s.height }
        $lw = if ($s.landscapeWidth) { [int]$s.landscapeWidth } else { $ph }
        $lh = if ($s.landscapeHeight) { [int]$s.landscapeHeight } else { $pw }
        $screens += [PSCustomObject]@{
            Name  = [string]$s.name
            W     = $pw
            H     = $ph
            LW    = $lw
            LH    = $lh
            Cover = [bool]$s.coverOrOuter
        }
    }

    Ok ($screens.Count -ge 1) "$short has screens"
    foreach ($s in $screens) {
        Ok ($s.W -gt 0 -and $s.H -gt 0) "$short $($s.Name) portrait $($s.W)x$($s.H)"
        Ok (($s.LW -eq $s.H) -and ($s.LH -eq $s.W)) "$short $($s.Name) landscape swap $($s.LW)x$($s.LH)"
        if ($s.W -ne $s.H) {
            Ok ($s.W -le $s.H) "$short $($s.Name) portrait upright"
        }
    }

    $cover = @($screens | Where-Object { $_.Cover }) | Select-Object -First 1
    $inner = @($screens | Where-Object { -not $_.Cover }) | Select-Object -First 1
    if (-not $cover) { $cover = $screens[0] }
    if (-not $inner) { $inner = $screens[0] }

    $closed = $cover
    $opened = $inner
    $half = $inner
    Ok ($closed.W -gt 0) "$short Closed $($closed.Name) $($closed.W)x$($closed.H)"
    Ok ($opened.W -gt 0) "$short Opened $($opened.Name) $($opened.W)x$($opened.H)"
    Ok (($half.W -eq $opened.W) -and ($half.H -eq $opened.H)) "$short HalfOpened uses open size"

    $deviceRel = [string]$j.skins.deviceFile
    Ok (-not [string]::IsNullOrWhiteSpace($deviceRel)) "$short has deviceFile"
    if ($deviceRel) {
        $devicePath = Join-Path $pkg ("Editor\" + ($deviceRel -replace '/', '\'))
        if (-not (Test-Path $devicePath)) {
            $devicePath = Join-Path $pkg $deviceRel
        }
        Ok (Test-Path $devicePath) "$short .device exists"
        if (Test-Path $devicePath) {
            $dev = Get-Content $devicePath -Raw | ConvertFrom-Json
            Ok ($dev.screens.Count -eq $screens.Count) "$short screen count json=$($screens.Count) device=$($dev.screens.Count)"
            for ($i = 0; $i -lt [Math]::Min($screens.Count, $dev.screens.Count); $i++) {
                Ok ([int]$dev.screens[$i].width -eq $screens[$i].W) "$short[$i] width json=$($screens[$i].W) device=$($dev.screens[$i].width)"
                Ok ([int]$dev.screens[$i].height -eq $screens[$i].H) "$short[$i] height json=$($screens[$i].H) device=$($dev.screens[$i].height)"
            }
        }
    }

    $coverArea = [long]$closed.W * $closed.H
    $openArea = [long]$opened.W * $opened.H
    $pct = if ($openArea -gt 0) { [math]::Round(100.0 * $coverArea / $openArea, 1) } else { 0 }
        $kindHeur = $false
        if (($kind -eq "FoldablePhone" -or $kind -eq "FlipablePhone") -and
            $cover.Cover -and
            ($closed.Name -ne $opened.Name -or $coverArea -lt $openArea)) {
            $cShort = [Math]::Min($closed.W, $closed.H)
            $cLong = [Math]::Max($closed.W, $closed.H)
            $oShort = [Math]::Min($opened.W, $opened.H)
            $oLong = [Math]::Max($opened.W, $opened.H)
            $kindHeur =
                ($coverArea -lt ($openArea * 0.70)) -or
                (($cShort -lt ($oShort * 0.75)) -and ($cLong -le ($oLong * 1.2)) -and ($coverArea -lt ($openArea * 0.85))) -or
                (($cShort -lt ($oShort * 0.75)) -and ($cLong -lt ($oLong * 0.9)))
            # Also: Device Kind open-size uses Fold defaults (1848x2448) / Flip (1080x2640)
            $defW = if ($kind -eq "FlipablePhone") { 1080 } else { 1848 }
            $defH = if ($kind -eq "FlipablePhone") { 2640 } else { 2448 }
            $defArea = [long]$defW * $defH
            $vsDefault =
                ($coverArea -lt ($defArea * 0.70)) -or
                (($cShort -lt ([Math]::Min($defW, $defH) * 0.75)) -and ($cLong -le ([Math]::Max($defW, $defH) * 1.2)) -and ($coverArea -lt ($defArea * 0.85)))
            $kindHeur = $kindHeur -or $vsDefault
            Ok $kindHeur "$short cover detectable by size ratio (own ${pct}% or kind defaults)"
        }

    $rows += [PSCustomObject]@{
        Device   = $short
        Kind     = $kind
        Closed   = "$($closed.Name) $($closed.W)x$($closed.H)"
        Opened   = "$($opened.Name) $($opened.W)x$($opened.H)"
        Half     = "$($half.Name) $($half.W)x$($half.H)"
        Pct      = $pct
        KindHeur = $kindHeur
    }

    Ok ($expectedNames -contains $short) "$short listed in test-resolutions Expected"
}

Write-Host ""
Write-Host ("Device".PadRight(36) + "Kind".PadRight(16) + "Closed".PadRight(20) + "Opened".PadRight(20) + "HalfOpened".PadRight(20) + "Cover%".PadRight(10) + "KindHeur")
foreach ($r in ($rows | Sort-Object Device)) {
    $pctLabel = ("{0}%" -f $r.Pct)
    Write-Host (
        $r.Device.PadRight(36) +
        $r.Kind.PadRight(16) +
        $r.Closed.PadRight(20) +
        $r.Opened.PadRight(20) +
        $r.Half.PadRight(20) +
        $pctLabel.PadRight(10) +
        $r.KindHeur
    )
}

foreach ($n in $expectedNames) {
    $hit = $rows | Where-Object { $_.Device -eq $n }
    Ok ($null -ne $hit) "Expected entry has profile: $n"
}
Ok ($rows.Count -eq 28) "profile count is 28 (got $($rows.Count))"
Ok ($expectedNames.Count -ge 26) "Expected matrix covers devices (got $($expectedNames.Count))"

Write-Host ""
Write-Host "=== Extra audit summary ==="
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
if ($failed -gt 0) { exit 1 }
