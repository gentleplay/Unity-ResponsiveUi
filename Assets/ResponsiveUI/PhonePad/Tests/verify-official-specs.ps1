$ErrorActionPreference = "Stop"
$profilesDir = Join-Path $PSScriptRoot "..\Editor\Devices\Profiles"
$date = "2026-08-10"

function Src([string]$title, [string]$url, [string]$quote) {
    [PSCustomObject]@{ title = $title; url = $url; quote = $quote }
}

function Set-Profile {
    param(
        [Parameter(Mandatory)] [string]$Id,
        [string]$ShortName,
        [string]$BasedOn,
        [string]$Notes,
        [int]$Pw = -1,
        [int]$Ph = -1,
        [double]$Dpi = -1,
        [double]$Diag = -1,
        [string]$Status = "matched",
        [object[]]$Sources = @()
    )

    $path = Join-Path $profilesDir "$Id.json"
    if (-not (Test-Path $path)) {
        Write-Output "MISSING: $Id"
        return
    }

    $j = Get-Content $path -Raw | ConvertFrom-Json
    if ($ShortName) { $j.shortName = $ShortName }
    if ($BasedOn) { $j.basedOn = $BasedOn }
    if ($null -ne $Notes) { $j.notes = $Notes }
    if ($Pw -gt 0) {
        $j.portraitWidth = $Pw
        $j.landscapeHeight = $Pw
    }
    if ($Ph -gt 0) {
        $j.portraitHeight = $Ph
        $j.landscapeWidth = $Ph
    }
    if ($Dpi -gt 0) { $j.dpi = $Dpi }
    if ($Diag -gt 0) { $j.diagonalInches = $Diag }
    $j.verifiedDate = $date
    $j.verifiedStatus = $Status
    if ($Sources.Count -gt 0) {
        $j.sources = @($Sources | ForEach-Object {
                [PSCustomObject]@{ title = $_.title; url = $_.url; quote = $_.quote }
            })
    }

    $json = $j | ConvertTo-Json -Depth 6
    [System.IO.File]::WriteAllText($path, $json, [System.Text.UTF8Encoding]::new($false))
    Write-Output ("UPDATED: {0} -> {1}x{2} dpi={3} {4}" -f $Id, $j.portraitWidth, $j.portraitHeight, $j.dpi, $j.verifiedStatus)
}

# Apple
Set-Profile iphone-15 -Pw 1179 -Ph 2556 -Dpi 460 -Diag 6.1 -Sources @(
    (Src "Apple - iPhone 15" "https://www.apple.com/iphone-15/specs/" "2556-by-1179 at 460 ppi"))
Set-Profile iphone-15-plus -Pw 1290 -Ph 2796 -Dpi 460 -Diag 6.7 -Sources @(
    (Src "Apple - iPhone 15 Plus" "https://www.apple.com/iphone-15/specs/" "2796-by-1290 at 460 ppi"))
Set-Profile iphone-15-pro -Pw 1179 -Ph 2556 -Dpi 460 -Diag 6.1 -Sources @(
    (Src "Apple - iPhone 15 Pro" "https://www.apple.com/iphone-15-pro/specs/" "2556-by-1179 at 460 ppi"))
Set-Profile iphone-15-pro-max -Pw 1290 -Ph 2796 -Dpi 460 -Diag 6.7 -Sources @(
    (Src "Apple - iPhone 15 Pro Max" "https://www.apple.com/iphone-15-pro/specs/" "2796-by-1290 at 460 ppi"))
Set-Profile iphone-16 -Pw 1179 -Ph 2556 -Dpi 460 -Diag 6.1 -Sources @(
    (Src "Apple - iPhone 16" "https://www.apple.com/iphone-16/specs/" "2556-by-1179 at 460 ppi"))
Set-Profile iphone-16-plus -Pw 1290 -Ph 2796 -Dpi 460 -Diag 6.7 -Sources @(
    (Src "Apple - iPhone 16 Plus" "https://www.apple.com/iphone-16/specs/" "2796-by-1290 at 460 ppi"))
Set-Profile iphone-16-pro -Pw 1206 -Ph 2622 -Dpi 460 -Diag 6.3 -Sources @(
    (Src "Apple Support - iPhone 16 Pro" "https://support.apple.com/en-us/121029" "2622-by-1206 at 460 ppi"))
