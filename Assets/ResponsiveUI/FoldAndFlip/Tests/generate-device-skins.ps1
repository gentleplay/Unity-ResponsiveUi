# Generate Device Simulator .device skins + bezel overlays for Fold & Flip Responsive Kit.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File Assets/ResponsiveUI/FoldAndFlip/Tests/generate-device-skins.ps1
# One device only (keeps hand-tuned skins such as the Fold8 Design Canvas label):
#   ... generate-device-skins.ps1 -Only "iPhone Duo-like"

param([string[]]$Only = @())

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
$devicesDir = Join-Path $pkg "Editor\Devices"
$overlayDir = Join-Path $devicesDir "DeviceOverlays"

function Get-DeterministicGuidHex([string]$path) {
    $relative = $path.Substring($pkg.Path.Length).TrimStart('\', '/').Replace('\', '/').ToLowerInvariant()
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.Encoding]::UTF8.GetBytes("FoldAndFlipResponsiveKit/" + $relative)
        $hash = $sha.ComputeHash($bytes)
        return (($hash | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 32)
    } finally {
        $sha.Dispose()
    }
}

function Write-DeviceMeta([string]$path) {
    # Never churn a committed Unity GUID. Missing metas use a stable path-derived GUID.
    if (Test-Path $path) { return }
    $guid = Get-DeterministicGuidHex $path
    @"
fileFormatVersion: 2
guid: $guid
ScriptedImporter:
  internalIDToNameTable: []
  externalObjects: {}
  serializedVersion: 2
  userData: 
  assetBundleName: 
  assetBundleVariant: 
  script: {fileID: 13973, guid: 0000000000000000e000000000000000, type: 0}
"@ | Set-Content -Path $path -Encoding ASCII
}

function Write-OverlayMeta([string]$path) {
    # Preserve existing references; deterministic generation is only for a new asset.
    if (Test-Path $path) { return }
    $guid = Get-DeterministicGuidHex $path
    $template = Get-Content (Join-Path $overlayDir "GalaxyZFold8Like_Inner_Overlay.png.meta") -Raw
    $template = $template -replace 'guid: [a-f0-9]+', "guid: $guid"
    Set-Content -Path $path -Value $template -Encoding ASCII -NoNewline
}

function New-BezelOverlay([string]$outPath, [int]$screenW, [int]$screenH, [int]$border, [int]$radius) {
    $ow = $screenW + $border * 2
    $oh = $screenH + $border * 2
    $bmp = New-Object System.Drawing.Bitmap $ow, $oh, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = [Math]::Max(2, $radius * 2)
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($ow - $d, 0, $d, $d, 270, 90)
    $path.AddArc($ow - $d, $oh - $d, $d, $d, 0, 90)
    $path.AddArc(0, $oh - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(230, 28, 30, 34))
    $g.FillPath($brush, $path)

    # Transparent screen hole
    $hole = New-Object System.Drawing.Drawing2D.GraphicsPath
    $hr = [Math]::Max(2, [int]($radius * 0.75))
    $hd = $hr * 2
    $sx = $border
    $sy = $border
    $hole.AddArc($sx, $sy, $hd, $hd, 180, 90)
    $hole.AddArc($sx + $screenW - $hd, $sy, $hd, $hd, 270, 90)
    $hole.AddArc($sx + $screenW - $hd, $sy + $screenH - $hd, $hd, $hd, 0, 90)
    $hole.AddArc($sx, $sy + $screenH - $hd, $hd, $hd, 90, 90)
    $hole.CloseFigure()
    $g.SetClip($hole)
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.ResetClip()

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(90, 255, 255, 255), 2)
    $g.DrawPath($pen, $path)

    $bmp.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $brush.Dispose(); $pen.Dispose(); $path.Dispose(); $hole.Dispose(); $bmp.Dispose()
}

function New-OrientationBlock([int]$w, [int]$h, [int]$nav) {
    $safeH = [Math]::Max(1, $h - $nav)
    $safeWLand = [Math]::Max(1, $h - $nav)
    @(
        @{ orientation = 1; safeArea = @{ serializedVersion = "2"; x = 0; y = 0; width = $w; height = $safeH }; cutouts = @() },
        @{ orientation = 2; safeArea = @{ serializedVersion = "2"; x = 0; y = $nav; width = $w; height = $safeH }; cutouts = @() },
        @{ orientation = 3; safeArea = @{ serializedVersion = "2"; x = $nav; y = 0; width = $safeWLand; height = $w }; cutouts = @() },
        @{ orientation = 4; safeArea = @{ serializedVersion = "2"; x = 0; y = 0; width = $h; height = $w }; cutouts = @() }
    )
}

