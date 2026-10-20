# Read-only profile/schema/resolution audit for PhonePad (no Unity required).
# This script never writes profiles, .device files, overlays, or metadata.

$ErrorActionPreference = "Stop"
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
$profilesDir = Join-Path $pkg "Editor\Devices\Profiles"
$failed = 0
$passed = 0

function Assert-True($condition, [string]$message) {
    if ($condition) {
        Write-Host "PASS  $message" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "FAIL  $message" -ForegroundColor Red
        $script:failed++
    }
}

function Has-Text($value) {
    return -not [string]::IsNullOrWhiteSpace([string]$value)
}

$expectedIds = @(
    "galaxy-a35", "galaxy-a36", "galaxy-a55", "galaxy-a56",
    "galaxy-s24", "galaxy-s24-plus", "galaxy-s24-ultra",
    "galaxy-s25", "galaxy-s25-edge", "galaxy-s25-fe", "galaxy-s25-plus", "galaxy-s25-ultra",
    "galaxy-tab-a9-plus", "galaxy-tab-s10-fe", "galaxy-tab-s10-fe-plus",
    "galaxy-tab-s10-plus", "galaxy-tab-s10-ultra",
    "galaxy-tab-s9", "galaxy-tab-s9-plus", "galaxy-tab-s9-ultra",
    "huawei-mate-60-pro", "huawei-mate-70-pro", "huawei-matepad-11-5",
    "huawei-matepad-pro-12-2", "huawei-matepad-pro-13-2", "huawei-p60-pro",
    "huawei-pura-70", "huawei-pura-70-pro", "huawei-pura-70-ultra", "huawei-pura-80-pro",
    "ipad-10", "ipad-a16", "ipad-air-11", "ipad-air-13", "ipad-mini-7",
    "ipad-pro-11-m4", "ipad-pro-11-m5", "ipad-pro-12-9", "ipad-pro-13-m4", "ipad-pro-13-m5",
    "iphone-15", "iphone-15-plus", "iphone-15-pro", "iphone-15-pro-max",
    "iphone-16", "iphone-16e", "iphone-16-plus", "iphone-16-pro", "iphone-16-pro-max",
    "iphone-17", "iphone-17-air", "iphone-17-pro", "iphone-17-pro-max",
    "motorola-edge-40", "motorola-edge-50-pro", "motorola-edge-50-ultra",
    "motorola-edge-60", "motorola-edge-60-pro",
    "oneplus-12", "oneplus-13", "oneplus-13r", "oneplus-nord-4", "oneplus-nord-5",
    "oneplus-pad-2", "oneplus-pad-3",
    "pixel-10", "pixel-10a", "pixel-10-pro", "pixel-10-pro-xl",
    "pixel-8", "pixel-8a", "pixel-8-pro", "pixel-9", "pixel-9a", "pixel-9-pro",
    "pixel-9-pro-xl", "pixel-tablet",
    "poco-f6", "poco-f7", "redmi-note-13-pro", "redmi-note-14-pro",
    "redmi-note-14-pro-plus", "redmi-pad-pro",
    "xiaomi-14", "xiaomi-14-ultra", "xiaomi-15", "xiaomi-15-pro", "xiaomi-15-ultra",
    "xiaomi-pad-6", "xiaomi-pad-6-pro", "xiaomi-pad-7", "xiaomi-pad-7-pro"
)

$allowedFields = @(
    "id", "shortName", "brand", "deviceModel", "kind",
    "portraitWidth", "portraitHeight", "landscapeWidth", "landscapeHeight",
    "dpi", "diagonalInches", "basedOn", "notes",
    "verifiedDate", "verifiedStatus", "deviceFile", "sources"
)

Write-Host "=== PhonePad profile identity ===" -ForegroundColor Cyan
$profileFiles = @(Get-ChildItem $profilesDir -Filter "*.json" -File | Sort-Object Name)
$actualIds = @($profileFiles | ForEach-Object { $_.BaseName })
Assert-True ($actualIds.Count -eq $expectedIds.Count) "profile count is locked at $($expectedIds.Count)"
Assert-True (@(Compare-Object $expectedIds $actualIds).Count -eq 0) "profile ID set is unchanged"