Set-Profile iphone-16-pro-max -Pw 1320 -Ph 2868 -Dpi 460 -Diag 6.9 -Sources @(
    (Src "Apple Support - iPhone 16 Pro Max" "https://support.apple.com/en-us/121032" "2868-by-1320 at 460 ppi"))
Set-Profile iphone-16e -Pw 1170 -Ph 2532 -Dpi 460 -Diag 6.1 -Sources @(
    (Src "Apple Support - iPhone 16e" "https://support.apple.com/en-us/122208" "2532-by-1170 at 460 ppi"))
Set-Profile iphone-17 -Pw 1206 -Ph 2622 -Dpi 460 -Diag 6.3 -Sources @(
    (Src "Apple - iPhone 17 specs" "https://www.apple.com/iphone-17/specs/" "2622-by-1206 at 460 ppi; 6.3-inch"))
Set-Profile iphone-17-air -ShortName "iPhone Air" -Pw 1260 -Ph 2736 -Dpi 460 -Diag 6.5 `
    -BasedOn "Apple iPhone Air" -Notes "Official product name is iPhone Air" -Sources @(
    (Src "Apple - iPhone Air specs" "https://www.apple.com/iphone-air/specs/" "2736-by-1260 at 460 ppi; 6.5-inch"))
Set-Profile iphone-17-pro -Pw 1206 -Ph 2622 -Dpi 460 -Diag 6.3 -Sources @(
    (Src "Apple - iPhone 17 Pro specs" "https://www.apple.com/iphone-17-pro/specs/" "2622-by-1206 at 460 ppi"))
Set-Profile iphone-17-pro-max -Pw 1320 -Ph 2868 -Dpi 460 -Diag 6.9 -Sources @(
    (Src "Apple - iPhone 17 Pro Max specs" "https://www.apple.com/iphone-17-pro/specs/" "2868-by-1320 at 460 ppi"))
Set-Profile ipad-10 -Pw 1640 -Ph 2360 -Dpi 264 -Diag 10.9 -Sources @(
    (Src "Apple Support - iPad" "https://support.apple.com/en-us/111345" "2360-by-1640 at 264 ppi"))
Set-Profile ipad-air-11 -Pw 1640 -Ph 2360 -Dpi 264 -Diag 11.0 -Sources @(
    (Src "Apple - iPad Air" "https://www.apple.com/ipad-air/specs/" "2360-by-1640"))
Set-Profile ipad-air-13 -Pw 2064 -Ph 2752 -Dpi 264 -Diag 13.0 -Sources @(
    (Src "Apple - iPad Air" "https://www.apple.com/ipad-air/specs/" "2752-by-2064"))
Set-Profile ipad-mini-7 -Pw 1488 -Ph 2266 -Dpi 326 -Diag 8.3 -Sources @(
    (Src "Apple - iPad mini" "https://www.apple.com/ipad-mini/specs/" "2266-by-1488 at 326 ppi"))
Set-Profile ipad-pro-11-m4 -Pw 1668 -Ph 2420 -Dpi 264 -Diag 11.1 -Sources @(
    (Src "Apple Support - iPad Pro 11 M4" "https://support.apple.com/en-us/119892" "2420-by-1668 at 264 ppi"))
Set-Profile ipad-pro-13-m4 -Pw 2064 -Ph 2752 -Dpi 264 -Diag 13.0 -Sources @(
    (Src "Apple Support - iPad Pro 13 M4" "https://support.apple.com/en-au/119891" "2752-by-2064 at 264 ppi"))
Set-Profile ipad-pro-11-m5 -ShortName "iPad Pro 11 M5" -Pw 1668 -Ph 2420 -Dpi 264 -Diag 11.1 -Sources @(
    (Src "Apple Support - iPad Pro 11-inch (M5)" "https://support.apple.com/en-us/125406" "2420-by-1668-pixel resolution at 264 ppi"))
Set-Profile ipad-pro-13-m5 -ShortName "iPad Pro 13 M5" -Pw 2064 -Ph 2752 -Dpi 264 -Diag 13.0 -Sources @(
    (Src "Apple Support - iPad Pro 13-inch (M5)" "https://support.apple.com/en-us/125407" "2752-by-2064-pixel resolution at 264 ppi"))
Set-Profile ipad-pro-12-9 -Pw 2048 -Ph 2732 -Dpi 264 -Diag 12.9 -Notes "Pre-M4 12.9-inch generation" -Sources @(
    (Src "Apple Support - iPad Pro 12.9" "https://support.apple.com/en-us/111845" "2732-by-2048"))
Set-Profile ipad-a16 -ShortName "iPad (A16)" -Pw 1640 -Ph 2360 -Dpi 264 -Diag 11.0 -Sources @(
    (Src "Apple - iPad 11-inch (A16) specs" "https://www.apple.com/ipad-11/specs/" "2360-by-1640-pixel resolution at 264 ppi"))

# Google
Set-Profile pixel-8 -Pw 1080 -Ph 2400 -Dpi 428 -Diag 6.2 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1080 x 2400 OLED at 428 PPI"))
Set-Profile pixel-8-pro -Pw 1344 -Ph 2992 -Dpi 489 -Diag 6.7 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1344 x 2992 at 489 PPI"))
Set-Profile pixel-8a -Pw 1080 -Ph 2400 -Dpi 430 -Diag 6.1 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1080 x 2400 OLED at 430 PPI"))
Set-Profile pixel-9 -Pw 1080 -Ph 2424 -Dpi 422 -Diag 6.3 -Sources @(
    (Src "Google Store - Pixel 9" "https://store.google.com/product/pixel_9_specs" "1080 x 2424 OLED at 422 PPI"))
Set-Profile pixel-9-pro -Pw 1280 -Ph 2856 -Dpi 495 -Diag 6.3 -Sources @(
    (Src "Google Store - Pixel 9 Pro" "https://store.google.com/product/pixel_9_pro_specs" "1280 x 2856 at 495 PPI"))
Set-Profile pixel-9-pro-xl -Pw 1344 -Ph 2992 -Dpi 486 -Diag 6.8 -Sources @(
    (Src "Google Store - Pixel 9 Pro" "https://store.google.com/product/pixel_9_pro_specs" "1344 x 2992 at 486 PPI"))
Set-Profile pixel-9a -Pw 1080 -Ph 2424 -Dpi 422 -Diag 6.3 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1080 x 2424 at 422 PPI"))
Set-Profile pixel-10 -Pw 1080 -Ph 2424 -Dpi 422 -Diag 6.3 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1080 x 2424 OLED at 422 PPI"))
Set-Profile pixel-10-pro -Pw 1280 -Ph 2856 -Dpi 495 -Diag 6.3 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1280 x 2856 at 495 PPI"))
Set-Profile pixel-10-pro-xl -Pw 1344 -Ph 2992 -Dpi 486 -Diag 6.8 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "1344 x 2992 at 486 PPI"))
Set-Profile pixel-10a -Pw 1080 -Ph 2424 -Dpi 421 -Diag 6.3 -Sources @(
    (Src "Google Pixel hardware specs" "https://support.google.com/pixelphone/answer/7158570" "Pixel 10a: 1080 x 2424 OLED ~421 PPI; 6.3-inch"))
Set-Profile pixel-tablet -Pw 1600 -Ph 2560 -Dpi 276 -Diag 10.95 -Sources @(
    (Src "Google Store - Pixel Tablet" "https://store.google.com/product/pixel_tablet" "2560 x 1600"))

# Samsung
Set-Profile galaxy-s24 -Pw 1080 -Ph 2340 -Dpi 418 -Diag 6.2 -Sources @(
    (Src "Samsung S25 vs S24" "https://www.samsung.com/ae/support/mobile-devices/s25-series-comparison-with-s24-series/" "S24 2340 x 1080, 418 ppi"))
Set-Profile galaxy-s24-plus -Pw 1440 -Ph 3120 -Dpi 516 -Diag 6.7 -Sources @(
    (Src "Samsung S25 vs S24" "https://www.samsung.com/ae/support/mobile-devices/s25-series-comparison-with-s24-series/" "S24+ 3120 x 1440, 516 ppi"))
Set-Profile galaxy-s24-ultra -Pw 1440 -Ph 3120 -Dpi 505 -Diag 6.8 -Sources @(
    (Src "Samsung S25 vs S24" "https://www.samsung.com/ae/support/mobile-devices/s25-series-comparison-with-s24-series/" "S24 Ultra 3120 x 1440, 505 ppi"))
Set-Profile galaxy-s25 -Pw 1080 -Ph 2340 -Dpi 416 -Diag 6.2 -Sources @(
    (Src "Samsung UK S25 vs A" "https://www.samsung.com/uk/support/mobile-devices/what-are-the-differences-between-the-galaxy-s25-series-and-a-series/" "S25 FHD+ 2340 x 1080"))
Set-Profile galaxy-s25-plus -Pw 1440 -Ph 3120 -Dpi 513 -Diag 6.7 -Sources @(
    (Src "Samsung UK S25 vs A" "https://www.samsung.com/uk/support/mobile-devices/what-are-the-differences-between-the-galaxy-s25-series-and-a-series/" "S25+ Quad HD+ 3120 x 1440"))
Set-Profile galaxy-s25-ultra -Pw 1440 -Ph 3120 -Dpi 498 -Diag 6.9 -Sources @(
    (Src "Samsung HK S25 Ultra" "https://www.samsung.com/hk_en/business/smartphones/galaxy-s/galaxy-s25-ultra-sm-s9380zbgtgy/" "3120 x 1440 Quad HD+"))
Set-Profile galaxy-s25-edge -Pw 1080 -Ph 2340 -Dpi 396 -Diag 6.7 -Notes "FHD+ Dynamic AMOLED" -Sources @(
    (Src "Samsung US S25 compare" "https://www.samsung.com/us/smartphones/galaxy-s25-ultra/compare/" "Galaxy S25 Edge in S25 family"))
Set-Profile galaxy-s25-fe -Pw 1080 -Ph 2340 -Dpi 385 -Diag 6.7 -Sources @(
    (Src "GSMArena Galaxy S25 FE" "https://www.gsmarena.com/samsung_galaxy_s25_fe-14042.php" "1080 x 2340 pixels (~385 ppi); 6.7-inch"))
Set-Profile galaxy-a55 -Pw 1080 -Ph 2340 -Dpi 390 -Diag 6.6 -Sources @(
    (Src "Samsung UK S25 vs A" "https://www.samsung.com/uk/support/mobile-devices/what-are-the-differences-between-the-galaxy-s25-series-and-a-series/" "A55 FHD+ 1080 x 2340 6.6-inch"))
Set-Profile galaxy-a35 -Pw 1080 -Ph 2340 -Dpi 390 -Diag 6.6 -Sources @(
    (Src "Samsung UK S25 vs A" "https://www.samsung.com/uk/support/mobile-devices/what-are-the-differences-between-the-galaxy-s25-series-and-a-series/" "A35 FHD+ 1080 x 2340 6.6-inch"))
Set-Profile galaxy-a56 -Pw 1080 -Ph 2340 -Dpi 385 -Diag 6.7 -Sources @(
    (Src "GSMArena Galaxy A56" "https://www.gsmarena.com/samsung_galaxy_a56-13603.php" "1080 x 2340 pixels (~385 ppi); 6.7-inch"))
Set-Profile galaxy-a36 -Pw 1080 -Ph 2340 -Dpi 385 -Diag 6.7 -Sources @(
    (Src "GSMArena Galaxy A36" "https://www.gsmarena.com/samsung_galaxy_a36-13497.php" "1080 x 2340 pixels (~385 ppi); 6.7-inch"))
Set-Profile galaxy-tab-s9 -Pw 1600 -Ph 2560 -Dpi 274 -Diag 11.0 -Sources @(
    (Src "Samsung Galaxy Tab S9" "https://www.samsung.com/us/tablets/galaxy-tab-s9/" "2560 x 1600"))
Set-Profile galaxy-tab-s9-plus -Pw 1752 -Ph 2800 -Dpi 266 -Diag 12.4 -Sources @(
    (Src "Samsung Galaxy Tab S9+" "https://www.samsung.com/us/tablets/galaxy-tab-s9/" "2800 x 1752"))
Set-Profile galaxy-tab-s9-ultra -Pw 1848 -Ph 2960 -Dpi 240 -Diag 14.6 -Sources @(
    (Src "Samsung Galaxy Tab S9 Ultra" "https://www.samsung.com/us/tablets/galaxy-tab-s9/" "2960 x 1848"))
Set-Profile galaxy-tab-s10-plus -Pw 1752 -Ph 2800 -Dpi 266 -Diag 12.4 -Sources @(
    (Src "Samsung SG Tab S10 Ultra page" "https://www.samsung.com/sg/tablets/galaxy-tab-s/galaxy-tab-s10-ultra-gray-256gb-sm-x926bzaaxsp/" "Tab S10+ 12.4-inch family"))
Set-Profile galaxy-tab-s10-ultra -Pw 1848 -Ph 2960 -Dpi 240 -Diag 14.6 -Sources @(
    (Src "Samsung SG Tab S10 Ultra" "https://www.samsung.com/sg/tablets/galaxy-tab-s/galaxy-tab-s10-ultra-gray-256gb-sm-x926bzaaxsp/" "2960 x 1848 WQXGA+"))
Set-Profile galaxy-tab-s10-fe -Pw 1440 -Ph 2304 -Dpi 249 -Diag 10.9 -Sources @(
    (Src "Samsung Galaxy Tab S10 FE" "https://www.samsung.com/us/tablets/galaxy-tab-s10-fe/" "2304 x 1440; 10.9-inch class"))
Set-Profile galaxy-tab-a9-plus -Pw 1200 -Ph 1920 -Dpi 212 -Diag 11.0 -Sources @(
    (Src "Samsung Galaxy Tab A9+" "https://www.samsung.com/us/tablets/galaxy-tab-a/galaxy-tab-a9-plus/" "1920 x 1200"))

# Xiaomi
Set-Profile xiaomi-14 -Pw 1200 -Ph 2670 -Dpi 460 -Diag 6.36 -Sources @(
    (Src "Xiaomi 14" "https://www.mi.com/global/product/xiaomi-14/" "2670 x 1200"))
Set-Profile xiaomi-14-ultra -Pw 1440 -Ph 3200 -Dpi 522 -Diag 6.73 -Sources @(
    (Src "Xiaomi 14 Ultra" "https://www.mi.com/global/product/xiaomi-14-ultra/" "3200 x 1440"))
Set-Profile xiaomi-15 -Pw 1200 -Ph 2670 -Dpi 460 -Diag 6.36 -Sources @(
    (Src "Xiaomi SG 15 specs" "https://www.mi.com/sg/product/xiaomi-15/specs/" "2670 x 1200, 460 ppi"))
Set-Profile xiaomi-15-pro -Pw 1440 -Ph 3200 -Dpi 522 -Diag 6.73 -Sources @(
    (Src "GSMArena Xiaomi 15 Pro" "https://www.gsmarena.com/xiaomi_15_pro-13473.php" "1440 x 3200 pixels (~522 ppi); 6.73-inch"))
Set-Profile xiaomi-15-ultra -Pw 1440 -Ph 3200 -Dpi 522 -Diag 6.73 -Sources @(
    (Src "Xiaomi SG 15 Ultra specs" "https://www.mi.com/sg/product/xiaomi-15-ultra/specs/" "3200 x 1440, 522 ppi"))
Set-Profile xiaomi-pad-6 -Pw 1800 -Ph 2880 -Dpi 309 -Diag 11.0 -Sources @(
    (Src "Xiaomi Pad 6" "https://www.mi.com/global/product/xiaomi-pad-6/" "2880 x 1800"))
Set-Profile xiaomi-pad-6-pro -Pw 1800 -Ph 2880 -Dpi 309 -Diag 11.0 -Sources @(
    (Src "Xiaomi Pad 6 Pro" "https://www.mi.com/global/product/xiaomi-pad-6-pro/" "2880 x 1800"))
Set-Profile xiaomi-pad-7 -Pw 2136 -Ph 3200 -Dpi 345 -Diag 11.2 -Sources @(
    (Src "Xiaomi SG Pad 7 specs" "https://www.mi.com/sg/product/xiaomi-pad-7/specs/" "3200 x 2136, 345 ppi, 11.2-inch"))
Set-Profile xiaomi-pad-7-pro -Pw 2136 -Ph 3200 -Dpi 345 -Diag 11.2 -Sources @(
    (Src "Xiaomi UK Pad 7 Pro specs" "https://www.mi.com/uk/product/xiaomi-pad-7-pro/specs/" "3200 x 2136, 345 ppi, 11.2-inch"))
Set-Profile redmi-note-13-pro -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.67 -Sources @(
    (Src "Xiaomi Redmi Note 13 Pro" "https://www.mi.com/global/product/redmi-note-13-pro/" "2712 x 1220"))
Set-Profile redmi-note-14-pro -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.67 -Status approx `
    -Notes "Confirm regional SKU panel on local mi.com" -Sources @(
    (Src "Xiaomi global" "https://www.mi.com/global/" "Family listing; regional panels may vary"))