# iOS has no navigation bar: sensor housing at the top and home indicator at the bottom
# in portrait; symmetric side insets plus a small bottom inset in both landscape
# orientations (Unity safe areas are bottom-left origin). Insets are pixels.
function New-IosOrientationBlock([int]$w, [int]$h, [int]$top, [int]$bottom, [int]$side, [int]$landBottom) {
    $safeH = [Math]::Max(1, $h - $top - $bottom)
    $safeWLand = [Math]::Max(1, $h - 2 * $side)
    $safeHLand = [Math]::Max(1, $w - $landBottom)
    @(
        @{ orientation = 1; safeArea = @{ serializedVersion = "2"; x = 0; y = $bottom; width = $w; height = $safeH }; cutouts = @() },
        @{ orientation = 2; safeArea = @{ serializedVersion = "2"; x = 0; y = $top; width = $w; height = $safeH }; cutouts = @() },
        @{ orientation = 3; safeArea = @{ serializedVersion = "2"; x = $side; y = $landBottom; width = $safeWLand; height = $safeHLand }; cutouts = @() },
        @{ orientation = 4; safeArea = @{ serializedVersion = "2"; x = $side; y = $landBottom; width = $safeWLand; height = $safeHLand }; cutouts = @() }
    )
}

function New-ScreenObj($name, $w, $h, $border, $radius, $overlayRel, $nav, $dpi, $ios) {
    $orientations = if ($ios) {
        New-IosOrientationBlock $w $h $ios.Top $ios.Bottom $ios.Side $ios.LandBottom
    } else {
        New-OrientationBlock $w $h $nav
    }
    @{
        width = $w
        height = $h
        navigationBarHeight = $(if ($ios) { 0 } else { $nav })
        dpi = $dpi
        presentation = @{
            name = $name
            borderSize = @{ x = $border; y = $border; z = $border; w = $border }
            cornerRadius = $radius
            overlayPath = $overlayRel
        }
        orientations = $orientations
    }
}

