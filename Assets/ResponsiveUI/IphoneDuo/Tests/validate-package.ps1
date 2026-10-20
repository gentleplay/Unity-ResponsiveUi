# Static structural validation for Assets/ResponsiveUI/IphoneDuo (no Unity required).
# This packaging gate is not proof of iOS runtime behavior; test the postures on a physical iPhone Duo.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File Assets/ResponsiveUI/IphoneDuo/Tests/validate-package.ps1

$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI; docs/sources contain → ◀ ▶.
$PSDefaultParameterValues['Get-Content:Encoding'] = 'UTF8'
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

Write-Host "Package root: $pkg"
Write-Host "=== Package structure ===" -ForegroundColor Cyan
$required = @(
    "README.md",
    "Runtime\com.futuredigitalplus.iphoneduo.asmdef",
    "Runtime\AssemblyInfo.cs",
    "Runtime\IphoneDuoHinge.cs",
    "Runtime\IphoneDuoHingeStatus.cs",
    "Runtime\IphoneDuoPostureBackend.cs",
    "Runtime\IphoneDuoHoldLock.cs",
    "Runtime\IphoneDuoHoldOrientation.cs",
    "Runtime\IphoneDuoAutorotateFlags.cs",
    "Runtime\IphoneDuoRules.cs",
    "Runtime\IphoneDuoSettings.cs",
    "Runtime\IphoneDuoDevice.cs",
    "Runtime\IphoneDuoState.cs",
    "Runtime\IphoneDuoPosture.cs",
    "Runtime\IphoneDuoHold.cs",
    "Runtime\IphoneDuoScreenMode.cs",
    "Runtime\IphoneDuoAcrossHold.cs",
    "Runtime\IphoneDuoSidewaysTurn.cs",
    "Runtime\IphoneDuoEvents.cs",
    "Runtime\IphoneDuoDebugOverlay.cs",
    "Runtime\IphoneDuoFlatLock.cs",
    "Runtime\IphoneDuoFlatOrientation.cs",
    "Runtime\IphoneDuoInterfaceOrientation.cs",
    "Runtime\Plugins\iOS\IphoneDuoHinge.mm",
    "Editor\com.futuredigitalplus.iphoneduo.editor.asmdef",
    "Editor\AssemblyInfo.cs",
    "Editor\IphoneDuoDeviceSimulatorPlugin.cs",
    "Editor\IphoneDuoBuildOrientationCheck.cs",
    "Editor\IphoneDuoEditorRules.cs",
    "Editor\IphoneDuoSettingsProvider.cs",
    "Editor\IphoneDuoBuildPlist.cs",
    "Tests\Editor\com.futuredigitalplus.iphoneduo.tests.editor.asmdef",
    "Tests\Editor\IphoneDuoTests.cs"
)
foreach ($rel in $required) {
    $path = Join-Path $pkg $rel
    Assert-True (Test-Path -LiteralPath $path) "iPhone Duo module file $rel"
    Assert-True (Test-Path -LiteralPath "$path.meta") "iPhone Duo module file $rel.meta"
}