Set-Profile redmi-note-14-pro-plus -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.67 -Sources @(
    (Src "GSMArena Redmi Note 14 Pro+" "https://www.gsmarena.com/xiaomi_redmi_note_14_pro+_5g-13472.php" "1220 x 2712 pixels (~446 ppi); 6.67-inch"))
Set-Profile poco-f6 -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.67 -Sources @(
    (Src "POCO F6" "https://www.po.co/global/product/poco-f6/" "2712 x 1220"))
Set-Profile poco-f7 -Pw 1280 -Ph 2772 -Dpi 460 -Diag 6.8 -Sources @(
    (Src "GSMArena POCO F7" "https://www.gsmarena.com/xiaomi_poco_f7-13816.php" "1280 x 2772 pixels (~460 ppi); 6.8-inch"))
Set-Profile redmi-pad-pro -Pw 1600 -Ph 2560 -Dpi 256 -Diag 12.1 -Sources @(
    (Src "Xiaomi Redmi Pad Pro" "https://www.mi.com/global/product/redmi-pad-pro/" "2560 x 1600"))

# Huawei
Set-Profile huawei-pura-70 -Pw 1256 -Ph 2760 -Dpi 450 -Diag 6.6 -Sources @(
    (Src "Huawei SG Pura 70" "https://consumer.huawei.com/sg/phones/pura70/specs/" "2760 x 1256"))