# Specs are "-like" approximations of public device resolutions (portrait WxH).
# Includes prior OEM skins + latest 2024–2025 models; re-run is idempotent.
$NewDevices = @(
    # --- previous OEM batch ---
    @{
        File = "Galaxy Z Fold6-like.device"
        Short = "Galaxy Z Fold6-like"
        Model = "gamedev fold6-like"
        OverlayBase = "GalaxyZFold6Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 968; H = 2376; Border = 48; Radius = 48; Nav = 120; Dpi = 410 },
            @{ Name = "Inner"; W = 1856; H = 2160; Border = 72; Radius = 72; Nav = 120; Dpi = 374 }
        )
    },
    @{
        File = "Huawei Mate X3-like.device"
        Short = "Huawei Mate X3-like"
        Model = "gamedev mate-x3-like"
        OverlayBase = "HuaweiMateX3Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2504; Border = 48; Radius = 48; Nav = 120; Dpi = 426 },
            @{ Name = "Inner"; W = 2224; H = 2496; Border = 72; Radius = 72; Nav = 120; Dpi = 426 }
        )
    },
    @{
        File = "Honor Magic V2-like.device"
        Short = "Honor Magic V2-like"
        Model = "gamedev magic-v2-like"
        OverlayBase = "HonorMagicV2Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1060; H = 2376; Border = 48; Radius = 48; Nav = 120; Dpi = 402 },
            @{ Name = "Inner"; W = 2156; H = 2344; Border = 72; Radius = 72; Nav = 120; Dpi = 402 }
        )
    },
    @{
        File = "OnePlus Open-like.device"
        Short = "OnePlus Open-like"
        Model = "gamedev oneplus-open-like"
        OverlayBase = "OnePlusOpenLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1116; H = 2484; Border = 48; Radius = 48; Nav = 120; Dpi = 420 },
            @{ Name = "Inner"; W = 2268; H = 2440; Border = 72; Radius = 72; Nav = 120; Dpi = 426 }
        )
    },
    @{
        File = "Xiaomi Mix Fold3-like.device"
        Short = "Xiaomi Mix Fold3-like"
        Model = "gamedev mix-fold3-like"
        OverlayBase = "XiaomiMixFold3Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 418 },
            @{ Name = "Inner"; W = 1916; H = 2160; Border = 72; Radius = 72; Nav = 120; Dpi = 360 }
        )
    },
    @{
        File = "Motorola Razr+-like.device"
        Short = "Motorola Razr+-like"
        Model = "gamedev razr-plus-like"
        OverlayBase = "MotorolaRazrPlusLike"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 1056; H = 1066; Border = 36; Radius = 36; Nav = 0; Dpi = 413 },
            @{ Name = "Main"; W = 1080; H = 2640; Border = 60; Radius = 60; Nav = 120; Dpi = 413 }
        )
    },
    # --- latest models ---
    @{
        File = "Galaxy Z Fold7-like.device"
        Short = "Galaxy Z Fold7-like"
        Model = "gamedev fold7-like"
        OverlayBase = "GalaxyZFold7Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 422 },
            @{ Name = "Inner"; W = 1968; H = 2184; Border = 72; Radius = 72; Nav = 120; Dpi = 368 }
        )
    },
    @{
        File = "Galaxy Z Flip7-like.device"
        Short = "Galaxy Z Flip7-like"
        Model = "gamedev flip7-like"
        OverlayBase = "GalaxyZFlip7Like"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 948; H = 1048; Border = 36; Radius = 36; Nav = 0; Dpi = 373 },
            @{ Name = "Main"; W = 1080; H = 2520; Border = 60; Radius = 60; Nav = 120; Dpi = 397 }
        )
    },
    @{
        File = "Pixel 9 Pro Fold-like.device"
        Short = "Pixel 9 Pro Fold-like"
        Model = "gamedev pixel9-pro-fold-like"
        OverlayBase = "Pixel9ProFoldLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2424; Border = 48; Radius = 48; Nav = 120; Dpi = 422 },
            @{ Name = "Inner"; W = 2076; H = 2152; Border = 72; Radius = 72; Nav = 120; Dpi = 374 }
        )
    },
    @{
        File = "Oppo Find N5-like.device"
        Short = "Oppo Find N5-like"
        Model = "gamedev find-n5-like"
        OverlayBase = "OppoFindN5Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1140; H = 2616; Border = 48; Radius = 48; Nav = 120; Dpi = 431 },
            @{ Name = "Inner"; W = 2248; H = 2480; Border = 72; Radius = 72; Nav = 120; Dpi = 412 }
        )
    },
    @{
        File = "Honor Magic V3-like.device"
        Short = "Honor Magic V3-like"
        Model = "gamedev magic-v3-like"
        OverlayBase = "HonorMagicV3Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1060; H = 2376; Border = 48; Radius = 48; Nav = 120; Dpi = 402 },
            @{ Name = "Inner"; W = 2156; H = 2344; Border = 72; Radius = 72; Nav = 120; Dpi = 402 }
        )
    },
    @{
        File = "Motorola Razr 50 Ultra-like.device"
        Short = "Motorola Razr 50 Ultra-like"
        Model = "gamedev razr50-ultra-like"
        OverlayBase = "MotorolaRazr50UltraLike"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 1272; Border = 36; Radius = 36; Nav = 0; Dpi = 417 },
            @{ Name = "Main"; W = 1080; H = 2640; Border = 60; Radius = 60; Nav = 120; Dpi = 413 }
        )
    },
    @{
        File = "Huawei Mate XT-like.device"
        Short = "Huawei Mate XT-like"
        Model = "gamedev mate-xt-like"
        OverlayBase = "HuaweiMateXTLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1008; H = 2232; Border = 48; Radius = 48; Nav = 120; Dpi = 381 },
            @{ Name = "Inner"; W = 2232; H = 3184; Border = 72; Radius = 72; Nav = 120; Dpi = 381 }
        )
    },
    @{
        File = "Vivo X Fold3 Pro-like.device"
        Short = "Vivo X Fold3 Pro-like"
        Model = "gamedev xfold3-pro-like"
        OverlayBase = "VivoXFold3ProLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1172; H = 2748; Border = 48; Radius = 48; Nav = 120; Dpi = 450 },
            @{ Name = "Inner"; W = 2200; H = 2480; Border = 72; Radius = 72; Nav = 120; Dpi = 413 }
        )
    },
    @{
        File = "Xiaomi Mix Fold4-like.device"
        Short = "Xiaomi Mix Fold4-like"
        Model = "gamedev mix-fold4-like"
        OverlayBase = "XiaomiMixFold4Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 418 },
            @{ Name = "Inner"; W = 2224; H = 2488; Border = 72; Radius = 72; Nav = 120; Dpi = 418 }
        )
    },
    # --- Samsung lineup from Device Lab grid (all models) ---
    @{
        File = "Galaxy Z Fold8 Ultra-like.device"
        Short = "Galaxy Z Fold8 Ultra-like"
        Model = "gamedev fold8-ultra-like"
        OverlayBase = "GalaxyZFold8UltraLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 422 },
            @{ Name = "Inner"; W = 2256; H = 2504; Border = 72; Radius = 72; Nav = 120; Dpi = 422 }
        )
    },
    @{
        File = "Galaxy Z Fold8-like.device"
        Short = "Galaxy Z Fold8-like"
        Model = "gamedev fold8-like"
        OverlayBase = "GalaxyZFold8Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1248; H = 1972; Border = 48; Radius = 48; Nav = 120; Dpi = 428 },
            @{ Name = "Inner"; W = 1848; H = 2448; Border = 72; Radius = 72; Nav = 120; Dpi = 403 }
        )
    },
    @{
        File = "Galaxy Z Flip8-like.device"
        Short = "Galaxy Z Flip8-like"
        Model = "gamedev flip8-like"
        OverlayBase = "GalaxyZFlip8Like"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 948; H = 1048; Border = 36; Radius = 36; Nav = 0; Dpi = 342 },
            @{ Name = "Main"; W = 1080; H = 2520; Border = 60; Radius = 60; Nav = 120; Dpi = 400 }
        )
    },
    @{
        File = "Galaxy Z TriFold-like.device"
        Short = "Galaxy Z TriFold-like"
        Model = "gamedev trifold-like"
        OverlayBase = "GalaxyZTriFoldLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 422 },
            @{ Name = "Inner"; W = 1584; H = 2160; Border = 72; Radius = 72; Nav = 120; Dpi = 269 }
        )
    },
    @{
        File = "Galaxy Z Flip7 FE-like.device"
        Short = "Galaxy Z Flip7 FE-like"
        Model = "gamedev flip7-fe-like"
        OverlayBase = "GalaxyZFlip7FELike"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 720; H = 748; Border = 36; Radius = 36; Nav = 0; Dpi = 306 },
            @{ Name = "Main"; W = 1080; H = 2640; Border = 60; Radius = 60; Nav = 120; Dpi = 426 }
        )
    },
    @{
        File = "Galaxy Z Fold Special Edition-like.device"
        Short = "Galaxy Z Fold Special Edition-like"
        Model = "gamedev fold-se-like"
        OverlayBase = "GalaxyZFoldSpecialEditionLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1080; H = 2520; Border = 48; Radius = 48; Nav = 120; Dpi = 422 },
            @{ Name = "Inner"; W = 1968; H = 2184; Border = 72; Radius = 72; Nav = 120; Dpi = 368 }
        )
    },
    @{
        File = "Galaxy Z Flip6-like.device"
        Short = "Galaxy Z Flip6-like"
        Model = "gamedev flip6-like"
        OverlayBase = "GalaxyZFlip6Like"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 720; H = 748; Border = 36; Radius = 36; Nav = 0; Dpi = 306 },
            @{ Name = "Main"; W = 1080; H = 2640; Border = 60; Radius = 60; Nav = 120; Dpi = 426 }
        )
    },
    @{
        File = "Galaxy Z Flip5-like.device"
        Short = "Galaxy Z Flip5-like"
        Model = "gamedev flip5-like"
        OverlayBase = "GalaxyZFlip5Like"
        Kind = "Flip"
        Screens = @(
            @{ Name = "Cover"; W = 720; H = 748; Border = 36; Radius = 36; Nav = 0; Dpi = 306 },
            @{ Name = "Main"; W = 1080; H = 2640; Border = 60; Radius = 60; Nav = 120; Dpi = 426 }
        )
    },
    @{
        File = "Galaxy Z Fold5-like.device"
        Short = "Galaxy Z Fold5-like"
        Model = "gamedev fold5-like"
        OverlayBase = "GalaxyZFold5Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 904; H = 2316; Border = 48; Radius = 48; Nav = 120; Dpi = 414 },
            @{ Name = "Inner"; W = 1812; H = 2176; Border = 72; Radius = 72; Nav = 120; Dpi = 373 }
        )
    },
    @{
        File = "Galaxy Z Fold2-like.device"
        Short = "Galaxy Z Fold2-like"
        Model = "gamedev fold2-like"
        OverlayBase = "GalaxyZFold2Like"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 816; H = 2260; Border = 48; Radius = 48; Nav = 120; Dpi = 386 },
            @{ Name = "Inner"; W = 1768; H = 2208; Border = 72; Radius = 72; Nav = 120; Dpi = 373 }
        )
    },
    # --- Apple (iOS systemInfo; inner is landscape-native, catalog stores portrait WxH) ---
    # Os "iOS*" uses the iOS safe-area template (Safe = pixel insets, navigationBarHeight 0).
    # Both displays are 3x at 460 ppi (Xcode iPhone Duo.simdevicetype: Cover 1398x2034,
    # Inner 2007x2853 = 669x951 pt). Cover: 59 pt housing, 34 pt home indicator, 21 pt
    # landscape bottom. Inner: small top cutout (~21 pt), same home indicator insets.
    @{
        File = "iPhone Duo-like.device"
        Short = "iPhone Duo-like"
        Model = "iPhone19,4"
        Os = "iOS 27.1"
        OverlayBase = "iPhoneDuoLike"
        Kind = "Fold"
        Screens = @(
            @{ Name = "Cover"; W = 1398; H = 2034; Border = 52; Radius = 150; Nav = 0; Dpi = 460;
               Safe = @{ Top = 177; Bottom = 102; Side = 177; LandBottom = 63 } },
            @{ Name = "Inner"; W = 2007; H = 2853; Border = 64; Radius = 120; Nav = 0; Dpi = 460;
               Safe = @{ Top = 63; Bottom = 102; Side = 63; LandBottom = 63 } }
        )
    }
)