$seenIds = @{}
foreach ($file in $profileFiles) {
    $label = $file.BaseName
    $profile = $null
    try {
        $profile = Get-Content $file.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-True $true "$label profile JSON parses"
    } catch {
        Assert-True $false "$label profile JSON parses"
        continue
    }

    Write-Host "`n--- $label ---" -ForegroundColor DarkCyan
    $fieldNames = @($profile.PSObject.Properties.Name)
    $missingFields = @($allowedFields | Where-Object { $_ -notin $fieldNames })
    $unknownFields = @($fieldNames | Where-Object { $_ -notin $allowedFields })
    Assert-True ($missingFields.Count -eq 0) "$label has complete schema"
    Assert-True ($unknownFields.Count -eq 0) "$label has no unknown schema fields"

    $id = [string]$profile.id
    Assert-True ($id -eq $label) "$label id matches filename"
    Assert-True ($id -match '^[a-z0-9]+(?:-[a-z0-9]+)*$') "$label id is kebab-case"
    Assert-True (-not $seenIds.ContainsKey($id)) "$label id is unique"
    $seenIds[$id] = $true

    Assert-True (Has-Text $profile.shortName) "$label shortName"
    Assert-True (Has-Text $profile.brand) "$label brand"
    Assert-True (Has-Text $profile.deviceModel) "$label deviceModel"
    Assert-True ($profile.kind -in @("Phone", "Pad")) "$label kind is Phone or Pad"
    Assert-True (Has-Text $profile.basedOn) "$label basedOn"
    Assert-True ($profile.verifiedStatus -in @("matched", "approx")) "$label verification status"
    Assert-True ([string]$profile.verifiedDate -match '^\d{4}-\d{2}-\d{2}$') "$label verification date format"

    $pw = [int]$profile.portraitWidth
    $ph = [int]$profile.portraitHeight
    $lw = [int]$profile.landscapeWidth
    $lh = [int]$profile.landscapeHeight
    Assert-True ($pw -ge 8 -and $ph -ge 8) "$label portrait resolution is positive"
    Assert-True ($pw -le $ph) "$label portrait resolution is upright"
    Assert-True ($lw -eq $ph -and $lh -eq $pw) "$label landscape resolution is exact portrait swap"
    Assert-True ([double]$profile.dpi -gt 0) "$label dpi is positive"
    Assert-True ([double]$profile.diagonalInches -gt 0) "$label diagonal is positive"

    $sources = @($profile.sources)
    Assert-True ($sources.Count -ge 1) "$label has source evidence"
    foreach ($source in $sources) {
        Assert-True (Has-Text $source.title) "$label source title"
        Assert-True ([string]$source.url -match '^https://') "$label source uses HTTPS"
        Assert-True (Has-Text $source.quote) "$label source quote"
    }

    $expectedDeviceFile = "Editor/Devices/$id.device"
    Assert-True ([string]$profile.deviceFile -eq $expectedDeviceFile) "$label deviceFile follows stable ID"
    $devicePath = Join-Path $pkg ($expectedDeviceFile -replace '/', [IO.Path]::DirectorySeparatorChar)
    if (-not (Test-Path $devicePath -PathType Leaf)) {
        Write-Host "INFO  $label .device is not enabled; profile resolution was audited" -ForegroundColor DarkGray
        continue
    }

    $bytes = [IO.File]::ReadAllBytes($devicePath)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    Assert-True (-not $hasBom) "$label .device is BOM-free"

    try {
        $device = Get-Content $devicePath -Raw -Encoding UTF8 | ConvertFrom-Json
        Assert-True $true "$label .device JSON parses"
    } catch {
        Assert-True $false "$label .device JSON parses"
        continue
    }

    Assert-True ([int]$device.version -eq 1) "$label .device schema version"
    Assert-True (@($device.screens).Count -eq 1) "$label has one non-foldable screen"
    $screen = @($device.screens)[0]
    Assert-True ([int]$screen.width -eq $pw -and [int]$screen.height -eq $ph) "$label profile/.device portrait resolution matches"
    Assert-True ([Math]::Abs([double]$screen.dpi - [double]$profile.dpi) -lt 0.01) "$label profile/.device dpi matches"
    Assert-True ([string]$screen.presentation.name -eq "Main") "$label screen name is Main"
    Assert-True ([string]$device.systemInfo.deviceModel -eq [string]$profile.deviceModel) "$label profile/.device model matches"

    $orientations = @($screen.orientations)
    $orientationIds = @($orientations | ForEach-Object { [int]$_.orientation } | Sort-Object)
    Assert-True ($orientations.Count -eq 4 -and ($orientationIds -join ",") -eq "1,2,3,4") "$label has four unique orientations"
    foreach ($orientation in $orientations) {
        $landscape = [int]$orientation.orientation -in @(3, 4)
        $frameWidth = if ($landscape) { $lw } else { $pw }
        $frameHeight = if ($landscape) { $lh } else { $ph }
        $safe = $orientation.safeArea
        $inBounds =
            [double]$safe.x -ge 0 -and [double]$safe.y -ge 0 -and
            [double]$safe.width -gt 0 -and [double]$safe.height -gt 0 -and
            ([double]$safe.x + [double]$safe.width) -le $frameWidth -and
            ([double]$safe.y + [double]$safe.height) -le $frameHeight
        Assert-True $inBounds "$label orientation $($orientation.orientation) safe area is in ${frameWidth}x${frameHeight}"
    }
}

Write-Host "`n=== PhonePad audit summary ===" -ForegroundColor Cyan
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
if ($failed -gt 0) { exit 1 }
exit 0