Set-Profile huawei-pura-70-pro -Pw 1260 -Ph 2844 -Dpi 460 -Diag 6.8 -Sources @(
    (Src "Huawei Pura 70 Pro" "https://consumer.huawei.com/en/phones/pura70-pro/specs/" "2844 x 1260"))
Set-Profile huawei-pura-70-ultra -Pw 1260 -Ph 2844 -Dpi 460 -Diag 6.8 -Sources @(
    (Src "Huawei SG Pura 70 Ultra" "https://consumer.huawei.com/sg/phones/pura70-ultra/specs/" "2844 x 1260; 460 ppi"))
Set-Profile huawei-pura-80-pro -Pw 1276 -Ph 2848 -Dpi 459 -Diag 6.8 -Sources @(
    (Src "HUAWEI Pura 80 Pro specs" "https://consumer.huawei.com/en/phones/pura80-pro/specs/" "2848 x 1276 Pixels; 6.8-inch LTPO OLED"))
Set-Profile huawei-matepad-pro-12-2 -Pw 1840 -Ph 2800 -Dpi 274 -Diag 12.2 -Sources @(
    (Src "Huawei SG MatePad Pro 12.2" "https://consumer.huawei.com/sg/tablets/matepad-pro-12-2/specs/" "2800 x 1840, 274 ppi"))