if ($Only.Count -gt 0) {
    $NewDevices = @($NewDevices | Where-Object { $Only -contains $_.Short })
    if ($NewDevices.Count -eq 0) { throw "No device matches -Only $($Only -join ', ')" }
}

foreach ($dev in $NewDevices) {
    $screens = @()
    foreach ($s in $dev.Screens) {
        $overlayName = "$($dev.OverlayBase)_$($s.Name)_Overlay.png"
        $overlayPath = Join-Path $overlayDir $overlayName
        New-BezelOverlay $overlayPath $s.W $s.H $s.Border $s.Radius
        Write-OverlayMeta ($overlayPath + ".meta")
        $ios = if ($dev.Os -like 'iOS*') { $s.Safe } else { $null }
        $screens += New-ScreenObj $s.Name $s.W $s.H $s.Border $s.Radius ("DeviceOverlays/" + $overlayName) $s.Nav $s.Dpi $ios
        Write-Host "OK  overlay $overlayName ($($s.W + $s.Border*2)x$($s.H + $s.Border*2))"
    }

    $star = [char]0x2605
    $os = if ($dev.Os) { $dev.Os } else { "Android OS 14 / API-34" }
    $obj = [ordered]@{
        friendlyName = "$star $($dev.Kind) / $($dev.Short)"
        version = 1
        screens = $screens
        systemInfo = [ordered]@{
            supportsAccelerometer = $true
            processorType = "ARM64 FP ASIMD AES"
            deviceModel = $dev.Model
            processorCount = 8
            operatingSystem = $os
            graphicsDependentData = @()
            supportsVibration = $true
            supportsGyroscope = $true
            processorFrequency = 2800
            systemMemorySize = 12288
            supportsAudio = $true
            supportsLocationService = $true
            deviceType = 1
            operatingSystemFamily = 0
        }
    }

    $devicePath = Join-Path $devicesDir $dev.File
    $json = $obj | ConvertTo-Json -Depth 12
    $utf8 = New-Object System.Text.UTF8Encoding $false
    [IO.File]::WriteAllText($devicePath, $json, $utf8)
    Write-DeviceMeta ($devicePath + ".meta")
    Write-Host "OK  device $($dev.File)"
}

Write-Host "`nGenerated $($NewDevices.Count) device skins." -ForegroundColor Cyan