Write-Host "=== Assemblies ===" -ForegroundColor Cyan
$runtimeAsmdef = Get-Content (Join-Path $pkg "Runtime\com.futuredigitalplus.iphoneduo.asmdef") -Raw | ConvertFrom-Json
Assert-True (@($runtimeAsmdef.includePlatforms).Count -eq 0) "iPhone Duo runtime compiles on every platform (game code and IphoneDuoEvents stay valid off iOS)"
Assert-True ((@($runtimeAsmdef.references) -join ",") -eq "com.futuredigitalplus.foldandflip") "iPhone Duo runtime depends on Fold & Flip only (no ResponsiveUI)"
$editorAsmdef = Get-Content (Join-Path $pkg "Editor\com.futuredigitalplus.iphoneduo.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True (@($editorAsmdef.references) -contains "com.futuredigitalplus.iphoneduo") "iPhone Duo editor shares the runtime rules (Project Settings page, Simulator preview)"
$testsAsmdef = Get-Content (Join-Path $pkg "Tests\Editor\com.futuredigitalplus.iphoneduo.tests.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True (@($testsAsmdef.references) -contains "com.futuredigitalplus.iphoneduo.editor") "EditMode tests cover the editor rules and the iOS build step"
$foldEditorInfo = Get-Content (Join-Path $pkg "..\FoldAndFlip\Editor\AssemblyInfo.cs") -Raw
Assert-True ($foldEditorInfo -notmatch "iphoneduo") "Fold & Flip editor grants iPhone Duo no internals (the Duo panel uses public API)"

Write-Host "=== Posture backend + hinge ===" -ForegroundColor Cyan
$duoBackend = Get-Content (Join-Path $pkg "Runtime\IphoneDuoPostureBackend.cs") -Raw
Assert-True ($duoBackend -match "RuntimeInitializeOnLoadMethod\(RuntimeInitializeLoadType\.SubsystemRegistration\)") "iPhone Duo backend registers before the first scene loads"
Assert-True ($duoBackend -match "#if UNITY_IOS && !UNITY_EDITOR") "iPhone Duo backend compiles for iOS players only"
$duoPlugin = Get-Content (Join-Path $pkg "Editor\IphoneDuoDeviceSimulatorPlugin.cs") -Raw
Assert-True ($duoPlugin -match 'Title = "iPhone Duo"') "iPhone Duo Simulator panel title"
Assert-True ($duoPlugin -match "FoldableSimulatorApply\.Apply") "iPhone Duo panel applies through the shared Simulator apply"
$iosHinge = Get-Content (Join-Path $pkg "Runtime\IphoneDuoHinge.cs") -Raw
$iosNative = Get-Content (Join-Path $pkg "Runtime\Plugins\iOS\IphoneDuoHinge.mm") -Raw
Assert-True ($iosHinge -match '"iPhone19,4"') "iPhone Duo model identifier gated"
Assert-True ($iosHinge -match 'DllImport\("__Internal"\)') "iOS hinge uses __Internal native calls"
Assert-True ($iosHinge -match '#if UNITY_IOS && !UNITY_EDITOR\s+\[DllImport') "native calls exist on iOS players only (the runtime compiles everywhere)"
$deviceApi = Get-Content (Join-Path $pkg "Runtime\IphoneDuoDevice.cs") -Raw
Assert-True ($deviceApi -match '#if UNITY_IOS && !UNITY_EDITOR\s+return IphoneDuoHinge\.IsSupportedDevice;\s+#else\s+return false;') "IphoneDuoDevice.IsSupported is false off iOS players"
Assert-True ($iosNative -match 'NSClassFromString\(@"UIHingeInteraction"\)') "native hinge resolved at runtime (any Xcode)"
Assert-True ($iosNative -match "case FDPHingePartiallyOpen: return FDPHingePartiallyOpen") "native honours the system UIHingeStatus on every SDK (ABI-stable values)"
Assert-True ($iosNative -match "StatusFromAngle") "native derives the status from the hinge angle when it is missing / Unknown"
# Name, return type and parameter types of every C# DllImport must match the .mm export.
function Get-IosSignatures([string]$text, [string]$pattern, [bool]$native) {
    ([regex]::Matches($text, $pattern) | ForEach-Object {
        $params = $_.Groups[3].Value
        if ($native) { $params = $params -replace '^\s*void\s*$', '' }
        $params = ($params -replace '\s*\w+\s*(,|$)', '$1') -replace '\s', ''
        "$($_.Groups[2].Value):$($_.Groups[1].Value)($params)"
    } | Sort-Object -Unique) -join ";"
}
$iosCsSignatures = Get-IosSignatures $iosHinge 'static extern (\w+) (FDPIphoneDuo_\w+)\(([^)]*)\)' $false
$iosMmSignatures = Get-IosSignatures $iosNative 'extern "C" (\w+) (FDPIphoneDuo_\w+)\(([^)]*)\)' $true
Assert-True (($iosCsSignatures -ne "") -and ($iosCsSignatures -eq $iosMmSignatures)) "iOS DllImport signatures match the .mm exports (C#: $iosCsSignatures; native: $iosMmSignatures)"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\Plugins\iOS\IphoneDuoHinge.mm.meta") -Raw) -match "iPhone: iOS\s+second:\s+enabled: 1") "native hinge plugin imports for iOS only"

Write-Host "=== Native launch hold contract ===" -ForegroundColor Cyan
# The .mm reads what IphoneDuoBuildPlist writes and mirrors C# values it cannot reference.
$plistStep = Get-Content (Join-Path $pkg "Editor\IphoneDuoBuildPlist.cs") -Raw
$plistKeys = @([regex]::Matches($plistStep, 'const string \w+Key = "(FDP\w+)"') | ForEach-Object { $_.Groups[1].Value })
Assert-True ($plistKeys.Count -ge 5) "IphoneDuoBuildPlist declares the launch hold Info.plist keys ($($plistKeys -join ', '))"
foreach ($key in $plistKeys) {
    Assert-True ($iosNative.Contains('@"' + $key + '"')) "native launch hold reads Info.plist key $key"
}
$acrossHold = Get-Content (Join-Path $pkg "Runtime\IphoneDuoAcrossHold.cs") -Raw
foreach ($rule in "Sideways", "Rotate", "SystemDefault") {
    $cs = [regex]::Match($acrossHold, "\b$rule = (\d+)").Groups[1].Value
    $mm = [regex]::Match($iosNative, "FDPRule$rule = (\d+)").Groups[1].Value
    Assert-True (($cs -ne "") -and ($cs -eq $mm)) "native FDPRule$rule ($mm) matches IphoneDuoAcrossHold.$rule ($cs)"
}
function Get-Number([string]$text, [string]$pattern) {
    $m = [regex]::Match($text, $pattern)
    if ($m.Success) { [double]$m.Groups[1].Value } else { $null }
}
$coverHeuristics = Get-Content (Join-Path $pkg "..\FoldAndFlip\Runtime\FoldableCoverHeuristics.cs") -Raw
$innerSize = @(
    @("InnerLongPoints", (Get-Number $iosHinge 'InnerLongPoints = ([\d.]+)f'), (Get-Number $iosNative 'FDPInnerLongPoints = ([\d.]+)')),
    @("InnerShortPoints", (Get-Number $iosHinge 'InnerShortPoints = ([\d.]+)f'), (Get-Number $iosNative 'FDPInnerShortPoints = ([\d.]+)')),
    @("CoverAreaRatio", (Get-Number $coverHeuristics 'CoverAreaRatio = ([\d.]+)f'), (Get-Number $iosNative 'FDPCoverAreaRatio = ([\d.]+)'))
)
foreach ($pair in $innerSize) {
    Assert-True (($null -ne $pair[1]) -and ($pair[1] -eq $pair[2])) "native $($pair[0]) ($($pair[2])) matches C# ($($pair[1]))"
}
Assert-True ($iosNative -match "FDPInnerLongPoints \* FDPInnerShortPoints \* FDPCoverAreaRatio") "native inner / cover split uses the mirrored constants"
Assert-True ($iosNative -match "endGeneratingDeviceOrientationNotifications") "native launch hold stops device orientation updates when it ends"
Assert-True ($iosNative -match "FDPLaunchHoldDeadline") "native launch hold ends by itself when C# never takes it over"
Assert-True ($iosNative -match "UIInterfaceOrientationIsPortrait\(upright\)\s+\?\s+UIInterfaceOrientationMaskPortrait") "native launch hold gives a tall hold Portrait, like C# (no Upside Down window for an iPhone app)"

Write-Host "=== Posture rules ===" -ForegroundColor Cyan
$holdLock = Get-Content (Join-Path $pkg "Runtime\IphoneDuoHoldLock.cs") -Raw
$hold = Get-Content (Join-Path $pkg "Runtime\IphoneDuoHoldOrientation.cs") -Raw
$settings = Get-Content (Join-Path $pkg "Runtime\IphoneDuoSettings.cs") -Raw
Assert-True ($holdLock -notmatch "#if") "hold lock state is compiled everywhere (EditMode-testable)"
Assert-True ($holdLock -notmatch "\bScreen\.") "hold lock state does no Screen I/O"
Assert-True ($hold -match "#if UNITY_IOS && !UNITY_EDITOR\s+static readonly IphoneDuoHoldLock") "hold Screen I/O runs on iOS players only"
Assert-True ($hold -match "Input\.deviceOrientation") "the hold comes from the device orientation"
Assert-True ($hold -match "case DeviceOrientation\.LandscapeLeft:\s+case DeviceOrientation\.LandscapeRight:\s+flags\.portrait = true;") "laptop hold (landscape device orientation on the inner display) allows only Portrait, so iOS rotates instead of letterboxing (iOS gives an iPhone app no Upside Down window)"
Assert-True ($hold -match "public static int ViewTurns\(") "the view turn makes up for Portrait on the side where Upside Down is upright"
Assert-True ($hold -match "public static IphoneDuoScreenMode Resolve\(") "one pure rule decision shared by the player and the Simulator preview"
Assert-True ($settings -match "Resources\.Load<TextAsset>") "project rules ship as a TextAsset (loads on every build target)"
$runtimeSources = Get-ChildItem (Join-Path $pkg "Runtime") -Filter *.cs | ForEach-Object { Get-Content $_.FullName -Raw }
Assert-True (-not ($runtimeSources -match ":\s*ScriptableObject")) "no ScriptableObject settings (its script would be missing on Android / desktop builds)"
$buildCheck = Get-Content (Join-Path $pkg "Editor\IphoneDuoBuildOrientationCheck.cs") -Raw
Assert-True ($buildCheck -match "IPreprocessBuildWithReport") "iOS builds warn when a fixed Default Orientation keeps Seated locked"
$flatLock = Get-Content (Join-Path $pkg "Runtime\IphoneDuoFlatLock.cs") -Raw
$backend = Get-Content (Join-Path $pkg "Runtime\IphoneDuoPostureBackend.cs") -Raw
Assert-True ($flatLock -notmatch "#if") "flat lock state is compiled everywhere (EditMode-testable)"
Assert-True ($iosNative -match "prefersInterfaceOrientationLocked") "flat inner keeps the interface when held across the lock (Android style)"
Assert-True ($iosNative -match "requestGeometryUpdateWithPreferences") "flat inner flips 180 degrees through an explicit geometry request"
Assert-True ($backend -match "IphoneDuoFlatOrientation\.Tick\(") "posture backend drives the flat lock"
$duoTests = Get-Content (Join-Path $pkg "Tests\Editor\IphoneDuoTests.cs") -Raw
Assert-True ($duoTests -match "new IphoneDuoHoldLock\(\)") "EditMode tests cover the hold lock state"
Assert-True ($duoTests -match "IphoneDuoHoldOrientation\.Resolve\(") "EditMode tests cover every posture rule"
$duoPanel = Get-Content (Join-Path $pkg "Editor\IphoneDuoDeviceSimulatorPlugin.cs") -Raw
Assert-True ($duoPanel -match "BuildTargetOwnsProfile\(landscapeNativeInner: true\)") "iPhone Duo panel is active on the iOS build target only"
Assert-True ($duoPanel -match "FoldableSimulatorPanelUi\.") "iPhone Duo panel shares the Fold & Flip panel UI helper"
Assert-True ($duoPanel -match "IphoneDuoEditorRules\.Resolve\(") "iPhone Duo panel previews the project rules"
$provider = Get-Content (Join-Path $pkg "Editor\IphoneDuoSettingsProvider.cs") -Raw
Assert-True ($provider -match "Project/Responsive UI/iPhone Duo") "rules live under Project Settings > Responsive UI > iPhone Duo"

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
if ($failed -gt 0) { exit 1 } else { exit 0 }