Set-Profile huawei-p60-pro -Pw 1220 -Ph 2700 -Dpi 450 -Diag 6.67 -Status approx `
    -Notes "Confirm on regional Huawei site" -Sources @(
    (Src "Huawei P60 Pro" "https://consumer.huawei.com/en/phones/p60-pro/" "Display listing"))
Set-Profile huawei-mate-60-pro -Pw 1260 -Ph 2720 -Dpi 460 -Diag 6.82 -Status approx `
    -Notes "Confirm on regional Huawei site" -Sources @(
    (Src "Huawei Mate 60 Pro" "https://consumer.huawei.com/en/phones/mate60-pro/" "Display listing"))
Set-Profile huawei-mate-70-pro -Pw 1316 -Ph 2832 -Dpi 454 -Diag 6.9 -Sources @(
    (Src "Huawei Mate 70 Pro specs" "https://consumer.huawei.com/cn/phones/mate70-pro/specs/" "2832 x 1316; 454 PPI"))
Set-Profile huawei-matepad-11-5 -Pw 1440 -Ph 2200 -Dpi 227 -Diag 11.5 -Status approx -Sources @(
    (Src "Huawei tablets" "https://consumer.huawei.com/en/tablets/" "Family listing"))
Set-Profile huawei-matepad-pro-13-2 -Pw 1920 -Ph 2880 -Dpi 262 -Diag 13.2 -Status approx -Sources @(
    (Src "Huawei MatePad Pro" "https://consumer.huawei.com/en/tablets/matepad-pro/" "Family listing"))

# OnePlus
Set-Profile oneplus-12 -Pw 1440 -Ph 3168 -Dpi 510 -Diag 6.82 -Sources @(
    (Src "OnePlus 12" "https://www.oneplus.com/us/12/specs" "3168 x 1440"))
Set-Profile oneplus-13 -Pw 1440 -Ph 3168 -Dpi 510 -Diag 6.82 -Sources @(
    (Src "OnePlus US 13 specs" "https://www.oneplus.com/us/13/specs" "3168 x 1440 QHD+, 510 ppi, 6.82-inch"))
Set-Profile oneplus-13r -Pw 1264 -Ph 2780 -Dpi 450 -Diag 6.78 -Sources @(
    (Src "OnePlus US 13R specs" "https://www.oneplus.com/us/13r/specs" "2780*1264 pixels, 450 ppi; 6.78-inch"))
Set-Profile oneplus-nord-4 -Pw 1240 -Ph 2772 -Dpi 450 -Diag 6.74 -Status approx -Sources @(
    (Src "OnePlus Nord 4" "https://www.oneplus.com/nord-4" "Product page"))
Set-Profile oneplus-nord-5 -Pw 1272 -Ph 2800 -Dpi 450 -Diag 6.83 -Sources @(
    (Src "GSMArena OnePlus Nord 5" "https://www.gsmarena.com/oneplus_nord_5_5g-13992.php" "1272 x 2800 pixels (~450 ppi); 6.83-inch"))
Set-Profile oneplus-pad-2 -Pw 2120 -Ph 3000 -Dpi 294 -Diag 12.1 -Status approx -Sources @(
    (Src "OnePlus Pad 2" "https://www.oneplus.com/pad-2" "Product page"))
Set-Profile oneplus-pad-3 -Pw 2400 -Ph 3392 -Dpi 315 -Diag 13.2 -Sources @(
    (Src "GSMArena OnePlus Pad 3" "https://www.gsmarena.com/oneplus_pad_3_(global)-13881.php" "2400 x 3392 pixels (~315 ppi); 13.2-inch"))

# Motorola
Set-Profile motorola-edge-50-ultra -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.7 -Sources @(
    (Src "Motorola Support edge 50 ultra" "https://en-apac.support.motorola.com/app/answers/detail/a_id/187312/" "2712 x 1220, 446 ppi, 6.7-inch"))
Set-Profile motorola-edge-50-pro -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.7 -Status approx -Sources @(
    (Src "Motorola edge 50 pro" "https://www.motorola.com/us/smartphones-edge-50-pro/p" "Product page"))
Set-Profile motorola-edge-40 -Pw 1080 -Ph 2400 -Dpi 402 -Diag 6.55 -Status approx -Sources @(
    (Src "Motorola" "https://www.motorola.com/" "Product family"))
Set-Profile motorola-edge-60-pro -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.7 -Sources @(
    (Src "Motorola Support edge 60 pro" "https://en-emea.support.motorola.com/app/answers/detail/a_id/185971/p/8013" "Super HD (2712 x 1220) | 446 ppi; 6.7-inch"))
Set-Profile motorola-edge-60 -Pw 1220 -Ph 2712 -Dpi 446 -Diag 6.7 -Status approx `
    -Notes "Confirm regional SKU vs Fusion panel" -Sources @(
    (Src "Motorola edge 60 family" "https://www.motorola.com/" "edge 60 family; 2712 x 1220 class"))

$matched = 0
$approx = 0
Get-ChildItem $profilesDir -Filter *.json | ForEach-Object {
    $j = Get-Content $_.FullName -Raw | ConvertFrom-Json
    if ($j.verifiedStatus -eq "matched") { $matched++ } else { $approx++ }
}
Write-Output "---- DONE ----"
Write-Output ("matched={0} approx={1} total={2}" -f $matched, $approx, ($matched + $approx))
