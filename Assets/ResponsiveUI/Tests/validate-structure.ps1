# Structural gate for ResponsiveUI Runtime/Editor sources (no Unity required).
# Greps files and API names. Does not compile C# — Unity Console / EditMode is the compiler.
# Also fails unqualified System BCL names (StringComparison, …) with no using System.
# Run: powershell -File Assets/ResponsiveUI/Tests/validate-structure.ps1

$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI; docs/sources contain → ◀ ▶.
$PSDefaultParameterValues['Get-Content:Encoding'] = 'UTF8'
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
$runtime = Join-Path $pkg "Runtime"
$editor = Join-Path $pkg "Editor"

function EditorPath([string]$name) {
    $folder = @{
        "ResponsiveUIEditor.cs" = "Hub"
        "ResponsiveUiDetectionDrawer.cs" = "Hub"
        "ResponsiveUiInspectorFoldouts.cs" = "Hub"
        "ResponsiveUiSlotClipboard.cs" = "Hub"
        "ResponsiveUiBulkCopyUtil.cs" = "Hub"
        "ResponsiveUiBulkSlotCopyPanel.cs" = "Hub"
        "ResponsiveUiHubEditorUtil.cs" = "Hub"
        "ResponsiveLayoutEventsDrawer.cs" = "Hub"
        "ResponsiveLayoutEventsDrawer.cs.meta" = "Hub"
        "ResponsiveFormTransitionLoadingDrawer.cs" = "Hub"
        "ResponsiveFormTransitionLoadingDrawer.cs.meta" = "Hub"
        "ResponsiveUiSimulatorSlotFocus.cs" = "Simulator"
        "ResponsiveUiSimulatorLayoutResolver.cs" = "Simulator"
        "ResponsiveUiEditorLayoutScheduler.cs" = "Simulator"
        "ResponsiveUiHingeGuideOverlay.cs" = "Simulator"
        "ResponsiveUiPathUtil.cs" = "Package"
        "ResponsiveUiPackageExport.cs" = "Package"
        "ResponsiveUiPackageSetup.cs" = "Package"
        "ResponsiveUiSampleSetup.cs" = "Package"
        "ResponsiveUiEditorSettings.cs" = "Package"
        "ResponsiveUiSettingsProvider.cs" = "Package"
        "ResponsiveUiSettingsWatchers.cs" = "Package"
        "ResponsiveUiEditorReloadGuard.cs" = "Simulator"
        "ResponsiveUiControllerEditor.cs" = "Widgets"
        "ResponsiveUiWidgetSync.cs" = "Widgets"
        "ResponsiveUiEditorCaptureGuard.cs" = "Widgets"
        "ResponsiveRectStateEditorUtil.cs" = "Widgets"
        "ResponsiveRectStateDrawer.cs" = "Widgets"
        "ResponsiveWidgetLayoutDrawer.cs" = "Widgets"
        "ResponsiveTwoSideWidgetDrawer.cs" = "Widgets"
        "ResponsiveTwoSideUiTweenControllerEditor.cs" = "Widgets"
        "ResponsiveActiveObjectEntryDrawer.cs" = "Widgets"
        "ResponsiveActiveObjectSlotsEditorUtil.cs" = "Widgets"
        "ResponsiveBackgroundDrawer.cs" = "Widgets"
        "ResponsiveBackgroundDrawer.cs.meta" = "Widgets"
        "ResponsiveCameraEditor.cs" = "Camera"
        "ResponsiveCameraStateDrawer.cs" = "Camera"
        "ResponsiveCameraLayoutDrawer.cs" = "Camera"
        "ResponsiveCameraSync.cs" = "Camera"
        "ResponsiveCameraEditorUtil.cs" = "Camera"
        "ResponsiveScrollViewEditor.cs" = "Scroll"
        "ResponsiveScrollStateDrawer.cs" = "Scroll"
        "ResponsiveScrollLayoutDrawer.cs" = "Scroll"
        "ResponsiveScrollEditorUtil.cs" = "Scroll"
        "ResponsiveScrollSync.cs" = "Scroll"
        "ResponsiveTextBannerEditor.cs" = "Banner"
    }
    if ($folder.ContainsKey($name)) {
        return Join-Path $editor (Join-Path $folder[$name] $name)
    }
    return Join-Path $editor $name
}

$sharedEditor = Join-Path $pkg "SharedEditor"
$failed = 0
$passed = 0
$skipped = 0

function Assert-True($cond, $msg) {
    if ($cond) {
        Write-Host "PASS  $msg" -ForegroundColor Green
        $script:passed++
    } else {
        Write-Host "FAIL  $msg" -ForegroundColor Red
        $script:failed++
    }
}

function Assert-Match($src, [string]$pattern, $msg) {
    Assert-True ([regex]::IsMatch([string]$src, $pattern)) $msg
}

function Assert-NotMatch($src, [string]$pattern, $msg) {
    Assert-True (-not [regex]::IsMatch([string]$src, $pattern)) $msg
}

# Host-project assets (scenes, prefabs) are not part of the package. In a project that does not
# have them the block that reads them is skipped, not failed; the summary reports the count so a
# developer can see what did not run.
function Skip-Host([string]$msg) {
    Write-Host "SKIP  $msg" -ForegroundColor Yellow
    $script:skipped++
}

Write-Host "ResponsiveUI root: $pkg" -ForegroundColor Cyan

$requiredRuntime = @(
    "ResponsiveDevice.cs",
    "ResponsiveUiHandler.cs",
    "ResponsiveActiveObjects.cs",
    "ResponsiveLayoutEvents.cs",
    "IResponsiveUiClient.cs",
    "ResponsiveUiController.cs",
    "ResponsiveTwoSideUiTweenController.cs",
    "ResponsiveScrollView.cs",
    "ResponsiveTextBanner.cs",
    "ResponsiveUI.cs",
    "ResponsiveBackgroundCover.cs",
    "ResponsiveDesignScale.cs",
    "ResponsiveFormTransitionLoading.cs",
    "ResponsiveUiHingeGuide.cs",
    "ResponsiveCamera.cs",
    "ResponsiveCameraState.cs",
    "ResponsiveCameraLayout.cs",
    "ResponsiveCameraSpriteFit.cs",
    "ResponsiveCameraFocus.cs",
    "ResponsiveRectState.cs",
    "ResponsiveWidgetLayout.cs",
    "ResponsiveWidgetSlotResolver.cs",
    "ResponsiveScrollState.cs",
    "ResponsiveScrollLayout.cs",
    "ResponsiveScrollDriver.cs",
    "ResponsiveUiApplyOrder.cs",
    "com.futuredigitalplus.responsiveui.asmdef"
)

foreach ($f in $requiredRuntime) {
    Assert-True (Test-Path (Join-Path $runtime $f)) "runtime file $f"
}

$requiredEditor = @(
    "Hub\ResponsiveUIEditor.cs",
    "Hub\ResponsiveFormTransitionLoadingDrawer.cs",
    "Camera\ResponsiveCameraEditor.cs",
    "Camera\ResponsiveCameraStateDrawer.cs",
    "Camera\ResponsiveCameraLayoutDrawer.cs",
    "Camera\ResponsiveCameraSync.cs",
    "Camera\ResponsiveCameraEditorUtil.cs",
    "Widgets\ResponsiveRectStateEditorUtil.cs",
    "Widgets\ResponsiveRectStateDrawer.cs",
    "Widgets\ResponsiveWidgetLayoutDrawer.cs",
    "Widgets\ResponsiveTwoSideWidgetDrawer.cs",
    "Widgets\ResponsiveTwoSideUiTweenControllerEditor.cs",
    "Hub\ResponsiveUiDetectionDrawer.cs",
    "Hub\ResponsiveUiInspectorFoldouts.cs",
    "Hub\ResponsiveUiSlotClipboard.cs",
    "Hub\ResponsiveUiBulkCopyUtil.cs",
    "Hub\ResponsiveUiBulkSlotCopyPanel.cs",
    "Hub\ResponsiveUiHubEditorUtil.cs",
    "Widgets\ResponsiveActiveObjectEntryDrawer.cs",
    "Widgets\ResponsiveActiveObjectSlotsEditorUtil.cs",
    "Widgets\ResponsiveBackgroundDrawer.cs",
    "Hub\ResponsiveLayoutEventsDrawer.cs",
    "Widgets\ResponsiveUiControllerEditor.cs",
    "Scroll\ResponsiveScrollViewEditor.cs",
    "Banner\ResponsiveTextBannerEditor.cs",
    "Scroll\ResponsiveScrollStateDrawer.cs",
    "Scroll\ResponsiveScrollLayoutDrawer.cs",
    "Scroll\ResponsiveScrollEditorUtil.cs",
    "Scroll\ResponsiveScrollSync.cs",
    "Package\ResponsiveUiEditorSettings.cs",
    "Package\ResponsiveUiSettingsProvider.cs",
    "Package\ResponsiveUiPackageExport.cs",
    "Package\ResponsiveUiPackageSetup.cs",
    "Package\ResponsiveUiPathUtil.cs",
    "Widgets\ResponsiveUiWidgetSync.cs",
    "Widgets\ResponsiveUiEditorCaptureGuard.cs",
    "Simulator\ResponsiveUiSimulatorSlotFocus.cs",
    "Simulator\ResponsiveUiEditorLayoutScheduler.cs",
    "Simulator\ResponsiveUiSimulatorLayoutResolver.cs",
    "Simulator\ResponsiveUiHingeGuideOverlay.cs",
    "Package\ResponsiveUiSampleSetup.cs",
    "Banner.meta",
    "Camera.meta",
    "Hub.meta",
    "Package.meta",
    "Scroll.meta",
    "Simulator.meta",
    "Widgets.meta",
    "com.futuredigitalplus.responsiveui.editor.asmdef"
)

foreach ($f in $requiredEditor) {
    $path = Join-Path $editor $f
    Assert-True (Test-Path $path) "editor file $f"
    if ((Test-Path $path) -and $f -like "*.cs") {
        $src = Get-Content $path -Raw
        Assert-True ($src -notmatch "BeginFoldoutHeaderGroup") "$f does not nest Foldout Headers"
    }
}

# AssemblyInfo.cs (assembly attributes) is the only C# allowed at the Editor/ root.
$strayCs = @(Get-ChildItem -LiteralPath $editor -File | Where-Object { $_.Extension -eq '.cs' -and $_.Name -ne 'AssemblyInfo.cs' })
Assert-True ($strayCs.Count -eq 0) "layout editor C# lives in Package/Hub/Simulator/Widgets/Camera/Scroll/Banner"
Assert-True (Test-Path (Join-Path $editor "AssemblyInfo.cs")) "editor assembly attributes sit at the Editor/ root"

$requiredSharedEditor = @(
    "ResponsiveUiEditorPath.cs",
    "ResponsiveUiSimulatorList.cs",
    "ResponsiveUiSimulatorName.cs",
    "ResponsiveUiInspectorRepaint.cs",
    "ResponsiveUiObjectReference.cs",
    "ResponsiveUiEditorMenus.cs",
    "com.futuredigitalplus.responsiveui.shared.editor.asmdef"
)
foreach ($f in $requiredSharedEditor) {
    Assert-True (Test-Path (Join-Path $sharedEditor $f)) "shared editor file $f"
}
$sharedEditorAsm = Get-Content (Join-Path $sharedEditor "com.futuredigitalplus.responsiveui.shared.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True ($sharedEditorAsm.rootNamespace -eq "FDPlus.ResponsiveUI.Shared.Editor") "shared editor has its own namespace (modules do not look like layout-editor dependents)"
$sharedNsMiss = @(Get-ChildItem -LiteralPath $sharedEditor -Filter "*.cs" | Where-Object { (Get-Content $_.FullName -Raw) -notmatch "namespace FDPlus\.ResponsiveUI\.Shared\.Editor" })
Assert-True ($sharedNsMiss.Count -eq 0) "every SharedEditor script is in FDPlus.ResponsiveUI.Shared.Editor"

$repaint = Get-Content (Join-Path $sharedEditor "ResponsiveUiInspectorRepaint.cs") -Raw
Assert-True ($repaint -match "isPlayingOrWillChangePlaymode") "shared Inspector soft-repaint skips Play Mode"
Assert-True ($repaint -match "EventType\.MouseDown") "shared Inspector soft-repaint avoids mid-click Repaint"
Assert-True ($repaint -match "public static void Queue") "shared Inspector soft-repaint has Queue API"

$rootEditorAsm = Get-Content (EditorPath "com.futuredigitalplus.responsiveui.editor.asmdef") -Raw
$foldEditorAsm = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\com.futuredigitalplus.foldandflip.editor.asmdef") -Raw
$phoneEditorAsm = Get-Content (Join-Path $pkg "PhonePad\Editor\com.futuredigitalplus.phonepad.simulator.editor.asmdef") -Raw
Assert-True ($rootEditorAsm -match "com\.futuredigitalplus\.responsiveui\.shared\.editor") "root editor references shared infrastructure"
Assert-True ($foldEditorAsm -match "com\.futuredigitalplus\.responsiveui\.shared\.editor") "FoldAndFlip editor references shared infrastructure"
Assert-True ($phoneEditorAsm -match "com\.futuredigitalplus\.responsiveui\.shared\.editor") "PhonePad editor references shared infrastructure"

$rectState = Get-Content (Join-Path $runtime "ResponsiveRectState.cs") -Raw
Assert-True ($rectState -notmatch "struct ResponsiveWidgetLayout") `
    "ResponsiveWidgetLayout is not defined inside ResponsiveRectState.cs"
Assert-True ($rectState -match "(?m)^\s*public bool enabled;") "widget Enabled has no true field default"
Assert-True ($rectState -notmatch "public bool enabled = true") "missing YAML Cover stays Disabled (no enabled = true field)"
Assert-True ($rectState -match "CreateDefault\(bool enabled = true\)") "widget CreateDefault exists for slot Reset"
Assert-True ($rectState -match "designScaleMode = ResponsiveDesignScaleMode.Auto") "new widget slots default Scale Mode Auto"
Assert-True ($rectState -notmatch "designCanvasSize = GetDefaultDesignCanvasSize") "From does not bake catalog Design Canvas pixels"
Assert-True ($rectState -match "HasSameLayout") "widget layout equality is shared with camera Focus Bounds"
Assert-True ($rectState -match "MatchesApplied") "widget/camera capture skips an Apply-scaled live rect"

$widgetLayout = Get-Content (Join-Path $runtime "ResponsiveWidgetLayout.cs") -Raw
Assert-True ($widgetLayout -match "public static readonly ResponsiveDevice\.FormFactor\[] FormFactors") "FormFactors is the canonical form order"
Assert-True ($widgetLayout -match "(?s)FormFactors\s*=\s*\{\s*ResponsiveDevice\.FormFactor\.Phone,\s*ResponsiveDevice\.FormFactor\.Pad,\s*ResponsiveDevice\.FormFactor\.FoldPhoneCover,\s*ResponsiveDevice\.FormFactor\.FoldHalfOpened,\s*ResponsiveDevice\.FormFactor\.FoldCoverHalfOpened,\s*ResponsiveDevice\.FormFactor\.FlipPhoneCover,\s*ResponsiveDevice\.FormFactor\.FlipHalfOpened,\s*ResponsiveDevice\.FormFactor\.iPhoneDuoCover,\s*ResponsiveDevice\.FormFactor\.iPhoneDuoSeated,\s*ResponsiveDevice\.FormFactor\.WideScreen,") "FormFactors order is Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened, iPhone Duo Cover, iPhone Duo Seated, Wide Screen"
Assert-True ($widgetLayout -match '(?s)StatePropertyNames\s*=\s*\{[^}]*"phonePortrait".*"phoneLandscape".*"padPortrait".*"padLandscape".*"foldPhoneCoverPortrait".*"foldPhoneCoverLandscape".*"foldHalfOpenedPortrait".*"foldHalfOpenedLandscape".*"foldCoverHalfOpenedPortrait".*"foldCoverHalfOpenedLandscape".*"flipPhoneCoverPortrait".*"flipPhoneCoverLandscape".*"flipHalfOpenedPortrait".*"flipHalfOpenedLandscape".*"iPhoneDuoCoverPortrait".*"iPhoneDuoCoverLandscape".*"iPhoneDuoSeatedPortrait".*"iPhoneDuoSeatedLandscape".*"wideScreenPortrait".*"wideScreenLandscape"') "StatePropertyNames follow FormFactors order (twenty slots)"
Assert-True ($widgetLayout -match "FormFactors\[slotIndex >> 1\]") "slot index → form is derived from FormFactors (form i owns slots 2i, 2i + 1)"
$device = Get-Content (Join-Path $runtime "ResponsiveDevice.cs") -Raw
Assert-Match $device 'Phone\s*=\s*0,[\s\S]{0,400}Pad\s*=\s*1,[\s\S]{0,400}FoldPhoneCover\s*=\s*2,[\s\S]{0,400}FoldHalfOpened\s*=\s*3,[\s\S]{0,400}FoldCoverHalfOpened\s*=\s*4,[\s\S]{0,400}FlipPhoneCover\s*=\s*5,[\s\S]{0,400}FlipHalfOpened\s*=\s*6,[\s\S]{0,400}iPhoneDuoCover\s*=\s*7,[\s\S]{0,400}iPhoneDuoSeated\s*=\s*8,[\s\S]{0,400}WideScreen\s*=\s*9,' "FormFactor values are the slot order (Phone 0 … Wide Screen 9)"
Assert-True ($device -match "(?s)RefineFoldHalfOpened.*IsKnownIphoneDuoInnerSize\(width, height\)\)\s*return FormFactor\.iPhoneDuoSeated") "iPhone Duo inner HalfOpened maps to its own form"
$slotResolver = Get-Content (Join-Path $runtime "ResponsiveWidgetSlotResolver.cs") -Raw
Assert-True ($slotResolver -match "MaxCandidates = 5") "slot resolver has room for Duo → Pad ×2 → Phone ×2"
Assert-True ($slotResolver -match "GetPadFirstCandidate") "disabled iPhone Duo Seated uses the Pad-first fallback chain"
Assert-Match $slotResolver 'UsesPadFirstChain\(ResponsiveDevice\.FormFactor form\) =>\s*form == ResponsiveDevice\.FormFactor\.iPhoneDuoSeated;' "Pad-first chain is iPhone Duo Seated only"
Assert-True ($slotResolver -notmatch "public static bool UsesPadFirstChain") "Pad-first chain test stays private to the resolver"
Assert-Match $slotResolver 'key\.Form == ResponsiveDevice\.FormFactor\.WideScreen[\s\S]{0,400}return index == 0 \|\| index == 1;' "Wide Screen falls back only to its other orientation (never mobile slots)"
Assert-Match $slotResolver 'ResponsivePlatforms\.IsActiveForm\(form\)' "resolver skips another platform's slots"
Assert-True ($slotResolver -match "ResponsiveDevice\.Orientation screen = key\.Orientation;") "iPhone Duo Seated falls back by screen shape (its label names the shape)"
Assert-Match $slotResolver 'form = index <= 2\s*\?\s*ResponsiveDevice\.FormFactor\.Pad\s*:\s*ResponsiveDevice\.FormFactor\.Phone' "iPhone Duo Seated tries Pad before Phone"
Assert-True ($device -notmatch "Orientation ScreenOrientation\(") "iPhone Duo Seated labels name the screen shape (no label swap helper)"
Assert-Match $device 'OrientationForForm\(FormFactor form, Orientation screen\)[\s\S]{0,200}IsFoldHalfOpenForm\(form\)[\s\S]{0,200}Orientation\.Landscape \? Orientation\.Portrait : Orientation\.Landscape' "Fold / Fold Cover Half Opened slots name the posture (tall = Landscape), other forms the screen shape"
Assert-True (-not ($device -match "IgnoresPlayerOrientationLock")) "Fold half-open slots follow the Player axes like every other posture-named slot"
Assert-Match $device 'SlotAllowedByPlayerAxes\([\s\S]{0,1200}slotOrientation == Orientation\.Landscape\s*\?\s*landscapeAllowed\s*:\s*portraitAllowed' "slots need the Player axis of their screen shape (iPhone Duo Seated always allowed)"
Assert-True ((Get-Content (EditorPath "ResponsiveUiEditorSettings.cs") -Raw) -match "ResponsiveDevice\.SlotAllowedByPlayerAxes\(") "Editor SlotAllowed uses the shared screen-axis rule"
Assert-True ((Get-Content (EditorPath "ResponsiveUiEditorSettings.cs") -Raw) -match "ResponsivePlatforms\.SeedsEnabled\(form\)") "Editor SlotAllowed seeds Wide Screen Enabled on Wide Screen targets only"
Assert-Match (Get-Content (Join-Path $runtime "ResponsiveScrollLayout.cs") -Raw) 'bool enable =[\s\S]{0,200}ResponsivePlatforms\.SeedsEnabled\(' "scroll SeedAll seeds Wide Screen Enabled on Wide Screen targets only"
Assert-Match $widgetLayout 'void Apply\(ResponsiveUI hub[\s\S]{0,400}out ResponsiveRectState state\)\)\s*return;' "hub widget Apply leaves the Target as is with no enabled candidate"
Assert-Match $widgetLayout 'void Apply\(ResponsiveDevice\.LayoutKey key, Vector2 currentCanvasSize\)[\s\S]{0,400}out ResponsiveRectState state\)\)\s*return;' "widget Apply leaves the Target as is with no enabled candidate"
$activeObjects = Get-Content (Join-Path $runtime "ResponsiveActiveObjects.cs") -Raw
Assert-True ($activeObjects -match "case 16: return ref slots\.iPhoneDuoSeatedPortrait;") "iPhone Duo Seated Active Objects are plain slots"
Assert-True ($activeObjects -match "case 19: return ref slots\.wideScreenLandscape;") "Active Objects cover all twenty slots"
Assert-True ($activeObjects -notmatch "iPhoneDuoSeatedAuthored") "Active Objects have no Duo authored flag (no Pad mirror)"
$platforms = Get-Content (Join-Path $runtime "ResponsivePlatform.cs") -Raw
Assert-True ($platforms -match "enum ResponsivePlatform") "ResponsivePlatform names Android / iOS / Wide Screen"
Assert-Match $platforms '#if UNITY_ANDROID\s+ResponsivePlatform\.Android;\s+#elif UNITY_IOS\s+ResponsivePlatform\.Ios;\s+#else\s+ResponsivePlatform\.WideScreen;' "platform follows the build target (Standalone and WebGL are Wide Screen)"
Assert-Match $device 'CurrentFormFactor => DetectActiveFormFactor\(GetWatchScreenSize\(\)\)' "CurrentFormFactor is DetectActiveFormFactor on the watch size (one detection chain, never another platform's form)"
Assert-True ($device -notmatch "DetectCurrentFormFactor") "no second form-detection chain"
Assert-Match $device 'DetectActiveFormFactor\([\s\S]{0,900}ResponsivePlatforms\.ConstrainDetected' "DetectActiveFormFactor constrains players to the build target (Editor Simulator profile is the exception)"
Assert-True ((Get-Content (EditorPath "ResponsiveUiDetectionDrawer.cs") -Raw) -match "ResponsiveUiEditorSettings\.SlotVisible\(stateName\)") "Inspectors draw slots through SlotVisible (Android and iOS groups)"
Assert-True ($widgetLayout -match "ResolveActiveSlot") "ResolveActiveSlot exists"
Assert-True ($widgetLayout -match "out ResponsiveDevice.Orientation sourceOrientation") "slot resolve reports source orientation without a name round-trip"
Assert-True ($widgetLayout -match "void Apply\(ResponsiveUI hub") "widget Apply writes through the hub so Design Scale ratio is solved once"
Assert-True ($widgetLayout -match "UsesDesignScale") "UsesDesignScale exists"
Assert-True ($widgetLayout -match "SlotOrientation") "SlotOrientation exists"
Assert-True ($widgetLayout -match "IsHalfOpenedSlot") "IsHalfOpenedSlot exists"
Assert-True ($widgetLayout -notmatch "IsFlipHalfOpenedSlot|IsFoldPhoneCoverSlot") "no unused per-form slot helpers"
Assert-True ($widgetLayout -match "IsSeedDisabledSlot") "Flip Cover Landscape starts Disabled"
Assert-True ($widgetLayout -notmatch "ApplyTargetActive") "controller widgets do not SetActive Targets from slot Enabled"
Assert-True ($widgetLayout -match "IsExactLayoutSlot") "exact live slot is distinct from Cover to Phone fallback"
Assert-True ($widgetLayout -match "IsFlipPhoneCoverSlot") "IsFlipPhoneCoverSlot exists"
Assert-True ($widgetLayout -match "IsOptInSlot") "IsOptInSlot exists"
Assert-True ($widgetLayout -match "foldPhoneCoverPortrait") "Fold Cover widget slots exist"
Assert-True ($widgetLayout -match "flipPhoneCoverPortrait") "Flip Cover widget slots exist"
Assert-True ($widgetLayout -match "foldCoverHalfOpenedPortrait") "Fold Cover Half Opened widget slots exist"
Assert-True ($widgetLayout -match "ResponsiveWidgetSlotResolver") "runtime layout uses shared slot fallback resolver"
Assert-True ($widgetLayout -match "StatePropertyIndex\(in ResponsiveDevice.LayoutKey") "layout keys map to slot indexes without a name round-trip"
Assert-True ($widgetLayout -match "GetStatePropertyIndex") "unknown property names do not collapse to Phone Portrait"
Assert-True ($widgetLayout -notmatch "flipClosedPortrait") "Flip Cover YAML field is flipPhoneCoverPortrait, not flipClosedPortrait"

$slotResolver = Get-Content (Join-Path $runtime "ResponsiveWidgetSlotResolver.cs") -Raw
Assert-True ($slotResolver -match "FormFactor\.Pad") "disabled Pad slots participate in Phone fallback"
$slotResolverTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveWidgetSlotResolverTests.cs") -Raw
Assert-True ($slotResolverTests -match "FlipPhoneCoverLandscape_CandidatesFallBackToPhone") "Unity tests Flip Cover landscape falls back to Phone"
Assert-True ($slotResolverTests -match "RuntimeResolver_UsesPhoneAfterDisabledFlipPhoneCoverSlots") "Unity tests disabled Flip Cover slots reuse Phone"
Assert-True ($slotResolverTests -match "RuntimeResolver_UsesPhoneAfterDisabledPadSlots") "Unity tests disabled Pad slots reuse Phone"
Assert-True ($slotResolverTests -match "RuntimeResolver_UsesPhoneLandscapeAfterDisabledFoldSlots") "Unity tests disabled Fold slots keep the Fold fallback test"
Assert-True ($slotResolverTests -notmatch '\}\r?\n    \{') "slot resolver tests have no orphaned method body"

Get-ChildItem -LiteralPath (Join-Path $pkg "Tests\Editor") -Filter *.cs | ForEach-Object {
    $raw = Get-Content $_.FullName -Raw
    Assert-True ($raw -notmatch '\}\r?\n    \{') "$($_.Name) has no orphaned class-level method body"
}

$twoSide = Get-Content (Join-Path $runtime "ResponsiveTwoSideUiTweenController.cs") -Raw
Assert-True ($twoSide -match "IResponsiveUiClient") "two-side UI subscribes to the ResponsiveUI hub"
Assert-True ($twoSide -match "GetHierarchyOrder") "two-side UI applies parent widgets before children"
Assert-True ($twoSide -match "_hierarchyDepths") "two-side hierarchy sort caches depth once per widget"
Assert-True ($twoSide -match "return ref widget.leftNormal") "two-side apply reads layouts by ref without copying sibling slots"
Assert-True ($twoSide -match "RebindIfChanged") "two-side rebinds when Hub is reassigned while enabled"
Assert-True ($twoSide -match "if \(layoutOnly\)") "two-side layout-only passes do not stop a running tween"
Assert-True ($twoSide -match "WorldCenter") "two-side animation preserves visual position across anchor changes"
Assert-True ($twoSide -match "delayComplete") "two-side instant transitions honor delay"
Assert-True ($twoSide -match "animationEnabled") "two-side widgets can disable animation independently"
Assert-True ($twoSide -match "leftNormal" -and $twoSide -match "leftAnimationStart") `
    "two-side widgets provide Normal and Animation Start layouts"
Assert-True ($twoSide -notmatch "ResponsiveUiVisibility") "two-side UI has no serialized Visibility mode"
# Row drawer + controller Inspector + shared bulk-copy panel, checked as one surface.
$twoSideDrawer = (Get-Content (EditorPath "ResponsiveTwoSideWidgetDrawer.cs") -Raw) +
    (Get-Content (EditorPath "ResponsiveTwoSideUiTweenControllerEditor.cs") -Raw) +
    (Get-Content (EditorPath "ResponsiveUiBulkSlotCopyPanel.cs") -Raw)
Assert-True ($twoSideDrawer -match "DrawTarget" -and $twoSideDrawer -match "OnGUIWithoutTarget") `
    "two-side widget draws one Target above its two target-free layouts"
Assert-Match $twoSideDrawer 'firstAssign[\s\S]*SeedSlots\(layout\)[\s\S]*ApplyInspectorTargetChange' `
    "new two-side layouts enable Scale From Design Canvas before capture"
Assert-Match $twoSideDrawer '!firstAssign && !cleared[\s\S]*ApplyModifiedProperties\(\)[\s\S]*return' `
    "live two-side Target replacement preserves all authored layouts"
Assert-Match $twoSideDrawer 'GUILayout\.Button\("Play"\)' "two-side Inspector provides animation Play control"
Assert-Match $twoSideDrawer '"Pause"' "two-side Inspector provides animation Pause control"
Assert-Match $twoSideDrawer 'GUILayout\.Button\("Stop"\)' "two-side Inspector provides animation Stop control"
Assert-True ($twoSideDrawer -match "ForEachNewRow") "two-side Inspector resets new rows before the list draws"
Assert-True ($twoSideDrawer -match "IgnoreTargetCapture") `
    "two-side animation preview does not overwrite authored layouts"
Assert-True ($twoSideDrawer -match "DisabledScope\(Application\.isPlaying\)") `
    "two-side animation preview controls are disabled in Play Mode"
Assert-True ($twoSideDrawer -match "RegisterCompleteObjectUndo") `
    "two-side asynchronous animation preview records complete Undo state"
Assert-True ($twoSideDrawer -match "DrawBulkCopyPanel") "two-side Inspector uses the shared bulk-copy panel"
Assert-True ($twoSideDrawer -match "CopySlotAcrossTwoSideWidgets") "two-side bulk copy writes the currently shown layout"
Assert-True ($twoSideDrawer -match "SeedTwoSideWidget") "two-side Inspector seeds all four layouts on new rows"
Assert-True ($twoSideDrawer -match "ConsumeSeedDefaultLayout") "two-side Inspector does not seed existing empty widget lists"
Assert-True ($twoSideDrawer -match "EnsureHubField") "two-side Inspector auto-assigns the Canvas hub"
Assert-True ($twoSideDrawer -match "InspectorSectionKey") "two-side Inspector foldouts are per component"
Assert-True ($twoSideDrawer -match "BoxedFoldoutSection") "two-side Widgets panel uses the shared boxed parent layout"
Assert-True ($twoSideDrawer -match "Layout Authoring") "two-side Inspector groups Side / Preview under Layout Authoring"
Assert-True ($twoSideDrawer -match 'InspectorKey\("Preview"\)') "two-side Inspector groups Play / Hide under Preview"
Assert-True ($twoSideDrawer -match "DrawSlotSection") "two-side widget rows use collapsible tinted Widget / Tween sections"
Assert-True ($twoSideDrawer -match "DrawPropertyFoldout") "two-side Widget / Tween sit under the item title"
Assert-True ($twoSideDrawer -match "DrawGuiFoldout") "two-side bulk-copy fields sit under their foldout titles"
Assert-True ($twoSideDrawer -match "DrawHubField") "two-side Hub field uses the shared label column"
Assert-True ($twoSideDrawer -notmatch "indentLevel\+\+") "two-side item children do not use a second Unity indent"
Assert-True ($twoSideDrawer -match '"Tween"') "two-side widget rows group animation fields under Tween"
Assert-True ($twoSideDrawer -match "Bulk Copy Widget Slots") "two-side Inspector exposes bulk widget slot copy"
Assert-True ($twoSideDrawer -match "Bulk Copy Side") "two-side Inspector exposes Left ↔ Right bulk copy"
Assert-True ($twoSideDrawer -match "Copy Right") "two-side Inspector can copy Right onto Left"
Assert-True ($twoSideDrawer -match "CopySideAcrossTwoSideWidgets") "two-side side copy writes Normal and Animation Start"
Assert-Match $twoSideDrawer 'DrawGuiFoldout\(\s*"Bulk Copy Widget"' "two-side Inspector exposes widget-to-widget copy"
Assert-True ($twoSideDrawer -match "CopyWidgetSideAcrossTwoSideWidgets") "two-side widget copy writes one chosen side"
Assert-True ($twoSideDrawer -match "SwapWidgetSideAcrossTwoSideWidgets") "two-side widget swap exchanges one chosen side"
Assert-True ($twoSideDrawer -match "currently shown Side") "two-side slot bulk copy help names the shown Side + Preview"
Assert-True ($twoSideDrawer -match 'FindProperty\("side"\)\.intValue = \(int\)destinationSide') "two-side side copy previews the destination Side"
Assert-True ($twoSide -match "ConsumeSeedDefaultLayout") "two-side Add Component seeds one widget only on Reset"
Assert-True ($twoSide -match "LayoutPropertyNames") "two-side widgets name their four layouts in one shared list"
Assert-True ($twoSide -notmatch "MovedFrom") "two-side tween controller has no MovedFrom shim (no old data)"
$twoSideMeta = Get-Content (Join-Path $runtime "ResponsiveTwoSideUiTweenController.cs.meta") -Raw
Assert-True ($twoSideMeta -match "a49dc6cb5f6a47c5b55cc24ed9c47568") `
    "two-side tween controller preserves its scene script GUID"
$cameraScriptMeta = Get-Content (Join-Path $runtime "ResponsiveCamera.cs.meta") -Raw
Assert-True ($cameraScriptMeta -match "d4e5f60718293a4b5c6d7e8f901a2b3c") `
    "camera script GUID preserved for 8BallPool"
$hubScriptMeta = Get-Content (Join-Path $runtime "ResponsiveUI.cs.meta") -Raw
Assert-True ($hubScriptMeta -match "c8f4e2a91b3d4f6e8a7c5d9e1f0b2a34") `
    "hub script GUID preserved for scene Canvas references"

$guidMap = @{}
$dupGuids = New-Object System.Collections.Generic.List[string]
$metaFiles = @(Get-ChildItem -LiteralPath $pkg -Filter *.meta -Recurse)
$folderMeta = Join-Path (Split-Path $pkg) "ResponsiveUI.meta"
if (Test-Path $folderMeta) { $metaFiles += Get-Item -LiteralPath $folderMeta }
$pkgRoot = $pkg.Path
foreach ($meta in $metaFiles) {
    $guidLine = Select-String -LiteralPath $meta.FullName -Pattern '^guid: ([0-9a-f]{32})$' |
        Select-Object -First 1
    if (-not $guidLine) { continue }
    $guid = $guidLine.Matches[0].Groups[1].Value
    $rel = if ($meta.FullName.StartsWith($pkgRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $meta.FullName.Substring($pkgRoot.Length).TrimStart('\', '/')
    } else {
        "ResponsiveUI.meta"
    }
    if ($guidMap.ContainsKey($guid)) {
        $dupGuids.Add("$guid ($($guidMap[$guid]) vs $rel)")
    } else {
        $guidMap[$guid] = $rel
    }
}
if ($dupGuids.Count -eq 0) {
    Assert-True ($guidMap.Count -gt 0) "ResponsiveUI .meta GUIDs are unique ($($guidMap.Count))"
} else {
    foreach ($dup in $dupGuids) {
        Assert-True $false "duplicate GUID $dup"
    }
}
$folderGuid = Get-Content $folderMeta -Raw
Assert-True ($folderGuid -notmatch "cb9e544b143cf452ebd9e1a9e424c91b") `
    "ResponsiveUI folder GUID is not the retired Containers.meta GUID"
$preservedSequential = @("d4e5f60718293a4b5c6d7e8f901a2b3c")
$placeholderGuids = New-Object System.Collections.Generic.List[string]
foreach ($guid in $guidMap.Keys) {
    if ($preservedSequential -contains $guid) { continue }
    if ($guid.StartsWith("a1b2c3d4e5f6")) {
        $placeholderGuids.Add("$guid in $($guidMap[$guid])")
        continue
    }
    $hits = 0
    for ($i = 0; $i -lt 30; $i += 2) {
        $a = [Convert]::ToInt32($guid.Substring($i, 2), 16)
        $b = [Convert]::ToInt32($guid.Substring($i + 2, 2), 16)
        if (($b - $a) -eq 17) { $hits++ }
    }
    if ($hits -ge 8) { $placeholderGuids.Add("$guid in $($guidMap[$guid])") }
}
if ($placeholderGuids.Count -eq 0) {
    Assert-True $true "ResponsiveUI GUIDs are not sequential placeholders"
} else {
    foreach ($item in $placeholderGuids) {
        Assert-True $false "sequential placeholder GUID $item"
    }
}

$device = Get-Content (Join-Path $runtime "ResponsiveDevice.cs") -Raw
Assert-True ($device -match "FoldPhoneCover\s*=\s*2") "FormFactor.FoldPhoneCover is 2 (Fold Cover)"
Assert-True ($device -match "FlipPhoneCover\s*=\s*5") "FormFactor.FlipPhoneCover is 5"
Assert-True ($device -match "FoldCoverHalfOpened\s*=\s*4") "FormFactor.FoldCoverHalfOpened is 4"
Assert-True ($device -match 'FoldPhoneCover => "Fold Cover"') "GetFormDisplayName uses Fold Cover"
Assert-True ($device -match "Fold Cover Half Opened") "GetFormDisplayName uses Fold Cover Half Opened"
Assert-True ($device -match "IsKnownFoldCoverHalfOpenedSize") "Fold8 HalfOpened size recognized"
Assert-True ($device -match "RefineFoldHalfOpened") "RefineFoldHalfOpened upgrades Fold8 HalfOpened"
Assert-True ($device -match "RefineWideCover") "RefineWideCover upgrades Fold8 / iPhone Duo Cover"
Assert-Match $device 'MapKnownWideCoverSize[\s\S]{0,400}MatchesIphoneDuoSizes && IsKnownIphoneDuoCoverSize[\s\S]{0,120}FormFactor\.iPhoneDuoCover' "iPhone Duo Cover WxH maps to its own form (not on Android players)"
Assert-Match $device '#if UNITY_IOS && !UNITY_EDITOR[\s\S]{0,400}if \(form == FormFactor\.Phone\)\s*return FormFactor\.iPhoneDuoCover;' "iOS players map a closed / standing iPhone Duo to its Cover form whatever the render size"
Assert-True ($device -match "RefineFlipPhoneCover") "RefineFlipPhoneCover upgrades Flip Cover"
Assert-True ($device -match "RefineCoverForms") "RefineCoverForms runs Fold then Flip cover upgrades"
Assert-True ($device -match "form == FormFactor.FoldCoverHalfOpened") "RefineWideCover leaves FoldCoverHalfOpened unchanged"
Assert-True ($device -match "form == FormFactor.FoldHalfOpened") "RefineWideCover leaves FoldHalfOpened unchanged"
Assert-True ($device -match "form == FormFactor.FlipHalfOpened") "RefineWideCover leaves FlipHalfOpened unchanged"
Assert-True ($device -notmatch "IsPhoneLike") "no unused IsPhoneLike helper (the Canvas Scaler picks Phone / Pad / Wide Screen refs)"
Assert-Match $device 'MapFromFoldable[\s\S]{0,400}FoldablePostureSource\.FindActiveLayoutState' "MapFromFoldable uses FoldablePostureSource"
Assert-True ($device -match "FoldablePostureSource\.IsRuntimeSupported") "ResponsiveDevice gates fold via FoldablePostureSource"

$cameraLayout = Get-Content (Join-Path $runtime "ResponsiveCameraLayout.cs") -Raw
Assert-True ($cameraLayout -match "foldPhoneCoverPortrait") "Fold Cover camera slots exist"
Assert-True ($cameraLayout -match "flipPhoneCoverPortrait") "Flip Cover camera slots exist"
Assert-True ($cameraLayout -match "foldCoverHalfOpenedPortrait") "Fold Cover Half Opened camera slots exist"
Assert-True ($cameraLayout -notmatch "flipClosedPortrait") "Flip Cover YAML field is flipPhoneCoverPortrait, not flipClosedPortrait"

$scrollLayout = Get-Content (Join-Path $runtime "ResponsiveScrollLayout.cs") -Raw
Assert-True ($scrollLayout -match "foldPhoneCoverPortrait") "Fold Cover scroll slots exist"
Assert-True ($scrollLayout -match "flipPhoneCoverPortrait") "Flip Cover scroll slots exist"
Assert-True ($scrollLayout -match "foldCoverHalfOpenedPortrait") "Fold Cover Half Opened scroll slots exist"
Assert-True ($scrollLayout -notmatch "flipClosedPortrait") "Flip Cover YAML field is flipPhoneCoverPortrait, not flipClosedPortrait"
Assert-True ($scrollLayout -match "Apply") "scroll layout Apply exists"
Assert-True ($scrollLayout -match "ApplyChanged") "scroll layout ApplyChanged skips Relayout when unchanged"
Assert-True ($scrollLayout -match "SeedAll") "scroll layout SeedAll seeds every slot from host metrics"
Assert-True ($scrollLayout -match "enablePortrait") "scroll SeedAll can disable disallowed orientations"
Assert-Match $scrollLayout 'ResponsiveDevice\.SlotAllowedByPlayerAxes\([\s\S]{0,300}SetSlot\(i, enable \? allowed : disabled\)' "scroll seed follows the shared slot-axis rule (Duo Seated always allowed)"
Assert-Match $scrollLayout '!ResponsiveWidgetLayout\.IsSeedDisabledSlot\(i\) &&' "scroll Flip Cover Landscape (IsSeedDisabledSlot) starts disabled"
Assert-True ($scrollLayout -match "SeedIfEmpty") "scroll layout SeedIfEmpty fills blank YAML"
Assert-True ($scrollLayout -match "ResponsiveWidgetSlotResolver") "scroll layout uses shared slot fallback resolver"

$scrollDriver = Get-Content (Join-Path $runtime "ResponsiveScrollDriver.cs") -Raw
Assert-True ($scrollDriver -match "SimulatorLayoutChanged") "scroll driver listens for Simulator pin"
Assert-True ($scrollDriver -match "LayoutChanged") "scroll driver listens for fold LayoutChanged"
Assert-True ($scrollDriver -match "FoldablePostureSource\.ActiveInstanceChanged") "scroll driver reacts to posture source lifecycle"
Assert-True ($scrollDriver -match "FoldablePostureSource\.LayoutChanged") "scroll driver listens via FoldablePostureSource"
Assert-True ($scrollDriver -notmatch "FoldableModeManager") "scroll driver has no FoldableModeManager binds"
Assert-True ($scrollDriver -match "QueuePlayModePostLoad") "scroll driver queues post-load from Bind (ExecuteAlways Start may skip)"
Assert-True ($scrollDriver -match "GetWatchScreenSize") "scroll driver watches simulated device pixels"

$scrollState = Get-Content (Join-Path $runtime "ResponsiveScrollState.cs") -Raw
Assert-True ($scrollState -match "cellInterval") "scroll state has Cell Interval"
Assert-True ($scrollState -match "scrollOffset") "scroll state has Scroll Offset"
Assert-Match $scrollState 'value <= 0f' "unset scroll Cell Interval applies as 0.2 (C# 9, no struct field initializers)"
Assert-True ($scrollState -match "DefaultCellInterval") "scroll Cell Interval default is 0.2"
Assert-True ($scrollState -match "DefaultScrollOffset") "scroll Scroll Offset default is 0.5"
Assert-True ($scrollState -match "CreateDefault\(bool enabled = true\)") "new scroll slots start Enabled"
Assert-True ($scrollState -notmatch "public bool enabled =") "scroll Enabled has no true field default (missing YAML stays Disabled)"

$rectApi = Get-Content (Join-Path $runtime "ResponsiveRectState.cs") -Raw
Assert-True ($rectApi -match "ComputeCanvasRatio") "ComputeCanvasRatio exists"
Assert-True ($rectApi -match "Auto = 5") "Design Scale Mode includes Auto"
Assert-True ($rectApi -match "ResolveAutoScaleMode") "Auto Scale Mode compares live aspect to Design Canvas"

$designScaleTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveDesignScaleTests.cs") -Raw
Assert-True ($designScaleTests -match "CreateDefault_UsesAutoScaleModeAndUnsetDesignCanvas") "Unity tests new widgets default Auto and unset Design Canvas"
Assert-True ($designScaleTests -match "ApplyScaler_IphoneDuo_PadReferenceFollowsScreenShape") "Unity tests hub scaler refs follow the slot's screen shape"
Assert-True ($designScaleTests -match "ResolveAutoScaleMode_WiderThanDesign_PicksHeight") "Unity tests Auto Scale Mode wider → Height"
Assert-True ($designScaleTests -match "ResolveAutoScaleMode_TallerThanDesign_PicksWidth") "Unity tests Auto Scale Mode taller → Width"
Assert-True ($designScaleTests -match "FormatAutoScaleHint_TallerThanPhone_SaysWidth") "Unity tests Auto Inspector hint taller → Width"
Assert-True ($designScaleTests -match "FormatAutoScaleHint_WiderThanPhone_SaysHeight") "Unity tests Auto Inspector hint wider → Height"
Assert-True ($designScaleTests -match "FormatAutoScaleHint_PortraitWatchOnLandscapeSlot_MatchesOrientedDesign") "Unity tests Auto hint orients watch to the slot"
Assert-True ($designScaleTests -match "ResolveAutoScaleMode_SameLive_PhoneVsPad_CanDiffer") "Unity tests Auto Scale Mode per form Design Canvas"
Assert-True ($designScaleTests -match "ToggleDesignScaleOn_CoverReferenceLandscape_KeepsAuthoredPixels") "Unity tests Cover Landscape design canvas is identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_CoverReferenceWatch_IgnoresExpandedScalerCanvas") "Unity tests Cover watch identity ignores SWSS Expand canvas"
Assert-True ($designScaleTests -match "GetLayoutRatio_CoverSafeAreaWatch_IsIdentity") "Unity tests Fold8 Cover safe-area watch is Cover identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_CoverWatch_PhoneSlot_IsNotIdentity") "Unity tests Cover watch does not identity a Phone slot"
Assert-True ($designScaleTests -match "GetLayoutRatio_CoverWatch_NestedPane_UsesPaneNotIdentity") "Unity tests nested Cover pane is not device identity"
Assert-True ($designScaleTests -match "ResidualRatio_IdentityDesired_IgnoresAncestorAuthoredScale") "Unity tests identity ratio does not divide parent Default Scale"
Assert-True ($designScaleTests -match "GetLayoutRatio_CoverIdentity_NestedWidget_KeepsAuthoredDefaultScale") "Unity tests Cover nested widgets keep Default Scale 1:1"
Assert-True ($designScaleTests -match "GetLayoutRatio_PhoneReferenceWatch_IsIdentity") "Unity tests Phone default profile is identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_CompactPhoneWatch_PhoneSlot_IsNotIdentity") "Unity tests selected non-reference Phone scales off identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_PadReferenceWatch_IsIdentity") "Unity tests Pad default profile is identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FoldHalfOpenedReferenceWatch_IsIdentity") "Unity tests Fold HalfOpened default profile is identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FlipHalfOpenedReferenceWatch_IsIdentity") "Unity tests Flip HalfOpened default profile is identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FoldExpandInnerWatch_PadSlot_IsIdentity") "Unity tests Fold Expand inner watch is Pad-slot identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FlipClosedCoverWatch_FlipPhoneCoverSlot_IsIdentity") "Unity tests Flip Closed cover watch is Flip Cover identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FlipCoverWatch_PhoneSlot_IsNotIdentity") "Unity tests Flip cover watch does not identity a Phone slot"
Assert-True ($designScaleTests -match "IsDesignCanvasReferenceWatch_CustomFlipCoverProvider_IsIdentity") "Unity tests custom Flip reference cover identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FlipOpenedMainWatch_PhoneSlot_IsIdentity") "Unity tests Flip Opened main watch is Phone-slot identity"
Assert-True ($designScaleTests -match "GetLayoutRatio_FoldInnerWatch_PhoneSlot_IsNotIdentity") "Unity tests Fold inner watch does not identity a Phone slot"
Assert-True ($designScaleTests -match "GetLayoutRatio_FoldExpandIdentity_NestedWidget_KeepsAuthoredDefaultScale") "Unity tests Fold Expand nested widgets keep Default Scale 1:1"
Assert-True ($designScaleTests -match "SlotFormFactor_MapsTwentySlotsInEnumOrder") "Unity tests map twenty widget slots in enum order"
Assert-True ($designScaleTests -match "ComputeDisplaySize_HeightOnly_MatchesCoverDisplaySize") "Unity tests HeightOnly ComputeDisplaySize matches Cover"
Assert-True ($designScaleTests -match "ComputeDisplaySize_FitWidth_MatchesParentWidth") "Unity tests FitWidth ComputeDisplaySize"
Assert-True ($designScaleTests -match "ComputeDisplaySize_FitHeight_MatchesParentHeight") "Unity tests FitHeight ComputeDisplaySize"
Assert-True ($designScaleTests -match "ComputeDisplaySize_Manual_ReturnsZero") "Unity tests Manual ComputeDisplaySize"

# Docs. FEATURES.md was split into CONCEPTS / COMPONENTS / SETTINGS and IPHONE-DUO-SIMULATOR.md
# folded into DEVICE-TESTING.md; each claim is asserted against the doc that now carries it.
$concepts = Get-Content (Join-Path $pkg "Docs\CONCEPTS.md") -Raw
$components = Get-Content (Join-Path $pkg "Docs\COMPONENTS.md") -Raw
$settingsDoc = Get-Content (Join-Path $pkg "Docs\SETTINGS.md") -Raw
$gettingStarted = Get-Content (Join-Path $pkg "Docs\GETTING-STARTED.md") -Raw
$structure = Get-Content (Join-Path $pkg "Docs\STRUCTURE.md") -Raw
$platforms = Get-Content (Join-Path $pkg "Docs\PLATFORMS.md") -Raw
$production = Get-Content (Join-Path $pkg "Docs\PRODUCTION.md") -Raw
$readme = Get-Content (Join-Path $pkg "README.md") -Raw
$iphoneDuoReadme = Get-Content (Join-Path $pkg "IphoneDuo\README.md") -Raw
$deviceTestingDoc = Get-Content (Join-Path $pkg "Docs\DEVICE-TESTING.md") -Raw
$detectionDoc = Get-Content (Join-Path $pkg "FoldAndFlip\Docs\DETECTION.md") -Raw
$foldAndFlipReadme = Get-Content (Join-Path $pkg "FoldAndFlip\README.md") -Raw

# Design Scale identity, slots and visibility (was the FEATURES identity / limits tables).
Assert-True ($concepts -match "Identity holds[\s\S]{0,40}matches the slot's design canvas") "CONCEPTS documents design canvas identity"
Assert-True ($platforms -match "IsDesignCanvasReferenceWatch") "PLATFORMS documents product-state identity helper"
Assert-True ($platforms -match "IsKnownFoldCoverPhoneSize") "PLATFORMS documents Fold Cover safe-area identity"
Assert-True ($concepts -match "a fold opened flat uses the Pad slot") "CONCEPTS documents Fold Opened flat identity on Pad slots"
Assert-True ($platforms -match "Closed / Normal still uses the cover size heuristics") "PLATFORMS documents Closed identity through the cover size helpers"
Assert-True ($concepts -match '`FoldPhoneCover` \| \*\*Fold Cover\*\*') "CONCEPTS form table names the Fold Cover slots"
Assert-True ($detectionDoc -match "Fold Cover HalfOpened") "DETECTION names the Fold Cover HalfOpened form"
Assert-True ($concepts -match '`FlipPhoneCover` \| \*\*Flip Cover\*\*') "CONCEPTS form table names the Flip Cover slots"
Assert-True ($concepts -match "a flip opened flat uses[\s\S]{0,5}the Phone slot") "CONCEPTS documents Flip Opened flat identity on Phone slots"
Assert-True ($components -match "ResponsiveTextBanner") "COMPONENTS documents text banner identity via widget slots"
Assert-True ($concepts -match "divides out the ancestor scale so the canvas ratio is not squared") "CONCEPTS documents identity residual for nested list rows"
Assert-True ($concepts -match "\*\*Cover / Fold / Flip slot Disabled\*\*") "CONCEPTS documents Phone-fallback is not Cover identity"
Assert-True ($concepts -match "\*\*constrained nested Canvas pane\*\*") "CONCEPTS documents nested pane is not device identity"
Assert-True ($settingsDoc -match "keeps its own authored refs") "SETTINGS documents hub scaler refs stay authored"
Assert-True ($concepts -match 'New and Reset slots start on `Auto`') "CONCEPTS documents Auto as the default Scale Mode"
Assert-True ($concepts -match "Same fit as UniformMin") "CONCEPTS Auto fits like UniformMin"
Assert-True ($components -match "Does not hide the widget Target") "COMPONENTS documents widget Enabled does not SetActive the Target"
Assert-True ($components -match "Does not hide the camera GameObject") "COMPONENTS documents cameras do not hide on Disabled"
Assert-True ($components -match "Show / hide GameObjects per exact slot") "COMPONENTS names Active Objects as the per-slot visibility owner"
Assert-True ($components -match "Uncheck Enabled on a Cover") "COMPONENTS documents unchecking widget Enabled falls back to Phone"
Assert-True ($components -match "Toggling Enabled on any slot in the live fallback chain previews the resolved layout") "COMPONENTS documents Inspector Enabled on the live fallback chain previews fallback layout"
Assert-True ($components -match "visibility stays on") "COMPONENTS documents two-side visibility stays on SetVisible"
Assert-True ($concepts -match "therefore keeps the \*Phone\* design") "CONCEPTS documents a Disabled Cover slot reusing the Phone design canvas"
Assert-True ($components -match "do \*\*not\*\* list a widget Target in Active Objects") "COMPONENTS documents widget Targets stay off the Active Objects list"
Assert-True ($components -notmatch "ApplyTargetActive") "COMPONENTS no longer names ApplyTargetActive"
Assert-True ($concepts -match "\*\*draws the half-open guide line\*\* on inner chrome only") "CONCEPTS half-open guide draws on inner chrome only"
Assert-True ($concepts -match "removes it on phone, pad, cover and[\s\S]{0,20}flat profiles") "CONCEPTS half-open guide is removed on phone / pad / cover / flat profiles"
Assert-True ($production -match "persistAcrossScenes") "PRODUCTION documents FoldablePostureDetector persistAcrossScenes"
Assert-True ($foldAndFlipReadme -match "FindActiveInstance recovers the persistent instance|recovers the persistent instance after a cached miss") "FoldAndFlip README documents FindActiveInstance persistent recovery"
Assert-True ($detectionDoc -match "Boot through the scene[\s\S]{0,5}that hosts the detector") "DETECTION requires booting through the detector scene for Android HalfOpened"
Assert-True ($platforms -match "DontDestroyOnLoad owner, so later scenes receive") "PLATFORMS documents the persistent DontDestroyOnLoad detector binding"
Assert-True ($gettingStarted -match "FoldablePostureDetector") "GETTING-STARTED documents FoldablePostureDetector"
Assert-True ($structure -match "Put it on the first scene that loads") "STRUCTURE says which scene hosts FoldablePostureDetector"

Assert-True ($structure -match "## Integrating into a project") "STRUCTURE has the Integrating into a project section"
Assert-True ($structure -match "persistAcrossScenes") "STRUCTURE documents persistAcrossScenes"
Assert-True ($structure -match "FoldablePostureDetector") "STRUCTURE integration documents FoldablePostureDetector"
Assert-True ($structure -match "Host a persistent posture detector") "STRUCTURE documents hosting a persistent posture detector"

Assert-True ($platforms -match "FlipPhoneCover") "PLATFORMS maps Flip Closed to FlipPhoneCover"
Assert-True ($platforms -notmatch "Opened flat and Closed cover") "PLATFORMS does not map Flip Closed onto Phone"
Assert-True ($platforms -notmatch "ApplyTargetActive") "PLATFORMS does not document controller widgets hide instead of Phone fallback"
Assert-True ($platforms -match "Flip Cover Landscape starts Disabled") "PLATFORMS documents Flip Cover Landscape starts Disabled"
Assert-True ($platforms -match 'with `persistAcrossScenes` one detector survives') "PLATFORMS documents a persistent detector carrying posture across scenes"
Assert-True ($platforms -match "FoldablePostureDetector") "PLATFORMS documents FoldablePostureDetector for HalfOpened"

Assert-True ($production -match "Confirm one scene in the boot path hosts a persistent") "PRODUCTION checklist confirms a persistent fold posture owner in the boot path"
Assert-True ($production -match "FoldablePostureDetector") "PRODUCTION checklist names FoldablePostureDetector"
Assert-True ($production -match "Confirm Fold Cover HalfOpened is \*\*Enabled\*\* in every scene") "PRODUCTION checklist confirms Fold Cover HalfOpened is Enabled where it is used"

Assert-True ($readme -match "FoldablePostureDetector") "README names FoldablePostureDetector"

# Device pose settings: Android Fold / Flip pages, runtime rules and hold, shared pose card.
foreach ($f in @("FoldAndFlip\Runtime\FoldableHalfOpenRules.cs", "FoldAndFlip\Runtime\FoldableSettings.cs",
        "FoldAndFlip\Runtime\FoldableOrientationHold.cs", "SharedEditor\ResponsivePoseCard.cs",
        "Editor\Package\AndroidFoldSettingsProvider.cs", "Editor\Package\AndroidFlipSettingsProvider.cs")) {
    Assert-True (Test-Path (Join-Path $pkg $f)) "pose settings file $f"
}
$androidFoldPage = Get-Content (Join-Path $pkg "Editor\Package\AndroidFoldSettingsProvider.cs") -Raw
$androidFlipPage = Get-Content (Join-Path $pkg "Editor\Package\AndroidFlipSettingsProvider.cs") -Raw
Assert-True ($androidFoldPage -match "Project/Responsive UI/Android Fold") "Android Fold Project Settings page path"
Assert-True ($androidFlipPage -match "Project/Responsive UI/Android Flip") "Android Flip Project Settings page path"
Assert-True ($platforms -match "Project/Responsive UI/Android Fold") "PLATFORMS documents the Android Fold page"
Assert-True ($platforms -match "Project/Responsive UI/Android Flip|\*\*Android Flip\*\*") "PLATFORMS documents the Android Flip page"
Assert-True ($platforms -match "FoldableOrientationHold") "PLATFORMS documents the Android runtime orientation hold"
Assert-True ($platforms -match "FDPIphoneDuoFlatRotation") "PLATFORMS documents the Flat rotation plist key"
Assert-True ($platforms -notmatch "both-axes projects are not locked at runtime") "PLATFORMS has no stale both-axes known gap"
Assert-True ($platforms -notmatch "with both axes the slot keeps the screen it had") "PLATFORMS has no stale both-axes Fold hold"
Assert-True ($settingsDoc -match "FoldableSettings\.json") "SETTINGS documents FoldableSettings.json"
Assert-True ($settingsDoc -match "Project/Responsive UI/Android Fold") "SETTINGS documents the Android Fold page"
Assert-True ($readme -match "\*\*Android Fold\*\*, \*\*Android Flip\*\*") "README overview names the pose settings pages"
Assert-True ($iphoneDuoReadme -match "FDPIphoneDuoFlatRotation") "IphoneDuo README documents Flat rotation plist key"
Assert-True ($iphoneDuoReadme -match "FDPIphoneDuoFlatLockedPose") "IphoneDuo README documents Flat locked pose plist key"
Assert-True ($deviceTestingDoc -match "FDPIphoneDuoFlatRotation FDPIphoneDuoFlatLockedPose") "DEVICE-TESTING lists the Flat plist keys"
Assert-True ($detectionDoc -match "FoldableOrientationHold") "DETECTION documents the Android runtime orientation hold"
Assert-True ($detectionDoc -notmatch "both axes keep the screen it entered with") "DETECTION has no stale both-axes hold"

Assert-True ($rectApi -match "ComputeAppliedLocalScale") "ComputeAppliedLocalScale exists"
Assert-True ($rectApi -match "ComputeAppliedSizeDelta") "stretched widget offsets scale in design space"
Assert-True ($rectApi -match "IsHorizontalStretch") "widget scale distinguishes horizontal stretch anchors"
Assert-True ($rectApi -match "IsVerticalStretch") "widget scale distinguishes vertical stretch anchors"
Assert-True ($rectApi -match "ParentAnchorSpan") "stretched axes compensate the live parent anchor span"
Assert-True ($rectApi -match "NormalizeLayoutRatio") "Apply and capture share one ratio normalization"
Assert-True ($rectApi -match "ResidualRatio") "nested Design Scale widgets remove ancestor localScale"
Assert-True ($rectApi -match "IsIdentityScale\(ratio\)") "identity canvas ratio does not divide ancestor Default Scale"
Assert-True ($rectApi -match "AncestorLocalScale") "residual ratio walks to the containing canvas"
Assert-True ($rectApi -match "span\.x \* \(1f / ratio\.x - 1f\)") "horizontal stretch compensation preserves transformed edge offsets"
Assert-True ($rectApi -match "span\.y \* \(1f / ratio\.y - 1f\)") "vertical stretch compensation preserves transformed edge offsets"
Assert-True ($rectApi -match "CopyDesignScaleSettingsFrom") "CopyDesignScaleSettingsFrom exists"
Assert-Match $rectApi 'CopyDesignScaleSettingsFrom[\s\S]{0,400}designCanvasSize = Vector2.zero' "CopyDesignScaleSettingsFrom keeps Design Canvas Size unset so slots follow catalog"
Assert-True ($rectApi -notmatch "designCanvasSize = source.designCanvasSize") "CopyDesignScaleSettingsFrom does not copy authored Design Canvas Size"
Assert-True ($rectApi -notmatch "designCanvasSize = source.GetResolvedDesignCanvasSize") "CopyDesignScaleSettingsFrom does not bake catalog pixels"
Assert-True ($rectApi -notmatch "ResolveCaptureSlot") "former ResolveCaptureSlot name removed"
Assert-NotMatch $rectApi 'CopyDesignScaleFrom\(' "removed CopyDesignScaleFrom stays absent"

$ui = Get-Content (Join-Path $runtime "ResponsiveUI.cs") -Raw
Assert-True ($ui -match "enabled = false") "ResponsiveUI Reset starts the component disabled"
Assert-True ($ui -notmatch "controllerMigrated|MigrateLegacyListsToController") "removed hub list migration stays absent"
Assert-True ($ui -match "IResponsiveUiClient") "ResponsiveUI notifies IResponsiveUiClient subscribers"
Assert-True ($ui -match "RegisterClient") "hub registers layout clients"
Assert-True ($ui -match "NotifyClients") "hub Apply notifies clients"
Assert-True ($ui -match "DrainEarlyClients") "hub drains early clients in settled batches"
Assert-True ($ui -match "SettleControllerGeometry") "hub settles controller geometry before late clients"
Assert-Match $ui 'void ApplyNotifyPhase[\s\S]{0,200}int end' "early notify batches freeze end so mid-Apply registers wait for settle"
Assert-Match $ui 'void SettleControllerGeometry[\s\S]{0,500}ForceUpdateCanvases\(\);[\s\S]{0,500}ApplyControllerBackgrounds\(' "settle cover-fits backgrounds before late clients on every geometry pass"
Assert-True ($ui -notmatch 'SettleControllerGeometry[\s\S]{0,400}if\s*\(\s*!layoutOnly\s*\)') "settle no longer skips backgrounds on layout-only passes"
Assert-True ($ui -match "notifyAllLayoutClients") "hub can notify every client on the Simulator canvas-ready pass"
Assert-Match $ui 'layoutOnly &&[\s\S]{0,80}!Application\.isPlaying[\s\S]{0,80}IsAuthoringSelectedTarget' "layout-only Apply skips the client whose Target is selected"
Assert-True ($ui -notmatch "EditorMigrateLegacyListsNow") "removed hub list migration entry point stays absent"
Assert-True ($ui -match "GetEditorRectTransform") "shared editor RectTransform resolve exists"
Assert-True ($ui -notmatch "TryGetWidget") "removed TryGetWidget stays absent"
Assert-True ($ui -notmatch "(?i)hingeGuide") "hinge guide style lives in Project Settings only (no per-hub fields)"
Assert-True ($ui -match "overrideOriginalBgSize") "ResponsiveUI can override Project Settings background size"
Assert-True ($ui -match "ResponsiveFormTransitionLoading formTransitionLoading") "hub owns form-transition loading settings"
Assert-True ($ui -match "if \(!loading\.Enabled\)") "hub skips form-transition Begin when feature off"
Assert-True ($ui -match "KeepFormTransitionLoadingVisible") "hub re-asserts form-transition loading after Active Objects"
Assert-True ($ui -match "EndFormTransitionLoading") "hub hides form-transition loading after Apply"
Assert-True ($ui -notmatch "BeginFormTransitionOverlay|ShouldShowFormTransitionOverlay|formTransitionLoadingUi|formTransitionLoadingEnabled|formTransitionMinVisibleSeconds") "former flat form-transition fields stay absent"
Assert-True ($ui -match "OriginalBgSizeProvider") "ResponsiveUI binds Project Settings background size"
Assert-True ($ui -match "GetOriginalBgSize") "cover Apply uses resolved original bg size"

$formTransition = Get-Content (Join-Path $runtime "ResponsiveFormTransitionLoading.cs") -Raw
Assert-True ($formTransition -match "class ResponsiveFormTransitionLoading") "form-transition loading is its own type"
Assert-True ($formTransition -match "bool enabled") "form-transition loading has enable flag"
Assert-True ($formTransition -match "Image blackOverlay") "form-transition loading has black overlay Image"
Assert-True ($formTransition -match "ResponsiveUiController controller") "form-transition loading has controller"
Assert-True ($formTransition -match "minVisibleSeconds") "form-transition loading has min visible time"
Assert-True ($formTransition -match "static bool ShouldShow") "form-transition loading gate is testable"
Assert-True ($formTransition -match "static bool IsFormChange") "form-transition form change is testable"
Assert-True ($formTransition -match "static bool ShouldCoverFormChange") "form-transition cover gate is testable"
Assert-True ($formTransition -match "ResponsiveActiveObjectSlots showOnSlots") "form-transition has per-slot Show On flags"
Assert-Match $formTransition 'ResponsiveActiveObjectSlots showOnSlots =\s*ResponsiveActiveObjectSlots\.FormTransitionDefaults\(\);' "form-transition Show On defaults come from the field initializer"
Assert-True ($formTransition -notmatch "showOnSlotsInitialized|EnsureShowOnSlotsDefaults") "removed Show On init-flag shim stays absent"
Assert-True ($formTransition -match "DismissInFlightCoverIfAny") "form-transition dismisses leftover cover when destination slot off"
Assert-True ($formTransition -match "hasBlackOverlay \|\| hasController") "form-transition gate accepts black overlay or controller"
Assert-True ($formTransition -match "public bool Begin\(") "form-transition loading Begin show path"
Assert-True ($formTransition -match "public void KeepVisible\(") "form-transition loading KeepVisible path"
Assert-True ($formTransition -match "public void End\(") "form-transition loading End hide path"
Assert-True ($formTransition -match "SetBlackOverlayVisible") "form-transition drives black overlay CanvasGroup"
Assert-True ($formTransition -match "SetControllerVisible") "form-transition drives loading controller CanvasGroup"
Assert-True ($formTransition -match "CanvasGroup") "form-transition loading uses CanvasGroup show/hide"
Assert-True ($formTransition -match "ignoreParentGroups") "form-transition CanvasGroup ignores parent fades"
Assert-True ($formTransition -match "BringToFront") "form-transition skips SetAsLastSibling when already front"
Assert-True ($formTransition -match "ApplyCanvasGroup") "form-transition skips no-op CanvasGroup writes"
Assert-True ($formTransition -match "_visible = true") "form-transition marks visible before Prepare to avoid alpha thrash"
Assert-True ($formTransition -match "EnsureReady") "form-transition loading can stay registered while invisible"
Assert-True ($formTransition -match "Application\.isPlaying") "form-transition EnsureReady / hide respect Play Mode for authoring"
Assert-True ($formTransition -match "SetAsLastSibling") "form-transition loading draws on top"
Assert-Match $formTransition 'public bool Begin\([\s\S]{0,400}if \(!ShouldShow\(' "form-transition Begin uses the tested ShouldShow gate"
Assert-Match $formTransition 'Application\.isPlaying &&\s*!enabled &&\s*controller != null' "form-transition only deactivates the controller in Play Mode"
Assert-True ($formTransition -match "Allocation-free wait") "form-transition hide wait avoids WaitForSecondsRealtime alloc"
Assert-True ($formTransition -notmatch "WaitForSecondsRealtime") "form-transition hide does not allocate WaitForSecondsRealtime"
Assert-True ($formTransition -match "if \(!_blackCanvasGroup\.ignoreParentGroups\)") "form-transition skips redundant ignoreParentGroups writes"

$bgCover = Get-Content (Join-Path $runtime "ResponsiveBackgroundCover.cs") -Raw
Assert-True ($bgCover -match "static class ResponsiveBackgroundCover") "background Match Mode math lives in ResponsiveBackgroundCover"
Assert-True ($bgCover -match "ResolveArtSize") "background Match Mode prefers Image sprite pixels"
Assert-True ($bgCover -match "anchorSpanX") "HeightOnly clears stretch-width leftover Fit letterbox"
Assert-True ($bgCover -match "ApplyBackground") "cover Apply dispatches Match Mode"
Assert-True ($bgCover -match "ComputeDisplaySize") "background Match Mode has shared display-size math"
Assert-Match $bgCover 'default:\s*return artSize \* CoverScale\(artSize, parentSize\);' "background Match Mode Cover keeps art ratio and fills"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.HeightOnly") "background Match Mode HeightOnly covers height"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.Fit") "background Match Mode Fit keeps art ratio and contains"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.Stretch") "background Match Mode Stretch ignores art ratio"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.WidthOnly") "background Match Mode WidthOnly covers width"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.FitWidth") "background Match Mode FitWidth matches parent width"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.FitHeight") "background Match Mode FitHeight matches parent height"
Assert-True ($bgCover -match "ResponsiveBgCoverMode\.Manual") "background Match Mode Manual leaves sizeDelta"
Assert-True ($bgCover -match "ApplyCoverWidth") "WidthOnly uses ApplyCoverWidth"
Assert-Match $bgCover 'mode == ResponsiveBgCoverMode\.Manual[\s\S]{0,40}return' "Manual Match Mode skips Apply resize"
Assert-True ($bgCover -notmatch "formerly") "background cover enum docs carry no legacy rename note"
Assert-True ($ui -match "override void ApplyLayout") "hub ApplyLayout cover-fits controller backgrounds"
Assert-True ($ui -match "controller.ApplyBackgrounds") "hub cover Apply uses controller backgrounds"
Assert-True ($ui -match "EditorBakeProjectBackgroundSize") "Editor bakes Project Settings size for player builds"
Assert-True ($ui -match "protected override void OnValidate") "ResponsiveUI OnValidate calls handler Apply queue"
Assert-True ($ui -match "GetDesignScaleCurrentSize") "widget Design Scale uses a dedicated live-size helper"
Assert-True ($ui -match "GetWatchSize") "Design Scale live size goes through GetWatchSize"
Assert-True ($ui -match "GetWidgetDesignSpaceSize") "SWSS Design Scale resolves one design-space size"
Assert-True ($ui -match "IsDesignCanvasReferenceIdentity") "slot Design Canvas device is identity even under SWSS Expand"
$designScale = Get-Content (Join-Path $runtime "ResponsiveDesignScale.cs") -Raw
Assert-True ($designScale -match "static class ResponsiveDesignScale") "Design Scale identity lives in ResponsiveDesignScale"
Assert-True ($ui -match "ResponsiveDesignScale\.IsDesignCanvasReferenceWatch") "every default profile / product state shares one identity watch helper"
Assert-True ($designScale -match "IsKnownFoldCoverPhoneSize") "Cover identity includes Fold8 Cover safe-area WxH"
Assert-True ($designScale -match "GetDefaultDesignCanvasSize\(\s*ResponsiveDevice.FormFactor.FoldHalfOpened") "Fold Expand Pad slots identity against Fold inner Design Canvas"
Assert-True ($designScale -match "GetDefaultDesignCanvasSize\(\s*ResponsiveDevice.FormFactor.FlipHalfOpened") "Flip Opened Phone slots identity against Flip main Design Canvas"
Assert-True ($designScale -match "GetDefaultFlipCoverSize") "Flip Closed Flip Cover identity follows the Flip reference profile cover"
Assert-True ($designScale -match "FormFactor.FlipPhoneCover") "Flip Closed cover watch identities Flip Cover slots"
Assert-True ($designScale -match "ResolveIdentityLiveSize") "widgets and cameras share one identity live-size helper"
Assert-True ($rectApi -match "MapScalerCanvasToDesignSpace") "SWSS canvas units map through the scaler reference"
Assert-True ($rectApi -match "ApproximatelyMatchesSize") "scaler and Design Canvas identity share a size match helper"
Assert-Match $ui 'OrientSize\(canvasSize, screenOrientation\)' "SWSS canvas rect follows the active slot's screen axis"
Assert-True ($ui -notmatch "ResponsiveDevice\.ScreenOrientation\(") "hub orients design sizes by the slot orientation (labels name the screen shape)"
Assert-True ($ui -notmatch "TryGetTrustedWatchScreenSize") "SWSS widget scaling does not mix raw screen pixels"
Assert-True ($ui -match "ResidualRatio") "hub layout ratio removes ancestor localScale"
Assert-True ($designScale -match "!targetCanvas\.isRootCanvas") "Design Scale can use a constrained nested Canvas pane"

Assert-Match $widgetLayout 'state\.Apply\(target, currentCanvasSize, sourceForm, sourceOrientation\)' "fallback widgets retain the source slot's design coordinate system"

$hingeGuide = Get-Content (Join-Path $runtime "ResponsiveUiHingeGuide.cs") -Raw
Assert-True ($hingeGuide -match "ComputeFromLayout") "hinge guide computes from fold bounds"
Assert-True ($hingeGuide -match "FoldablePostureSource\.FindActiveLayoutState") "hinge guide reads FoldablePostureSource layout"
Assert-True ($hingeGuide -match "CanDrawFromLayout") "hinge guide gates Cover / Expand / non-separating layouts in one helper"
Assert-True ($hingeGuide -match "isCoverScreen") "hinge guide rejects Cover / Normal layouts"
Assert-True ($hingeGuide -match "!layout.isSeparating") "hinge guide requires a separating hinge"
Assert-True ($hingeGuide -match "productState != FoldableProductState.HalfOpened") "hinge guide requires HalfOpened product"
Assert-True ($hingeGuide -match "state != FoldableState.HalfOpened") "hinge guide requires HalfOpened fold state"
Assert-True ($hingeGuide -match "ToGuiRect") "hinge guide converts to Game View GUI rect"
Assert-True ($hingeGuide -match "DefaultGuideColor") "hinge guide exposes default color"
Assert-True ($hingeGuide -match "DefaultThicknessPx") "hinge guide exposes default thickness"
Assert-True ($hingeGuide -match "0\.25f, 0\.25f, 0\.25f, 0\.4f") "hinge guide default color is dark gray"
Assert-True ($hingeGuide -match "DefaultThicknessPx = 5f") "hinge guide default thickness is 5px"
Assert-True ($hingeGuide -match "thicknessPixels") "hinge guide ToGuiRect accepts pixel thickness"

$hingeOverlay = Get-Content (EditorPath "ResponsiveUiHingeGuideOverlay.cs") -Raw
Assert-True ($hingeOverlay -match "RemoveFromHierarchy") "hinge overlay hard-removes on Normal/Expand"
Assert-True ($hingeOverlay -match "IsFoldableDeviceBound") "hinge overlay requires a bound Fold/Flip session"
Assert-True ($hingeOverlay -match "BuildLayout|FoldableEditorSession") "hinge overlay reads Fold session layout"
Assert-True ($hingeOverlay -match "generateVisualContent|painter2D") "hinge overlay draws with UI Toolkit, not Simulator IMGUI"
Assert-True ($hingeOverlay -notmatch "IMGUIContainer") "hinge overlay does not host IMGUI on the Simulator window"
Assert-True ($hingeOverlay -notmatch "EditorGUI\.DrawRect") "hinge overlay does not EditorGUI.DrawRect on the Simulator"
Assert-True ($hingeOverlay -match "ResolveLineStyle|HingeGuideColor|HingeGuideThicknessPx") "hinge overlay reads developer color and thickness"
Assert-True ($hingeOverlay -match "ResponsiveUiEditorSettings") "hinge overlay style comes from Project Settings"
Assert-True ($hingeOverlay -match "FindUniqueOverlay") "hinge overlay keeps a single Simulator IMGUI host"
Assert-True ($hingeOverlay -match "name: OverlayElementName") "hinge overlay queries the named host"
Assert-True ($hingeOverlay -notmatch "FindObjectsByType<ResponsiveUI>") "hinge overlay does not draw per ResponsiveUI"
Assert-True ($hingeOverlay -notmatch "DrawIntoCameraTarget|onPostRender|endCameraRendering") "hinge overlay avoids camera-RT flash path"
Assert-True ($hingeOverlay -notmatch "SceneView") "hinge overlay does not draw in Scene view"
Assert-True ($hingeOverlay -notmatch "EditorApplication\.update") "hinge overlay does not poll Simulator windows"
Assert-True ($hingeOverlay -notmatch "window\.Repaint") "hinge overlay never Repaints the Simulator window (Raw Input)"
Assert-True ($hingeOverlay -notmatch "HasVisibleSimulatorWindow") "hinge overlay does not read Simulator window.position"
Assert-True ($hingeOverlay -match "GetSimulatorWindowObjects") "hinge overlay reuses Simulator window lookup"
Assert-True ($hingeOverlay -match "LiveSelectionAllowsHinge") "hinge overlay checks live Simulator selection"
Assert-True ($hingeOverlay -match "IsToolkitListName") "hinge overlay hides on Phone/Pad Simulator skins"
Assert-True ($hingeOverlay -match "CoverOrOuter") "hinge overlay hides on Cover / Normal chrome"
Assert-True ($hingeOverlay -match "IsKnownCoverPhoneSize") "hinge overlay hides on Fold/Flip Cover WxH"
Assert-True ($hingeOverlay -match "SelectionChanged") "hinge overlay re-evaluates on Simulator selection"
Assert-True ($hingeOverlay -match "ScreenSynced") "hinge overlay re-evaluates after Cover/Inner commit"
Assert-True ($hingeOverlay -match "return ResponsiveUiHingeGuide.ComputeFromLayout") "hinge overlay does not fallback-draw without layout bounds"

$hingeTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveUiHingeGuideTests.cs") -Raw
Assert-True ($hingeTests -match "CanDrawFromLayout") "Unity tests Cover / Expand gates go through CanDrawFromLayout"
Assert-True ($hingeTests -match "ComputeFromLayout_Opened_ReturnsFalse") "Unity tests Opened layouts do not produce a hinge line"
Assert-True ($hingeTests -match "ComputeFromLayout_ClosedCover_ReturnsFalse") "Unity tests Cover / Normal layouts do not produce a hinge line"
Assert-True ($hingeTests -match "ComputeFromLayout_HalfOpenedWithoutSeparating_ReturnsFalse") "Unity tests HalfOpened without isSeparating do not produce a hinge line"
Assert-True ($hingeTests -match "LiveSelectionAllowsHinge_PhoneSkin_IsFalse") "Unity tests Phone Simulator skins hide the hinge line"
Assert-True ($hingeTests -match "LiveSelectionAllowsHinge_FoldCover_IsFalse") "Unity tests Fold Cover chrome hides the hinge line"
Assert-True ($hingeTests -match "LiveSelectionAllowsHinge_FoldInner_IsTrue") "Unity tests Fold Inner chrome can show the hinge line"

$uiEditor = Get-Content (EditorPath "ResponsiveUIEditor.cs") -Raw
Assert-True ($uiEditor -match "DrawHingeSection") "ResponsiveUI hub Inspector writes the Project Settings hinge overlay"
Assert-True ($uiEditor -match "ResponsiveUiSettingsProvider\.DrawHingeGuideFields") "hub Inspector exposes the shared hinge line fields"
Assert-True ($uiEditor -notmatch "sizeProp\.vector2Value != projectSize") "hub Inspector does not write the baked background size every repaint"

$camera = Get-Content (Join-Path $runtime "ResponsiveCamera.cs") -Raw
Assert-True ($camera -match "class ResponsiveCamera\s*:\s*MonoBehaviour,\s*IResponsiveUiClient") "ResponsiveCamera subscribes to the Canvas hub"
Assert-True ($camera -match "void Reset") "ResponsiveCamera Reset starts the component disabled"
Assert-True ($camera -match "enabled = false") "ResponsiveCamera Add Component leaves the script checkbox off"
Assert-True ($camera -match "_seedDefaultLayout = true") "ResponsiveCamera seeds one layout only on Add Component / Reset"
Assert-True ($camera -match "ApplyFromHub") "ResponsiveCamera applies camera slots from the hub layout"
Assert-True ($camera -match "EditorApplyCameras") "camera Inspector can preview bulk edits without a full hub Apply"
Assert-True ($camera -notmatch "\.SetActive\(") "camera slot Enabled does not SetActive the Camera GameObject"
Assert-True ($camera -match "EditorUsesFocusBounds") "camera knows which Focus Bounds rects it is watching"
Assert-True ($camera -match "CaptureFocusBoundsGeometry") "camera refreshes Focus Bounds geometry after Apply"
Assert-True ($camera -match "camera\.pixelWidth" -and $camera -match "camera\.pixelHeight") "camera focus watches settled render dimensions"
Assert-True ($camera -match "RefreshEditorFocusRenderGeometry") "camera focus refreshes settled Simulator aspect in Edit Mode"
Assert-True ($camera -match "ResponsiveUiHub.BindClient") "camera uses shared hub bind"
Assert-True ($camera -match "RebindIfChanged") "camera rebinds when Hub is reassigned while enabled"
Assert-True ($camera -match "GetEditorCamera") "ResponsiveCamera editor Camera resolve exists"
Assert-True ($camera -notmatch "EvaluateApplyViewport|ShouldApplyViewport") "no viewport ownership helper (rows with a Target always write Camera.rect)"
Assert-True ($camera -notmatch "deferViewportToFoldableManager") "no obsolete deferViewport field (no old data)"
Assert-True ($camera -notmatch "FoldableModeManager") "ResponsiveCamera has no FoldableModeManager usage"
Assert-True ($camera -match "SizeChangeRequiresReapply") "ResponsiveCamera re-applies on size changes"
Assert-True ($camera -match "IsAuthoringSelectedTarget") "camera authoring guard exists"
Assert-Match $camera 'IsAuthoringSelectedTarget[\s\S]{0,2500}UsesFocusBoundsRect' "selected Focus Bounds HUD skips camera canvas-ready Apply"
Assert-True ($camera -match "applyViewport: true") "ResponsiveCamera always applies viewport when Target is set"
Assert-True ($camera -match "GetUiLayoutSize") "ResponsiveCamera resolves UI size for scale"
Assert-True ($camera -match "ResolveUiLayoutSize") "ResponsiveCamera prefers Simulator watch size when the scaler is missing"
Assert-True ($camera -notmatch "ResolveUiScaleSize") "removed test-only ResolveUiScaleSize overloads stay absent"
Assert-True ($camera -match "ScaleWithScreenSize") "ResponsiveCamera prefers Screen when CanvasScaler Scale With Screen Size"
Assert-True ($camera -match "EditorBeginApply") "camera hub Apply notifies editor sync to suppress slot recapture"
Assert-True ($camera -match "EditorEndApply") "camera hub Apply releases editor capture suppress after delayCall"
Assert-True ($camera -match "EditorIgnoreTargetCapture") "camera hub Apply ignores Target recapture like text banners"
Assert-True ($camera -match "uiSizeRect|uiSizeCanvas") "ResponsiveCamera exposes UI size sources"
Assert-True ($camera -match "ResolveActiveSlot") "ResponsiveCamera size reapply checks the active slot only"
Assert-True ($camera -match "state\.UsesCanvasScale") "ResponsiveCamera size reapply gates on active UI-size scale"
Assert-True ($camera -notmatch "UsesCanvasScalerResolutionFit") "camera Play Mode size ticks still re-apply UI scale under a hub scaler"
Assert-True ($camera -match "!hub\.enabled") "ResponsiveCamera rejects notifications from a disabled hub"
Assert-True ($camera -notmatch "scaleOrthographicSizeMigrated|projectionTypeMigrated|controllerMigrated|Migrate[A-Z]") "removed camera YAML migrations stay absent"
Assert-True ($camera -match "HasBackground") "ResponsiveCamera re-applies its background on size changes"
Assert-True ($camera -match "HasFocus") "ResponsiveCamera re-applies focus on size changes"
Assert-True ($camera -match "EditorCaptureActiveFocusBounds") "ResponsiveCamera samples HUD bounds into the active slot in Edit Mode"
Assert-Match $camera 'if \(!Application\.isPlaying\)[\s\S]{0,100}RefreshEditorFocusRenderGeometry\(\)[\s\S]{0,50}return' "Edit Mode LateUpdate only watches cheap render geometry"
Assert-True ($camera -match "applyBoundsLayout") "camera Apply can skip writing Focus Bounds layout over a Scene HUD edit"
Assert-Match $camera 'applyBoundsLayout,\s*hub,' "camera Apply passes the hub so Focus Bounds uses widget canvas ratio"
Assert-True ($camera -match "CaptureActiveFocusBoundsLayout\(key, uiSize, hub\)") "camera HUD capture passes the hub widget ratio"
Assert-True ($camera -notmatch "SyncApplyLocalTransform") "removed Apply Local YAML sync stays absent"

$cameraLayout = Get-Content (Join-Path $runtime "ResponsiveCameraLayout.cs") -Raw
Assert-True ($cameraLayout -match "ResponsiveWidgetSlotResolver") "camera layout uses shared slot fallback resolver"
Assert-True ($cameraLayout -match "ResolveActiveSlot") "camera ResolveActiveSlot exists"
Assert-True ($cameraLayout -match "ApplyFullViewport") "camera layout falls back to full viewport"
Assert-True ($cameraLayout -match "currentUiSize") "camera layout passes UI size into state Apply"
Assert-True ($cameraLayout -match "out ResponsiveDevice.Orientation sourceOrientation") "camera UI scaling uses the resolved source slot orientation"
Assert-True ($cameraLayout -match "UnscaleDesignPosition") "camera capture unscales design position"
Assert-True ($cameraLayout -notmatch "Migrate[A-Z]|SyncApplyLocalTransform") "removed camera layout YAML migrations stay absent"
Assert-Match $cameraLayout 'SpriteRenderer background' "camera layout has one background SpriteRenderer"
Assert-True ($cameraLayout -match "ResponsiveCameraSpriteFit.ApplyBackground") "camera layout fits its background after Apply"
Assert-True ($cameraLayout -match "ApplyFocus") "camera layout focuses after slot Apply"
Assert-True ($cameraLayout -match "existing.HasFocus\(\)") "camera CaptureFromTarget keeps Focus-owned pose"
Assert-True ($cameraLayout -notmatch "public RectTransform focusBounds|public Transform focusTarget") "camera rows hold no row-level Focus (per slot only)"
Assert-True ($cameraLayout -match "state.focusBounds") "camera Apply uses slot Focus Bounds"
Assert-True ($cameraLayout -match "ApplyFocusBoundsLayout") "camera Apply writes per-slot Focus Bounds widget layout"
Assert-Match $cameraLayout 'ApplyFocusBoundsLayout[\s\S]{0,160}if \(!state\.HasFocus\(\)\)' "Focus Bounds layout Apply does not require Focus Target"
Assert-True ($cameraLayout -match "GetLayoutRatio") "camera Focus Bounds capture unscales with the widget canvas ratio"
Assert-Match $cameraLayout 'CaptureFocusBoundsRatio[\s\S]{0,420}ComputeLayoutRatio\(\s*ResponsiveUI\.OrientSizeForSlot\(\s*currentUiSize,\s*form,\s*orientation\)' "camera Focus Bounds hub-less path uses its live canvas size, oriented to the source slot's screen axis"
Assert-True ($cameraLayout -match "CopyDesignScaleSettingsFrom") "camera HUD capture keeps Design Scale settings like widgets"
Assert-True ($cameraLayout -match "MatchesApplied") "camera HUD capture skips when the live HUD already matches Apply"
Assert-True ($cameraLayout -match "GetAppliedWidgetScale") "camera HUD capture expected scale matches ApplyWidget"
Assert-True ($cameraLayout -notmatch "From\(slot\.focusBounds, form\)") "camera HUD capture always unscales with layout ratio like widgets"
Assert-True ($cameraLayout -match "CaptureActiveFocusBoundsLayout") "camera layout can sample HUD bounds into the active slot"
Assert-True ($cameraLayout -match "UsesFocusBounds") "camera layout knows which Focus Bounds rect it uses"
Assert-True ($cameraLayout -match "UsesFocusBoundsRect") "camera layout Scene capture matches assigned Focus Bounds without Focus Target"

$spriteFit = Get-Content (Join-Path $runtime "ResponsiveCameraSpriteFit.cs") -Raw
Assert-True ($spriteFit -match "GetVisibleWorldSize") "sprite fit uses camera world size"
Assert-True ($spriteFit -match "ResponsiveCameraSpriteFitMode") "sprite fit has Stretch/Cover/Fit"
Assert-True ($spriteFit -match "ViewportToWorldPoint") "sprite fit uses camera viewport world corners"
Assert-True ($spriteFit -match "MeasureProjectedSize") "sprite fit measures projected sprite size"
Assert-True ($spriteFit -match "ApplyDrawMode|MeasureProjectedSizeWithDrawSize") "sprite fit handles Sliced/Tiled draw modes"
Assert-True ($spriteFit -match "AlignToCameraView") "sprite fit centers sprite on camera view after scale"
Assert-True ($spriteFit -match "ApplySimple|SolveLocalScale") "sprite fit solves absolute localScale from unit axes"
Assert-True ($spriteFit -match "AlignToCameraView\(sprite, camera, depth") "sprite fit Apply recenters after scale"
Assert-True ($spriteFit -match "Mathf.Max\(sx, sy\)") "camera background Cover fills the view"
Assert-True ($spriteFit -match "Mathf.Min\(sx, sy\)") "camera background Fit contains inside the view"
Assert-True ($spriteFit -match "PlaceBehindCameraContent") "camera background moves behind visible content"
Assert-True ($spriteFit -match "sortingOrder = short.MinValue") "camera background uses the minimum sprite sorting order"

$cameraFocus = Get-Content (Join-Path $runtime "ResponsiveCameraFocus.cs") -Raw
Assert-True ($cameraFocus -match "enum ResponsiveCameraFocusScreen") "camera focus names Focus Screen"
Assert-True ($cameraFocus -notmatch "Normalize\(") "no legacy pane-screen normalization (no YAML holds 3-6)"
Assert-True ($cameraFocus -notmatch "ResolveAutoScreen") "camera focus Auto does not pick geometric fold panes"
Assert-True ($cameraFocus -match "GetCameraFocusViewport") "camera focus maps a pane into the live Camera.rect"
Assert-True ($cameraFocus -match "ignoreBackground") "camera focus Cover/Fit ignores the fitted background"
Assert-True ($cameraFocus -match "Mathf.Min\(sx, sy\)") "camera focus Cover zooms in to fill the pane"
Assert-True ($cameraFocus -match "GetComponentsInChildren\(true, _colliders2D\)") "camera focus measures 2D colliders when no Renderer is present"
Assert-Match $cameraFocus 'GetTargetWorldBounds\([\s\S]{0,200}UncachedBounds\.GetBounds\(' "one-shot Target bounds reuse the TargetBoundsCache rules"
Assert-Match $cameraFocus 'bool hasBounds = GetTargetWorldBounds\([\s\S]{0,300}ApplyProjectionFit\(camera, targetBounds' "focus Apply scans Target bounds once for fit and centering"
Assert-True ($cameraFocus -match "None = 0") "camera focus Screen starts at None"
Assert-True ($cameraFocus -match "Auto = 1") "camera focus Screen includes Auto"
Assert-True ($cameraFocus -match "Full = 2") "camera focus Screen includes Full"
Assert-True ($cameraFocus -notmatch "Upper = 3") "camera focus has no Upper pane enum"
Assert-True ($cameraFocus -notmatch "Lower = 4") "camera focus has no Lower pane enum"
Assert-True ($cameraFocus -notmatch "Left = 5") "camera focus has no Left pane enum"
Assert-True ($cameraFocus -notmatch "Right = 6") "camera focus has no Right pane enum"
Assert-True ($cameraFocus -match "CenterOnTarget") "camera focus slides the camera onto the object"
Assert-True ($cameraFocus -match "hasBounds \? targetBounds\.center : target\.position") "camera focus Center uses measured bounds, not only the Transform pivot"
Assert-True ($cameraFocus -notmatch "GetTabletopCameraRect") "camera focus does not reuse FoldableLayoutApplier pane rects"
Assert-True ($cameraFocus -notmatch "applyViewport|_ = layout") "camera focus API carries no ignored viewport / layout parameters"
Assert-True ($cameraFocus -match "Bounds = 7") "camera focus Screen keeps Bounds at 7 so legacy YAML stays Bounds"
Assert-True ($cameraFocus -match "GetUiBoundsViewport") "camera focus converts a UI rect to viewport"
Assert-True ($cameraFocus -match "HasBoundsRect") "camera focus treats a stored window as the Focus pane"
Assert-True ($cameraFocus -match "ScreenSpaceOverlay") "camera focus Bounds reads Overlay canvas corners"
Assert-True ($cameraFocus -match "RectTransformUtility.WorldToScreenPoint") "camera focus Overlay uses RectTransformUtility screen corners"
Assert-True ($cameraFocus -match "WorldToScreenPoint\(null") "overlay Focus Bounds keeps a null camera \(world XY is already screen pixels\)"
Assert-True ($cameraFocus -match "Canvas.ForceUpdateCanvases") "camera focus reads HUD Bounds after canvas layout"
Assert-True ($cameraFocus -match "GetNormalizedInRoot\(bounds, root") "camera focus Overlay Bounds uses canvas fraction, not Camera.pixelRect"
Assert-True ($cameraFocus -match "ScreenToViewportPoint") "camera focus Overlay falls back to ScreenToViewportPoint"
Assert-True ($cameraFocus -notmatch "camera\.rect\s*=") "camera focus never writes Camera.rect"

$cameraTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveCameraTests.cs") -Raw
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_FoldExpandInner_PadSlot_IsIdentity") "Unity tests Fold Expand camera UI scale is identity"
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_CompactPhone_PhoneSlot_IsNotIdentity") "Unity tests selected non-reference Phone camera scale is not identity"
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_FlipClosedCover_FlipPhoneCoverSlot_IsIdentity") "Unity tests Flip Closed camera UI scale is Flip Cover identity"
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_FlipCoverWatch_PhoneSlot_IsNotIdentity") "Unity tests Flip cover watch does not identity a Phone camera slot"
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_FlipOpenedMain_PhoneSlot_IsIdentity") "Unity tests Flip Opened camera UI scale is identity"
Assert-True ($cameraTests -match "WriteCapturedState_FlipOpenedIdentity_KeepsLiveAuthoredValues") "Unity tests camera capture preserves default-profile identity"
Assert-True ($cameraTests -match "ComputeCanvasScaleRatio_CoverSafeArea_FoldPhoneCoverSlot_IsIdentity") "Unity tests Cover safe-area camera UI scale is identity"
Assert-True ($cameraTests -match "Apply_ScaleFromUiSize_FoldExpandInner_KeepsAuthoredPosition") "Unity tests Fold Expand camera Apply keeps authored pose"
Assert-True ($cameraTests -match "Apply_ScaleFromUiSize_FlipOpenedMain_KeepsAuthoredPosition") "Unity tests Flip Opened camera Apply keeps authored pose"
Assert-True ($cameraTests -match "GetCameraFocusViewport_OverlayHud_BoundsBelowTopBar") "Unity tests convert an Overlay HUD rect to a pane below the top"
Assert-True ($cameraTests -match "Focus_Fit_Bounds_KeepsWholeTableBelowHud") "Unity tests Fit keeps the table in the HUD play area"
Assert-True ($cameraTests -match "Focus_Cover_FillsThePane") "Unity tests Cover fills the pane"
Assert-True ($cameraTests -match "Focus_Cover_NoRenderer_LeavesOrthographicSize") "Unity tests Cover is a no-op without Renderer or Collider"
Assert-True ($cameraTests -match "Focus_Center_UsesRendererBoundsCenter") "Unity tests Center uses collider/renderer bounds"
Assert-True ($cameraTests -match "Focus_Fit_ShowsTheWholeTarget") "Unity tests Fit keeps the whole object visible"
Assert-True ($cameraTests -match "Layout_FocusFit_AdjustsAppliedOrthographicSeed") "Unity tests Focus Fit adjusts the applied projection seed"
Assert-True ($cameraTests -match "Focus_Auto_Phone_UsesBoundsWhenAssigned") "Unity tests Auto+Bounds on Phone"
Assert-True ($cameraTests -match "Focus_Auto_Fold_UsesSlotBoundsWhenAssigned") "Unity tests Fold Auto uses that slot's Focus Bounds"
Assert-True ($cameraTests -match "Layout_UsesFocusBounds_RequiresFocusAndMatchingRect") "Unity tests Focus Bounds ownership"
Assert-True ($cameraTests -match "Layout_UsesFocusBoundsRect_MatchesAssignedRect") "Unity tests Scene capture matches assigned Focus Bounds without Focus Target"
Assert-True ($cameraTests -match "GetCameraFocusViewport_BoundsMissing_IsFull") "Unity tests missing Bounds fall back to Full"
Assert-True ($cameraTests -match "GetCameraFocusViewport_StoredBoundsRect") "Unity tests stored Focus Bounds values without a live HUD"
Assert-True ($cameraTests -match "Layout_SlotsKeepIndependentFocusBoundsLayouts") "Unity tests Phone and Fold keep their own Focus Bounds layout"
Assert-True ($cameraTests -match "Layout_ApplyFocusBoundsLayout_WritesWidgetFields") "Unity tests Focus Bounds layout writes widget fields"
Assert-True ($cameraTests -match "Layout_ApplyFocusBoundsLayout_WithHub_MatchesApplyWidget") "Unity tests Focus Bounds Apply uses the widget canvas ratio"
Assert-True ($cameraTests -match "Layout_Apply_WritesFocusBoundsLayoutWithoutFocusTarget") "Unity tests Focus Bounds layout Apply without Focus Target"
Assert-True ($cameraTests -match "Layout_Apply_SkipBoundsLayout_LeavesHud") "Unity tests Scene capture can reframe without rewriting the HUD"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_WritesActiveSlotOnly") "Unity tests HUD sample writes the active slot only"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_WritesDisabledExactSlot") "Unity tests HUD sample writes the exact layout key when no slot is enabled"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_StoresLiveScaleAsDefaultScale") "Unity tests HUD capture stores origin Default Scale from live localScale"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_ScaleOn_KeepsDesignCanvasSentinel") "Unity tests HUD capture keeps (0,0) Design Canvas Size"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_AppliedHud_KeepsDesignInsetsAndOriginScale") "Unity tests Apply-scaled HUD is not recaptured into design insets or origin scale"
Assert-True ($cameraTests -match "Layout_CaptureActiveFocusBoundsLayout_ScaleOn_UnscalesEditedHud") "Unity tests HUD Scene edits store unscaled design-space insets"
Assert-True ($cameraTests -match "SizeChangeRequiresReapply_ScaleFromUiSize_WithHubScaler_True") "Unity tests camera UI scale still reapplies on Play Mode size ticks under a hub scaler"

$slotSeedTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveSlotSeedTests.cs") -Raw
Assert-True ($slotSeedTests -match "WriteCapturedLive_SkipsWhenHudAlreadyMatches") "Unity tests shared live capture skips an Apply-scaled HUD"
Assert-True ($slotSeedTests -match "CaptureFocusBoundsLayoutInto_ScaleToggle_ForcesRecapture") "Unity tests Bounds Layout Scale toggle recaptures even when the HUD already matches"
Assert-True ($slotSeedTests -match "WriteState_PreservesIndependentApplyLocalFlags") "Unity tests WriteState keeps applyLocalPosition and applyLocalRotation independent"
Assert-True ($slotSeedTests -match "CopyDesignScaleSettingsFrom_ClearsAuthoredDesignCanvasSize") "Unity tests capture clears baked Design Canvas Size"
Assert-True ($slotSeedTests -match "ExpectedSeedEnabled") "Unity tests new rows use shared seed-enabled policy"

$editorTestUtil = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveEditorTestUtil.cs") -Raw
Assert-True ($editorTestUtil -match "ExpectedSeedEnabled") "shared tests encode seed Enabled from SlotAllowed"
Assert-True ($editorTestUtil -match "IsSeedDisabledSlot") "shared seed-enabled policy leaves Flip Cover Landscape disabled"
Assert-True ($editorTestUtil -match "SetHubOverride") "shared tests pin hub Override In Editor"

$widgetTargetActiveTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveWidgetTargetActiveTests.cs") -Raw
Assert-True ($widgetTargetActiveTests -match "ApplyFromHub_DisabledExactSlot_DoesNotHideTarget") "Unity tests Disabled exact slot leaves the widget Target active"
Assert-True ($widgetTargetActiveTests -match "ApplyFromHub_LayoutOnly_DoesNotHideDisabledExactSlot") "Unity tests size-only Apply does not hide a Disabled exact slot"
Assert-True ($widgetTargetActiveTests -match "IsExactLayoutSlot_IgnoresFallback") "Unity tests exact live slot is not a Cover to Phone fallback"
Assert-True ($widgetTargetActiveTests -match "IsSeedDisabledSlot_FlipCoverLandscapeOnly") "Unity tests only Flip Cover Landscape seeds Disabled"
Assert-True ($widgetTargetActiveTests -match "ApplyFromHub_AuthoredInactiveTarget_StaysInactive") "Unity tests authored-inactive widget Targets stay inactive"
$twoSideTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveTwoSideUiControllerTests.cs") -Raw
Assert-True ($twoSideTests -match "ApplyFromHub_DisabledExactSlot_DoesNotHideTarget") "Unity tests two-side Disabled exact slot leaves the Target active"

$hubTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveUiHubTests.cs") -Raw
Assert-True ($hubTests -match "RefreshEditorLayoutAfterCanvas_RecoversBackgroundAfterCanvasSettle") "Unity tests canvas-ready pass re-covers Match Mode after canvases settle"
Assert-True ($hubTests -match "RefreshEditorLayoutAfterCanvas_NotifiesClientsThatSkipSizeReapply") "Unity tests canvas-ready pass notifies scaler/camera clients"
Assert-True ($hubTests -match "NotifyClients_LayoutOnly_NotifyAll_AppliesWhenSizeReapplyFalse") "Unity tests notify-all layout-only bypasses size reapply"
Assert-True ($hubTests -match "IsFormTransition_TrueWhenFormChanges") "Unity tests form-transition form change detection"
Assert-True ($hubTests -match "ShouldShowFormTransitionLoading_GatesPlayModeFormChangeAfterFirstApply") "Unity tests form-transition loading gate"
Assert-True ($hubTests -match "ShouldCoverFormChange_RespectsDestinationSlotFlag") "Unity tests form-transition destination slot flag"
Assert-True ($hubTests -match "FormTransitionDefaults_DisablesFlipPhoneCoverSlots") "Unity tests form-transition default slots"
Assert-True ($hubTests -match "ResponsiveFormTransitionLoading\.IsFormChange") "Unity tests use ResponsiveFormTransitionLoading.IsFormChange"
Assert-True ($hubTests -match "ResponsiveFormTransitionLoading\.ShouldCoverFormChange") "Unity tests use ResponsiveFormTransitionLoading.ShouldCoverFormChange"
Assert-True ($hubTests -match "ResponsiveFormTransitionLoading\.ShouldShow") "Unity tests use ResponsiveFormTransitionLoading.ShouldShow"
Assert-True ($hubTests -match "ResponsiveActiveObjectSlots\.FormTransitionDefaults") "Unity tests form-transition slot defaults factory"
Assert-True ($hubTests -match "NotifyClients_LayoutOnly_SkipsAuthoringClient") "Unity tests layout-only Apply skips the authored Target"
Assert-True ($hubTests -match "NotifyClients_ClientRegisteredDuringApply_ReceivesCurrentLayout") "Unity tests clients registered during Apply receive the current layout"
Assert-True ($hubTests -match "NotifyClients_ControllerEnabledDuringApply_SettlesBeforeLateClient") "Unity tests mid-Apply controller enable settles before late clients"
Assert-True ($hubTests -match "ScrollView_LayoutOnly_InvokesApplyForViewportRelayout") "Unity tests scroll layout-only InvokesApply for Relayout"
Assert-True ($hubTests -match "ApplyFromHub_CoversBackgroundAfterShowingWidgetParent") "Unity tests Match Mode cover runs after an authored-inactive parent is shown"
Assert-True ($hubTests -match "ApplyCoverHeight_StretchWidth_ClearsLetterboxX") "Unity tests HeightOnly clears full-stretch letterbox X"
Assert-True ($hubTests -match "ApplyCoverHeight_NonStretchWidth_KeepsSizeDeltaX") "Unity tests HeightOnly keeps sizeDelta.x when not full-stretch"
Assert-True ($hubTests -match "ApplyCoverWidth_StretchHeight_ClearsLetterboxY") "Unity tests WidthOnly clears full-stretch letterbox Y"
Assert-True ($hubTests -match "ApplyCoverWidth_NonStretchHeight_KeepsSizeDeltaY") "Unity tests WidthOnly keeps sizeDelta.y when not full-stretch"
Assert-True ($hubTests -match "ApplyBackground_Manual_LeavesSizeDelta") "Unity tests Manual Match Mode leaves sizeDelta"
Assert-True ($hubTests -match "Controller_SizeChangeRequiresReapply_DisabledPad_UsesPhoneFallback_True") "Unity tests Disabled Pad still size-reapplies through Phone fallback"

$simIntegrationTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveUiSimulatorIntegrationTests.cs") -Raw
Assert-True ($simIntegrationTests -match "SlotFocus_CoverToMainSize_RequiresLayoutApply") "Unity tests Flip Cover to Main WxH is not treated as Simulator flicker"
Assert-True ($simIntegrationTests -match "Session_ExposesScreenSyncedAfterCoverToMain") "Unity tests Fold session exposes Cover to Main screen-synced"
Assert-True ($simIntegrationTests -match "ScreenCommit_ExposesDeferredAndImmediatePaths") "Unity tests Fold screen commit exposes one Cover to Main helper"

$scrollLayoutTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveScrollLayoutTests.cs") -Raw
Assert-True ($scrollLayoutTests -match "SeedAll_EnablesFlipHalfOpenedAndLeavesFlipCoverLandscapeDisabled") "Unity tests scroll Flip HalfOpened seeds Enabled"
Assert-True ($scrollLayoutTests -match "ExpectedScrollSeedEnabled") "Unity tests scroll seed Enabled uses IsSeedDisabledSlot"
Assert-True ($scrollLayoutTests -match "IsSeedDisabledSlot") "Unity tests scroll Flip Cover Landscape seeds Disabled"

$scrollSyncTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveScrollSyncTests.cs") -Raw
Assert-True ($scrollSyncTests -match "SameAuthoring_TrueWhenMetricsMatch") "Unity tests scroll SameAuthoring skips foldout-only edits"
Assert-True ($scrollSyncTests -match "CaptureHostToActiveSlot_WritesEnabledApplySlot") "Unity tests scroll host capture writes the active slot"
Assert-True ($scrollSyncTests -match "CaptureHostToActiveSlot_PreservesDisabledFlag") "Unity tests scroll host capture does not force-enable the slot"
Assert-True ($scrollSyncTests -match "ApplyFromHub_WritesHostMetrics") "Unity tests scroll hub Apply writes host SerializedObject"
Assert-True ($scrollSyncTests -match "CopySlotToHost_DisabledHub_DoesNotWriteHost") "Unity tests scroll LivePush waits for an enabled hub"

$chromeTests = Get-Content (Join-Path $PSScriptRoot "Editor\ResponsiveUiInspectorChromeTests.cs") -Raw
Assert-True ($chromeTests -match "InspectorSectionKey_DiffersPerComponent") "Unity tests lock per-component Inspector foldout keys"
Assert-True ($chromeTests -match "PropertySectionKey_IncludesInstanceIdAndPath") "Unity tests lock per-slot Inspector foldout keys"
Assert-True ($chromeTests -match "SlotSectionHeight_CollapsedIgnoresBody") "Unity tests collapsed slot sections ignore body height"
Assert-True ($chromeTests -match "AtZeroIndent_RestoresCallerIndent") "Unity tests slot height measure restores Inspector indent"
Assert-True ($chromeTests -match "FieldLabelWidthFor_WideRow_UsesStandardWidth") "Unity tests lock the Inspector label column width"
Assert-True ($chromeTests -match "DrawFoldout_DoesNotKeepCallerIndent") "Unity tests foldout arrows do not consume Inspector indent"
Assert-True ($chromeTests -match "HeaderFoldoutPad_IsSixteen") "Unity tests lock 16px header foldout pad"
Assert-True ($chromeTests -match "NestedContentRect_OffsetsByFoldoutArrow") "Unity tests nested foldout content sits under the title"
Assert-True ($chromeTests -match "HeaderFoldoutRect_Tinted_InsetsPastAccent") "Unity tests section foldout arrows sit inside the tint bar"
Assert-True ($chromeTests -match "HeaderContentInset_Tinted_MatchesFoldoutPlusPad") "Unity tests nested GUILayout bodies match tinted header inset"
Assert-True ($chromeTests -match "HeaderTintInset_Untinted_IsHeaderPad") "Unity tests unboxed and boxed headers share the same pad"

$cameraState = Get-Content (Join-Path $runtime "ResponsiveCameraState.cs") -Raw
Assert-True ($cameraState -match "CreateDefault\(bool enabled = true\)") "new camera CreateDefault can start Enabled"
Assert-True ($cameraState -match "uiScaleMode = ResponsiveDesignScaleMode.Auto") "new camera slots default Scale Mode Auto"
Assert-True ($cameraState -match "(?m)^\s*public bool enabled;") "camera Enabled has no true field default"
Assert-True ($cameraState -notmatch "public bool enabled = true") "missing YAML camera Cover stays Disabled"
Assert-True ($cameraState -match "camera\.rect") "camera state writes Camera.rect"
Assert-Match $cameraState 'w <= 0f \|\| h <= 0f' "degenerate viewport becomes full screen"
Assert-True ($cameraState -match "applyLocalPosition") "camera state can write local position"
Assert-True ($cameraState -match "applyLocalRotation") "camera state can write local rotation"
Assert-True ($cameraState -match "applyLocalTransform") "camera Apply Local is one flag for position and rotation"
Assert-True ($cameraState -match "scaleFromUiSize") "camera state can scale from UI size"
Assert-True ($cameraState -match "ResolveLiveUiSize") "camera UI scale uses the same default-profile identity as widgets"
Assert-True ($cameraState -match "scaleProjection") "camera state can scale projection independently of position"
Assert-True ($cameraState -notmatch "public bool scaleOrthographicSize\b") "no scaleOrthographicSize alias (scaleProjection is the field)"
Assert-True ($cameraState -match "ScaleOrthographicSize") "camera ortho scale uses aspect framing"
Assert-True ($cameraState -match "ResolveAutoScaleMode") "camera projection Auto Scale Mode resolves Width or Height"
Assert-True ($cameraTests -match "ScaleOrthographicSize_Auto_MatchesResolvedWidthOrHeight") "Unity tests Auto projection matches Width or Height"
Assert-True ($cameraTests -match "ScaleOrthographicSize_Auto_PhoneVsPadDesign_DifferOnSameLive") "Unity tests Auto projection uses the slot form Design Canvas"
Assert-True ($cameraState -match "UnscaleOrthographicSize") "camera ortho capture unscales with aspect framing"
Assert-True ($cameraState -match "ScaleFieldOfView") "camera FOV scale uses aspect framing"
Assert-True ($cameraState -match "UnscaleFieldOfView") "camera FOV capture unscales with aspect framing"
Assert-True ($cameraState -notmatch "ScaleProjectionValue") "no compatibility-only projection value helpers"
Assert-True ($cameraState -match "FovToTan") "FOV scale uses tan of half-FOV, not degrees × pixel ratio"
Assert-True ($cameraState -match "ScaleOrthographicSize\(tanHalf") "FOV aspect framing shares ortho math"
Assert-True ($cameraState -match "ResponsiveRectState\.OrientSizeToMatch\(designUiSize, currentUiSize\)") "camera projection scale orients design size to live size (shared widget helper)"
Assert-True ($cameraState -match "public bool orthographic") "camera state stores Orthographic vs Perspective"
Assert-True ($cameraState -match "public Transform focusTarget") "camera slot can focus a world object"
Assert-True ($cameraState -match "focusScreen") "camera slot names a Focus Screen"
Assert-True ($cameraState -match "public RectTransform focusBounds") "camera slot has Focus Bounds"
Assert-True ($cameraState -match "public ResponsiveRectState focusBoundsLayout") "camera slot stores Focus Bounds like a widget item"
Assert-True ($cameraState -match "\[HideInInspector\][\s\S]{0,80}focusBoundsLayout") "camera Focus Bounds layout is drawn by the slot drawer, not a nested foldout"
Assert-True ($cameraState -match "ApplyFocusBoundsLayout") "camera slot can apply Focus Bounds like a widget"
Assert-True ($cameraState -match "hub\.ApplyWidget") "camera slot Focus Bounds Apply uses the widget canvas ratio"
Assert-True ($cameraState -match "camera\.orthographic = orthographic") "camera Apply writes Projection type"
Assert-True ($cameraState -match "ScaleDesignPosition") "camera state scales design position"
Assert-True ($cameraState -match "UnscaleDesignPosition") "camera state can unscale design position"
Assert-Match $cameraState 'GetResolvedDesignUiSize[\s\S]{0,400}GetDefaultDesignCanvasSize' "camera UI scale always resolves the Responsive UI form default"
Assert-True ($cameraState -match "GetResolvedDesignUiSize\(slotForm, slotOrientation\)") "camera UI scale resolves the slot-oriented form default"

$cameraStateDrawer = Get-Content (EditorPath "ResponsiveCameraStateDrawer.cs") -Raw
Assert-True ($cameraStateDrawer -match "GetWatchScreenSize") "camera slot UI size fallback uses Simulator watch"
Assert-True ($cameraStateDrawer -notmatch "Screen\.width") "camera state drawer does not read Game View Screen"
Assert-True ($cameraState -match "applyLocalTransform") "camera state has one Apply Local flag"
Assert-True ($cameraStateDrawer -match "Apply Local") "camera slot drawer draws one Apply Local checkbox"
Assert-True ($cameraStateDrawer -match "DrawLocalTransformFields") "camera Local Position and Euler share Apply Local"
Assert-True ($cameraStateDrawer -match "Local Position") "camera state drawer draws Local Position per slot"
Assert-True ($cameraStateDrawer -notmatch 'new GUIContent\(\s*"Position"') "camera slots do not show live Position in place of stored pose"
Assert-True ($cameraStateDrawer -notmatch 'new GUIContent\(\s*"Rotation"') "camera slots do not show live Rotation in place of stored pose"
Assert-True ($cameraStateDrawer -match "Other profiles keep their own stored pose") "camera Local Position stays per device slot when Apply Local is off"
Assert-True ($cameraStateDrawer -match "Local Euler Angles") "camera state drawer draws Local Euler per slot"
Assert-True ($cameraStateDrawer -match "Capture From Camera") "camera state drawer can capture live Target into slot"
Assert-True ($cameraStateDrawer -match "stay synchronized") "camera Capture synchronizes focused slot values"
Assert-True ($cameraStateDrawer -match "GetFocusMeasureHint") "camera slot warns when Cover/Fit cannot measure the Focus Target"
Assert-True ($cameraStateDrawer -match "Renderer or Collider") "camera Cover/Fit warning names Renderer or Collider"
Assert-True ($cameraStateDrawer -match "ResponsiveCameraEditorUtil\.WriteCapturedState") "camera state drawer uses shared capture write helper"
Assert-True ($cameraStateDrawer -match "ResponsiveCameraEditorUtil\.UnscaleLiveToDesign") "camera Local Position capture preserves default-profile identity (shared unscale helper)"
Assert-True ($cameraStateDrawer -match "applyPos == null \|\| applyRot == null") "camera Apply Local drawer guards missing serialized fields"
Assert-True ($cameraStateDrawer -match "toggle == null") "camera state drawer null-checks Apply toggles"
Assert-Match $cameraStateDrawer 'scaleFromUi != null && scaleFromUi\.boolValue' "camera state drawer null-checks Scale From UI Size"
Assert-True ($cameraStateDrawer -match "Scale From UI Size") "camera state drawer draws Scale From UI Size"
Assert-True ($cameraStateDrawer -match "TransformFieldsHeight") "camera slot height includes Local Position / Euler rows so Scale fields are not clipped"
Assert-True ($cameraStateDrawer -match "DrawVector3Field") "camera Local Position / Euler stay on one Inspector row"
Assert-True ($cameraStateDrawer -match "EnableApplyLocalForScale") "camera Scale From UI Size turns on Apply Local so position can scale"
Assert-True ($cameraStateDrawer -match "Projection") "camera state drawer exposes Orthographic / Perspective"
Assert-True ($cameraStateDrawer -match "GetLiveProjectionHint") "camera slot explains live Orthographic Size vs authored seed"
Assert-True ($cameraStateDrawer -match "Live Size") "camera slot shows live Camera size when Focus or Scale Projection owns it"
Assert-True ($cameraStateDrawer -match "slot seed") "camera Orthographic Size field is labeled as the slot seed when live differs"
Assert-True ($cameraState -match "From\(Camera camera\)") "camera From auto-detects Projection from Target"
Assert-True ($cameraStateDrawer -match "EditorGUI\.DisabledScope\(true\)") "camera Design UI Size is read-only"
Assert-True ($cameraStateDrawer -notmatch '"designUiSize"') "camera Design UI Size row is display-only (no per-repaint serialized write)"
Assert-True ($cameraStateDrawer -match "Locked to Responsive UI settings") "camera Design UI Size reports its locked source"
Assert-True ($cameraStateDrawer -match "OrientSize") "camera Design UI Size hint orients to the slot"

Assert-True (Test-Path (EditorPath "ResponsiveCameraEditorUtil.cs")) "ResponsiveCameraEditorUtil exists"
$cameraUtil = Get-Content (EditorPath "ResponsiveCameraEditorUtil.cs") -Raw
Assert-True ($cameraUtil -match "ResolveIdentityLiveSize") "camera design-unscale helper preserves default-profile identity"
Assert-True ($cameraUtil -match "ResolveIdentityLiveSize") "camera capture preserves default-profile identity"
Assert-True ($cameraUtil -match "GetWatchScreenSize") "camera LivePush fallback uses Simulator watch not Game View Screen"
Assert-True ($cameraUtil -notmatch "Screen\.width") "camera editor util does not read Game View Screen"
Assert-True ($cameraUtil -match "WriteCapturedState") "camera editor util writes captured slots"
Assert-True ($cameraUtil -match 'applyLocalPosition\.boolValue = value\.applyLocalPosition') "WriteState writes applyLocalPosition, not applyLocalTransform"
Assert-True ($cameraUtil -match 'applyLocalRotation\.boolValue = value\.applyLocalRotation') "WriteState writes applyLocalRotation, not applyLocalTransform"
Assert-True ($cameraUtil -notmatch 'applyLocalPosition\.boolValue = value\.applyLocalTransform') "WriteState does not OR-collapse applyLocalPosition"
Assert-True ($cameraUtil -notmatch 'applyLocalRotation\.boolValue = value\.applyLocalTransform') "WriteState does not OR-collapse applyLocalRotation"
Assert-True ($cameraUtil -match "if \(enabled != null\)") "camera editor util null-checks ReadState properties"
Assert-True ($cameraUtil -match "!ResponsiveWidgetLayout\.IsSeedDisabledSlot\(i\)") "new camera rows leave Flip Cover Landscape disabled"
Assert-Match $cameraUtil 'scaleProp != null && scaleProp\.boolValue' "camera editor util null-checks WriteCapturedState scale"
Assert-True ($cameraUtil -match "GetDefaultDesignCanvasSize\(slotForm\)") "camera capture unscales from the locked form default"
Assert-True ($cameraUtil -match "UnscaleFieldOfView\(") "camera capture unscales Field Of View with aspect framing"
Assert-True ($cameraUtil -match "modeProp\.intValue") "camera scale mode uses enum intValue"
Assert-True ($cameraUtil -notmatch '"designUiSize"') "camera editor util writes no removed Design UI Size field"
Assert-True ($cameraUtil -match "MatchesLive") "camera editor util detects dirty Scene cameras"
Assert-True ($cameraUtil -match "ResolveLiveUiSize") "camera MatchesLive uses default-profile identity"
Assert-True ($cameraUtil -match "FitBackground") "camera editor util fits the camera background"
Assert-True ($cameraUtil -match "ApplyFocus") "camera editor util applies focus after LivePush"
Assert-True ($cameraUtil -match "ApplyFocusBoundsLayout\(stateProp\)") "editor LivePush writes Focus Bounds layout like a widget"
Assert-True ($cameraUtil -match "hub\.ApplyWidget") "editor LivePush Focus Bounds uses the widget canvas ratio"
Assert-True ($cameraUtil -match 'FindPropertyRelative\("focusFit"\)\?\.intValue') "editor Focus Fit uses intValue"
Assert-True ($cameraUtil -match "SlotHasFocus") "camera editor util detects a focus target"
Assert-True ($cameraUtil -match "SlotHasFocus") "camera Scene capture uses the active slot's Focus"
Assert-True ($cameraUtil -match "writeOrtho = false") "camera capture does not bake Cover/Fit ortho into focused slots"
Assert-True ($cameraUtil -match "seedTransform") "camera capture can skip forcing Apply Local"
Assert-True ($cameraUtil -match "localPositionProp\.vector3Value = localPosition") "camera Capture writes the live pose into focused slots"
Assert-True ($cameraUtil -match "if \(seedTransform\)") "camera Scene sync does not write Local Position without seedTransform"
Assert-True ($cameraUtil -match "!seedTransform && !captureLiveValues && SlotHasFocus") "non-live focused capture preserves derived projection"
Assert-True ($cameraUtil -match "SlotFocusOwnsProjection") "camera Scene capture distinguishes Center from Fit/Cover focus"
Assert-True ($cameraUtil -match "writeFov = !preserveFocusProjection") "camera Scene capture protects focus-derived Field Of View"
Assert-True ($cameraUtil -match "writeOrtho = !preserveFocusProjection") "camera Scene capture protects focus-derived Orthographic Size"
Assert-True ($cameraUtil -match "expectedOrtho - camera.orthographicSize") "camera MatchesLive compares live ortho regardless of Apply flags"
Assert-True ($cameraUtil -match "ignoreFraming") "camera Scene capture skips pose owned by focus"
Assert-True ($cameraUtil -match "RecordPreviewTargets") "camera preview records camera and background undo"
Assert-True ($cameraUtil -match "FindObjectsInactive\.Include") "camera editor util includes inactive cameras"
Assert-True ($cameraUtil -match "public static bool CopyFallback") "enabling a camera slot copies its fallback source (Phone, or Pad for iPhone Duo Seated)"
Assert-True ($cameraUtil -match "CopySlotAcrossLayouts") "camera utility bulk-copies one exact slot across all camera rows"
Assert-True ($cameraUtil -match "public static void SeedSlots") "camera new rows share SeedSlots"
Assert-True ($cameraUtil -match "public static void ResetSlot") "camera slots can reset to CreateDefault"
Assert-True ($cameraUtil -match "public static void ResetSlots") "clearing camera Target resets every slot"
Assert-True ($cameraUtil -match "public static void ResetNewListRow") "list + clears Unity duplicated camera rows"
Assert-True ($cameraUtil -match "public static void WriteCaptureToSlots") "camera first-assign capture is shared"
Assert-True ($cameraUtil -match "ApplyInspectorTargetChange") "camera Target field uses shared assign/clear path"
Assert-True ($cameraUtil -notmatch "public static void CopySlot\(") "camera Copy lives on the shared clipboard"

Assert-True (Test-Path (EditorPath "ResponsiveCameraSync.cs")) "ResponsiveCameraSync exists"
$cameraSync = Get-Content (EditorPath "ResponsiveCameraSync.cs") -Raw
Assert-True ($cameraSync -match "postprocessModifications") "camera Scene→slot sync listens to Undo like widgets"
Assert-True ($cameraSync -notmatch "ObjectChangeEvents|changesPublished") "camera sync does not recapture hub Apply via ObjectChangeEvents"
Assert-True ($cameraSync -match "m_LocalPosition") "camera sync captures user-authored Target position"
Assert-True ($cameraSync -match "seedTransform: false") "camera Scene capture preserves authored apply flags"
Assert-True ($cameraSync -match "captureLiveValues: true") "camera sync writes user-authored Target values"
Assert-True ($cameraSync -match "CaptureChangedTargets|WriteCapturedState") "camera sync writes active Simulator slot"
Assert-True ($cameraSync -match "SuppressCapture") "camera sync can suppress LivePush echo"
Assert-True ($cameraSync -match "EditorBeginApply") "camera sync binds hub Apply suppress"
Assert-True ($cameraSync -match "BeginApplySuppress") "camera hub Apply increments capture suppress"
Assert-True ($cameraSync -match "FitBackground") "camera sync re-fits the background after Scene capture"
Assert-True ($cameraSync -match "ResolveCaptureSlot") "camera sync falls back to exact layout key when no slot enabled"
Assert-True ($cameraSync -match "m_OrthographicSize|orthographic size") "camera sync captures orthographic size"
Assert-True ($cameraSync -match "m_Orthographic") "camera sync captures Orthographic / Perspective projection"
Assert-True ($cameraSync -match "MatchesLive") "camera sync skips write when Scene already matches slot"
Assert-True ($cameraSync -match "currentlyEnabled") "camera Scene capture preserves the slot Enabled flag"
Assert-True ($cameraSync -notmatch "enabled: true") "camera Scene capture does not force-enable the resolved slot"
Assert-True ($cameraSync -match "ResponsiveUiWidgetSync\.IsLayoutPropertyPath") "camera sync captures Focus Bounds layout properties (shared widget markers)"
Assert-True ($cameraSync -notmatch "afterAssemblyReload") "camera sync rebinds from its [InitializeOnLoad] constructor only"
Assert-True ($cameraSync -match "CaptureFocusBoundsOwners") "camera sync captures edited Focus Bounds into the active slot"
Assert-True ($cameraSync -match "CaptureFocusBoundsLayoutInto") "camera sync writes Bounds Layout through SerializedObject so the Inspector updates"
Assert-True ($cameraSync -match "ApplyModifiedProperties") "camera Focus Bounds capture commits Inspector slot values"

Assert-True (Test-Path (EditorPath "ResponsiveScrollSync.cs")) "ResponsiveScrollSync exists"
$scrollSync = Get-Content (EditorPath "ResponsiveScrollSync.cs") -Raw
Assert-True ($scrollSync -match "postprocessModifications") "scroll host→slot sync listens to Undo like widgets"
Assert-True ($scrollSync -notmatch "ObjectChangeEvents|changesPublished") "scroll sync does not recapture hub Apply via ObjectChangeEvents"
Assert-True ($scrollSync -match "IsHostMetricPath") "scroll sync matches only root host cellInterval / scrollOffset"
Assert-True ($scrollSync -match "scrollMetrics") "scroll sync ignores slot YAML cellInterval paths"
Assert-True ($scrollSync -match "HostIntervalProp") "scroll sync uses the shared host cellInterval property name"
Assert-True ($scrollSync -match "CaptureHostToActiveSlot") "scroll sync writes the active Simulator slot"
Assert-True ($scrollSync -match "SuppressCapture") "scroll sync can suppress LivePush echo"
Assert-True ($scrollSync -match "EditorBeginApply") "scroll sync binds hub Apply suppress"
Assert-True ($scrollSync -match "BeginApplySuppress") "scroll hub Apply increments capture suppress"
Assert-True ($scrollSync -match "ApplyLayoutToHost") "scroll hub Apply pushes host SerializedObject metrics"
Assert-True ($scrollSync -match "IgnoreHostCapture") "scroll hub Apply ignores host recapture like cameras"
Assert-True ($scrollSync -match "ResponsiveScrollEditorUtil\.CanLivePush\(scroll\)") "scroll capture skips components / hubs that missed the Simulator switch"
Assert-True ($scrollSync -notmatch "afterAssemblyReload") "scroll sync rebinds from its [InitializeOnLoad] constructor only"
Assert-True ($cameraSync -match "applyBoundsLayout: false") "camera Focus Bounds capture does not paint the old slot back onto the HUD"
Assert-True ($cameraSync -match "EditorUsesFocusBounds") "camera sync matches edited rects to slot Focus Bounds"
Assert-True ($cameraSync -match "RecordPreviewTargets") "camera Focus Bounds capture records camera and background undo"
Assert-True ($cameraSync -notmatch "ApplyFocusBoundsOwners") "camera sync does not re-apply old Bounds Layout over a Scene HUD edit"
Assert-True ($cameraSync -notmatch "EditorCaptureActiveFocusBounds") "camera sync does not recapture Bounds Layout from a C# field write that skips the Inspector"

$cameraEditor = Get-Content (EditorPath "ResponsiveCameraEditor.cs") -Raw
Assert-True ($cameraEditor -match "EnsureDefaultCamera") "camera Inspector seeds one layout on Add Component"
Assert-True ($cameraEditor -match "ConsumeSeedDefaultLayout") "camera Inspector does not seed existing empty camera lists"
Assert-True ($cameraEditor -match "SyncCameras") "camera Inspector syncs slot Enabled from orientations"
Assert-True ($cameraEditor -match "ApplyListFoldouts") "camera Inspector expands detected form foldout (opt-in FoldPhoneCover)"
Assert-True ($cameraEditor -match "FoldablePostureDetector") "camera Inspector documents FoldablePostureDetector"
Assert-True ($cameraEditor -notmatch "FoldableModeManager") "camera Inspector has no FoldableModeManager"
Assert-True ($cameraEditor -notmatch '"deferViewportToFoldableManager"') "camera Inspector hides obsolete viewport deferral"
Assert-True ($cameraEditor -match "InspectorSectionKey") "camera Inspector foldouts are per component"
Assert-True ($cameraEditor -notmatch "applyCanvasScaler") "camera Inspector hides Canvas Scaler fields"
Assert-True ($cameraEditor -match "Production \(real devices\)") "camera Inspector documents device pipeline"
Assert-True ($cameraEditor -match "lock Design UI Size to Project Settings") "camera Inspector documents locked Project Settings Design UI Size"
Assert-True ($cameraEditor -match "ResponsiveCameraEditorUtil\.ResetNewListRow") "new camera rows start empty instead of copying the row above"
Assert-True ($cameraEditor -match "ForEachNewRow") "camera Inspector resets new rows before the list draws"
Assert-True ($cameraEditor -match "ResponsiveCameraEditorUtil\.SeedSlots") "new camera rows seed slots from Phone"
Assert-True ($cameraEditor -match "Bulk Copy Camera Slots") "camera Inspector exposes bulk slot copy"
Assert-True ($cameraEditor -match "ResponsiveUiBulkSlotCopyPanel") "camera Inspector uses the shared bulk-copy panel"
Assert-True ($cameraEditor -match 'InspectorKey\("Cameras"\)') "camera Inspector groups tools and rows under Cameras"
Assert-Match $cameraEditor 'new GUIContent\("Camera Items"\)' "camera list is labeled as a child of Cameras"
Assert-True ($cameraEditor -match "BoxedFoldoutSection") "camera Inspector uses the shared boxed parent panel"
Assert-True ($cameraEditor -match "ColoredFoldoutSection") "camera Inspector uses tinted collapsible Usage notes / Cameras sections"
$bulkPanel = Get-Content (EditorPath "ResponsiveUiBulkSlotCopyPanel.cs") -Raw
Assert-True ($bulkPanel -match "EditorApplication\.delayCall") "camera bulk copy defers mutation past the GUI event"
Assert-True ($cameraEditor -match "ResponsiveUiBulkCopyUtil\.Execute") "camera bulk copy uses the shared Undo helper"
Assert-True ((Get-Content (EditorPath "ResponsiveUiBulkCopyUtil.cs") -Raw) -match "Undo\.RevertAllDownToGroup") "shared bulk copy cleans up a deferred no-op Undo group"
Assert-True ($cameraEditor -match "NotifyWidgetLivePreview") "camera bulk copy suppresses competing validate Apply"
Assert-Match $cameraEditor 'hub != null && hub\.enabled' "camera bulk preview waits for an enabled hub"
Assert-True ($cameraEditor -match "LogBulkCopyResult") "camera bulk copy reports completion in the Unity Console"
Assert-True ($cameraEditor -match "responsiveUi") "camera Inspector assigns the Canvas hub"
Assert-True ($cameraEditor -match "EnsureHubField") "camera Inspector auto-assigns the Canvas hub"
Assert-True ($cameraEditor -match "DrawHubField") "camera Hub field uses the shared label column"
Assert-True ($cameraEditor -notmatch "ResponsiveActiveObjectEntryDrawer.DrawSection") "camera Inspector does not draw Active Objects"
Assert-True ($cameraEditor -notmatch "ResponsiveLayoutEventsDrawer.DrawSection") "camera Inspector does not draw Events"
Assert-True ($cameraEditor -notmatch "phoneOnlyObjects") "camera Inspector no longer draws the phone-only list"

$inspectorFoldouts = Get-Content (EditorPath "ResponsiveUiInspectorFoldouts.cs") -Raw
Assert-True ($inspectorFoldouts -match "#region Constants") "Inspector chrome sizes and tints live in Constants"
Assert-True ($inspectorFoldouts -match "#region Inspector") "Inspector chrome caller APIs live in Inspector"
Assert-True ($inspectorFoldouts -match "#region Helpers") "Inspector chrome inset math lives in Helpers"
Assert-True ($inspectorFoldouts -match "defaultOpen = false") "Inspector utility sections start collapsed"
Assert-True ($inspectorFoldouts -match "BoxedFoldoutSection") "list Inspectors share one boxed parent-section layout"
Assert-True ($inspectorFoldouts -match "FoldoutArrowWidth") "section foldout arrows use a fixed width past the tint bar"
Assert-True ($inspectorFoldouts -match "FieldLabelWidth") "Inspector values share one label column width"
Assert-True ($inspectorFoldouts -match "DrawFoldout") "Inspectors draw foldouts without a second Unity indent"
Assert-True ($inspectorFoldouts -match "BoldFoldoutStyle") "section titles are the foldout label so the arrow and name stay together"
Assert-True ($inspectorFoldouts -match "DrawSlotSection") "camera slots share tinted collapsible section chrome"
Assert-True ($inspectorFoldouts -match "SlotSectionHeight") "slot sections report Inspector height"
Assert-True ($inspectorFoldouts -match "open && next") "slot sections do not draw a body into a collapsed box"
Assert-True ($inspectorFoldouts -match "ColoredFoldoutSection") "GUILayout Inspectors share tinted collapsible sections"
Assert-True ($inspectorFoldouts -match "InspectorSectionKey") "GUILayout foldouts are keyed per component"
Assert-True ($inspectorFoldouts -match "PropertySectionKey") "slot foldouts are keyed per component and property path"
Assert-True ($inspectorFoldouts -match "FieldLabelWidthFor") "slot value columns keep a minimum field width"
Assert-True ($inspectorFoldouts -match "NestedContentRect") "nested foldout bodies sit under the section title"
Assert-True ($inspectorFoldouts -match "HeaderFoldoutPad = 16f") "top-level section arrows use 16px header pad"
Assert-True ($inspectorFoldouts -match "HeaderFoldoutPad") "section foldout arrows keep padding inside the tint bar"
Assert-True ($inspectorFoldouts -match "HeaderFoldoutRect") "section foldout arrows sit after the tint accent"
Assert-True ($inspectorFoldouts -match "overflow = new RectOffset") "section foldout arrows do not draw outside the header"
Assert-Match $inspectorFoldouts 'DrawHeaderFoldout[\s\S]{0,800}BeginVertical\(EditorStyles\.helpBox\)' "boxed section helpBox wraps the open body, not the header"
Assert-True ($inspectorFoldouts -match "DrawNestedGui") "GUILayout section bodies share the foldout-title indent"
Assert-True ($inspectorFoldouts -match "DrawNestedContent") "property-drawer bodies share the foldout-title indent"
Assert-True ($inspectorFoldouts -match "DrawGuiFoldout") "GUILayout Inspectors share one foldout-plus-body helper"
Assert-True ($inspectorFoldouts -match "DrawPropertyFoldout") "item drawers share one foldout-plus-body helper"
Assert-True ($inspectorFoldouts -match "DrawHubField") "Inspector Hub fields share one label and tooltip"
Assert-True ($inspectorFoldouts -match "DrawRootProperty") "Inspector Hub fields use the shared label column"
Assert-True ($inspectorFoldouts -match "HeaderTintInset") "section arrows share one tint-plus-pad inset"
Assert-True ($inspectorFoldouts -match "AtZeroIndent") "slot body height is measured at indent 0"
Assert-True ($inspectorFoldouts -notmatch "public static void FoldoutSection") "dead un-tinted FoldoutSection wrapper is gone"
Assert-True ($inspectorFoldouts -notmatch "public static void Section\(") "dead Section wrapper is gone"
Assert-True ($inspectorFoldouts -match "TintBlue") "Inspectors share one section color palette"

Assert-True (-not (Test-Path (Join-Path $pkg "FoldAndFlip\Runtime\FoldableModeManager.cs"))) "FoldableModeManager removed - detector is sole posture owner"
$foldDetector = Get-Content (Join-Path $pkg "FoldAndFlip\Runtime\FoldablePostureDetector.cs") -Raw
Assert-True (Test-Path (Join-Path $pkg "FoldAndFlip\Runtime\FoldablePostureDetector.cs")) "FoldablePostureDetector exists"
Assert-True ($foldDetector -match "deviceKind = FoldableDeviceKind\.Unknown") "new detectors do not classify every Android device as foldable"
Assert-True ($foldDetector -match "persistAcrossScenes") "FoldablePostureDetector can persist across scenes"
Assert-True ($foldDetector -match "DontDestroyOnLoad") "FoldablePostureDetector uses DontDestroyOnLoad when persistAcrossScenes"
Assert-True ($foldDetector -match "s_Persistent") "persistent FoldablePostureDetector singleton survives scene loads"
Assert-True ($foldDetector -match "s_LookupComplete") "FindActiveInstance caches a miss so gameplay cameras do not FindObjectsByType every Apply"
Assert-True ($foldDetector -match "s_Persistent != null && s_Persistent\.isActiveAndEnabled") "FindActiveInstance recovers the DontDestroyOnLoad detector after a cached miss"
Assert-True ($foldDetector -match "!candidate\.isDuplicatePersist") "FindActiveInstance skips persistent duplicates pending destruction"
Assert-True ($foldDetector -match "!other\.isDuplicatePersist") "owner promotion skips persistent duplicates pending destruction"
Assert-True ($foldDetector -match "void ActivateOwner\(\)") "all detector owner transitions share one initialization path"
Assert-True ($foldDetector -match "EditorInitializing\?\.Invoke\(this\);[\s\S]{0,100}ApplyPlayLayout\(\);\s*ActiveInstanceChanged\?\.Invoke\(\);") "Simulator kind and Play layout initialize before ownership listeners apply"
Assert-True ($foldDetector -match "layoutState.deviceKind == FoldableDeviceKind.Unknown") "native snapshot does not wipe configured Device Kind"
Assert-True ($foldDetector -notmatch "FoldableLayoutApplier") "FoldablePostureDetector does not call FoldableLayoutApplier"
Assert-True ($foldDetector -notmatch "mainCamera|skyBoxCamera|controlsCanvas|healthCanvas|flexBG|UIBlur") "FoldablePostureDetector has no camera/canvas hinge refs"
Assert-True ($foldDetector -match "LayoutChanged") "FoldablePostureDetector raises LayoutChanged"
Assert-True ($foldDetector -match "ActiveInstanceChanged") "FoldablePostureDetector raises ActiveInstanceChanged"
Assert-True ($foldDetector -match "NativeAttachRetrySeconds") "FoldablePostureDetector retries failed Android bridge attachment"
Assert-True ($foldDetector -match "!nativeAttached && Time\.unscaledTime >= nextNativeAttachTime") "Android bridge retry is throttled and automatic"
Assert-True ($foldDetector -match "forceNotify: false") "native duplicate layouts do not notify ResponsiveUI again"
Assert-True ($foldDetector -match "layoutState\.ContentEquals\(lastPublishedState\)") "native duplicate detection compares complete layout content"
Assert-True ($foldDetector -match "SetEditorDeviceKind") "detector supports a transient Simulator DeviceKind"
Assert-True ($foldDetector -match "ClearEditorDeviceKind") "detector restores its authored player DeviceKind"
Assert-True ($foldDetector -match "hasEditorDeviceKind \? editorDeviceKind") "detector resolves Simulator kind without overwriting serialized player configuration"
Assert-Match $foldDetector "#if UNITY_ANDROID && !UNITY_EDITOR\s+void Update\(\)" "posture polling Update only compiles for Android players"
Assert-True ($foldDetector -match "nativeListenerMayBeRegistered") "partial Android listener attachment is detached safely"
Assert-True ($foldDetector -match "GetInvocationList") "detector event subscribers receive isolated snapshots"
Assert-True ($foldDetector -notmatch "FoldableModeManager") "FoldablePostureDetector has no ModeManager refs"
$foldSource = Get-Content (Join-Path $pkg "FoldAndFlip\Runtime\FoldablePostureSource.cs") -Raw
Assert-True (Test-Path (Join-Path $pkg "FoldAndFlip\Runtime\FoldablePostureSource.cs")) "FoldablePostureSource exists"
Assert-True ($foldSource -match "FoldablePostureDetector\.FindActiveInstance") "FoldablePostureSource uses detector"
Assert-True ($foldSource -notmatch "FoldableModeManager") "FoldablePostureSource has no manager fallback"
Assert-True ($foldSource -match "ApplySimulatedLayout") "FoldablePostureSource ApplySimulatedLayout"
Assert-True ($foldSource -match "SyncSimulatedLayoutState") "FoldablePostureSource SyncSimulatedLayoutState"
Assert-True ($foldSource -match "ActiveInstanceChanged -= OnOwnerLifecycle") "FoldablePostureSource releases detector lifecycle hook when unused"
Assert-True ($foldSource -match "GetInvocationList") "FoldablePostureSource isolates snapshots between subscribers"
$foldProxy = Get-Content (Join-Path $pkg "FoldAndFlip\Runtime\FoldableProxy.cs") -Raw
Assert-True ($foldProxy -match "ParseCallbackPayload") "FoldableProxy parses the Java payload in a pure, testable method"
Assert-True ($foldProxy -match "parts\.Length != PayloadFieldCount") "FoldableProxy rejects payloads with the wrong field count"
Assert-True ($foldProxy -notmatch "Mathf\.") "FoldableProxy callback avoids Unity API calls from a Java background thread"
$foldWatcher = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldableEditorSessionWatcher.cs") -Raw
Assert-True ($foldWatcher -match "FoldablePostureDetector\.EditorInitializing \+= InitializeDetector") "new detector adopts the bound Simulator profile kind before ownership notification"
Assert-True ($foldWatcher -match "FoldablePostureSource\.SetEditorDeviceKind\(FoldableEditorSession\.DeviceKind\)") "Simulator profile kind syncs transiently into the detector"
Assert-True ($foldWatcher -match "FoldableEditorSession\.ScreenSynced \+= SyncDetectorLayout") "stable Simulator screen syncs the complete detector layout"
Assert-True ($foldWatcher -match "invalidSelectionChecksRemaining = 3") "invalid Simulator selection uses a bounded debounce"
$simPlugin = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldableDeviceSimulatorPlugin.cs") -Raw
$profilePluginPath = Join-Path $pkg "FoldAndFlip\Editor\FoldableDeviceProfileNavigationPlugin.cs"
Assert-True (Test-Path $profilePluginPath) "separate Device Profiles Simulator panel exists"
$profilePlugin = Get-Content $profilePluginPath -Raw
Assert-True ($simPlugin -notmatch "SelectAdjacentProfile|◀ Previous|Next ▶") "Fold panel does not contain profile navigation"
Assert-True ($profilePlugin -match 'title => "Device Profiles"') "profile navigation uses its own Device Simulator group"
Assert-True ($profilePlugin -match "SelectAdjacentProfile\(-1\)") "Device Profiles panel exposes Previous"
Assert-True ($profilePlugin -match "SelectAdjacentProfile\(1\)") "Device Profiles panel exposes Next"
Assert-True ($profilePlugin -match "currentProfileIndex = index;") "Device Profiles updates navigation index immediately"
Assert-True ($profilePlugin -match "ResolveProfileIndex") "Device Profiles reconciles index from settled selection"
Assert-True ($profilePlugin -match "CreateMutedLabel\([^\r\n]+minHeight: 36\)") "Device Profiles reserves two lines for profile name"
Assert-True ($profilePlugin -match "pendingProfileName") "Device Profiles resists stale selection events after navigation"
Assert-True ($profilePlugin -match "previousProfileName") "Device Profiles accepts a superseding manual selection"
$simApply = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldableSimulatorApply.cs") -Raw
Assert-True ($simPlugin -match "FoldableSimulatorApply\.Apply") "Device Simulator panel applies through the shared Simulator apply"
Assert-True ($simApply -match "FoldablePostureSource\.ApplySimulatedLayout") "Device Simulator Apply uses FoldablePostureSource"
Assert-True ($simApply -match "FoldablePostureSource\.HasActiveOwner\(\)") "Device Simulator accepts detector"
Assert-True ($simApply -match "No active FoldablePostureDetector in loaded scenes") "Device Simulator dialog requires an active detector"
Assert-True ($simPlugin -notmatch "FoldableModeManager") "Device Simulator has no ModeManager path"
$projRoot = Resolve-Path (Join-Path $pkg "..\..")
# Host-project integration checks. These read the consuming project's own scenes and prefabs, so
# they only run where those assets exist; elsewhere they are skipped (see Skip-Host).
$loginScene = Join-Path $projRoot "Assets\_Scenes\Login.unity"
if (Test-Path -LiteralPath $loginScene) {
    $loginYaml = Get-Content -LiteralPath $loginScene -Raw
    Assert-True ($loginYaml -match "248dd1307748f4c44b27d8ddfdda6a48") "boot scene hosts FoldablePostureDetector script guid"
    Assert-True ($loginYaml -match "deviceKind: 1") "boot scene FoldablePostureDetector is configured as FoldablePhone"
    Assert-True ($loginYaml -match "persistAcrossScenes: 1") "boot scene FoldablePostureDetector persists across scenes"
    Assert-True ($loginYaml -notmatch "889fc0c2a4d10e3459afbef060f1f30b") "boot scene no longer hosts FoldableModeManager script guid"
    Assert-True ($loginYaml -match "(?m)^\s+foldCoverHalfOpenedPortrait:\s*\r?\n\s+enabled: 1") "boot scene Fold Cover HalfOpened Portrait layout slots are enabled"
} else {
    Skip-Host "host boot scene Assets\_Scenes\Login.unity absent - detector wiring checks skipped"
}
$hostScenes = @("Login.unity", "Main.unity", "8BallPool.unity")
foreach ($sceneName in $hostScenes) {
    $scenePath = Join-Path $projRoot "Assets\_Scenes\$sceneName"
    if (-not (Test-Path -LiteralPath $scenePath)) {
        Skip-Host "host scene Assets\_Scenes\$sceneName absent - ResponsiveUI integration checks skipped"
        continue
    }

    $sceneYaml = Get-Content -LiteralPath $scenePath -Raw
    Assert-True ($sceneYaml -notmatch "889fc0c2a4d10e3459afbef060f1f30b") "$sceneName has no removed FoldableModeManager"
    Assert-True ($sceneYaml -match "c8f4e2a91b3d4f6e8a7c5d9e1f0b2a34") "$sceneName preserves a ResponsiveUI hub"
    Assert-True ($sceneYaml -match "(?m)^\s+foldCoverHalfOpenedPortrait:\s*\r?\n\s+enabled: 1") "$sceneName has authored Fold Cover HalfOpened Portrait slots"
    Assert-True ($sceneYaml -match "(?m)^\s+foldCoverHalfOpenedLandscape:\s*\r?\n\s+enabled: 1") "$sceneName has authored Fold Cover HalfOpened Landscape slots"
}
# Slot data: every slot mapping in the host project's scenes / prefabs holds exactly the twenty
# StatePropertyNames keys (no removed names such as phoneCover* / iphoneDuoHalfOpened*), and the
# Wide Screen / iPhone Duo slots keep the unset Design Canvas Size (their form default applies).
# The rule is worth checking in any project that has such assets, so whichever of the list exists
# is validated and the rest are skipped.
$slotForms = @("phone", "pad", "foldPhoneCover", "foldHalfOpened", "foldCoverHalfOpened",
    "flipPhoneCover", "flipHalfOpened", "iPhoneDuoCover", "iPhoneDuoSeated", "wideScreen")
$expectedSlotKeys = ($slotForms | ForEach-Object { "$($_)Portrait"; "$($_)Landscape" } |
    Sort-Object -CaseSensitive) -join ","
$slotKeyPattern = '^( *)((?:phone|pad|fold|flip|iphone|iPhone|wideScreen)\w*(?:Portrait|Landscape)):'
$slotDataAssets = @(
    "Assets\_Scenes\Main.unity",
    "Assets\_Scenes\8BallPool.unity",
    "Assets\_Scenes\Login.unity",
    "Assets\_Resources\Prefab\UI\Responsive Sub Ui.prefab",
    "Assets\_Resources\Prefab\PowerUp\Play Popup UI.prefab",
    "Assets\_Custom Packages\GUI\Loading Screen Studio\Resources\Loading Screens\PP_LoadMatch.prefab",
    "Assets\_Custom Packages\GUI\Loading Screen Studio\Resources\Loading Screens\PP_LoadDashboard.prefab"
)
foreach ($slotAsset in $slotDataAssets) {
    $slotAssetPath = Join-Path $projRoot $slotAsset
    if (-not (Test-Path -LiteralPath $slotAssetPath)) {
        Skip-Host "host asset $slotAsset absent - slot data checks skipped"
        continue
    }

    $openSlotGroups = @{}
    $slotGroups = New-Object System.Collections.Generic.List[object]
    $authoredCanvas = New-Object System.Collections.Generic.List[string]
    $canvasIndent = -1
    $canvasSlot = ""
    $lineNumber = 0
    foreach ($line in ((Get-Content -LiteralPath $slotAssetPath -Raw) -split "\r?\n")) {
        $lineNumber++
        $trimmed = $line.TrimStart(' ')
        if ($trimmed.Length -eq 0) {
            continue
        }

        # A shallower line closes the slot mappings opened deeper.
        $indent = $line.Length - $trimmed.Length
        foreach ($openIndent in @($openSlotGroups.Keys)) {
            if ($openIndent -gt $indent) {
                $slotGroups.Add($openSlotGroups[$openIndent])
                $openSlotGroups.Remove($openIndent)
            }
        }
        if ($canvasIndent -ge 0 -and $indent -le $canvasIndent) {
            $canvasIndent = -1
        }
        if ($canvasIndent -ge 0 -and $indent -eq $canvasIndent + 2 -and
            $trimmed -match '^designCanvasSize: \{x: ([^,]+), y: ([^}]+)\}' -and
            ([double]$Matches[1] -ne 0 -or [double]$Matches[2] -ne 0)) {
            $authoredCanvas.Add("$canvasSlot (line $lineNumber)")
        }
        if ($line -cmatch $slotKeyPattern) {
            $slotKey = $Matches[2]
            if (-not $openSlotGroups.ContainsKey($indent)) {
                $openSlotGroups[$indent] = [pscustomobject]@{
                    Line = $lineNumber
                    Names = (New-Object System.Collections.Generic.List[string])
                }
            }
            $openSlotGroups[$indent].Names.Add($slotKey)
            if ($slotKey -cmatch '^(wideScreen|iPhoneDuo)' -and $trimmed -match ':\s*$') {
                $canvasIndent = $indent
                $canvasSlot = $slotKey
            }
        }
    }
    foreach ($openGroup in $openSlotGroups.Values) {
        $slotGroups.Add($openGroup)
    }

    $badSlotGroups = @($slotGroups | Where-Object {
        ((@($_.Names) | Sort-Object -CaseSensitive) -join ",") -cne $expectedSlotKeys
    })
    $badSlotLines = ($badSlotGroups | Select-Object -First 3 | ForEach-Object { $_.Line }) -join ", "
    Assert-True ($slotGroups.Count -gt 0) "$slotAsset has slot data"
    Assert-True ($badSlotGroups.Count -eq 0) `
        "$slotAsset slot mappings hold exactly the twenty slot keys ($($slotGroups.Count) mappings; bad at lines: $badSlotLines)"
    $authoredCanvasList = ($authoredCanvas | Select-Object -First 3) -join ", "
    Assert-True ($authoredCanvas.Count -eq 0) `
        "$slotAsset Wide Screen / iPhone Duo slots keep the unset Design Canvas Size ($authoredCanvasList)"
}

$handler = Get-Content (Join-Path $runtime "ResponsiveUiHandler.cs") -Raw
Assert-True ($handler -match "IsAuthoringSelectedWidgetTarget") "authoring guard exists"
Assert-True ($handler -notmatch "GetCanvasScalerReference|GetCanvasScalerMatch") "removed test-only CanvasScaler helpers stay absent"
Assert-True ($handler -match "EditorFormFactor =>\s*ResponsivePlatforms\.ConstrainDetected\(editorFormFactor\)") "Override In Editor form is constrained to the build target"
Assert-Match $handler 'GetEditorCaptureLayout\(\)[\s\S]{0,200}new ResponsiveDevice\.LayoutKey\(EditorFormFactor, editorOrientation\)' "Editor capture targets the constrained override form"
Assert-Match $handler 'void ResolveCurrent\(\)[\s\S]{0,200}CurrentFormFactor = EditorFormFactor;' "Override In Editor applies the constrained form"
Assert-True ($handler -match "usePadRefs \? padReferenceResolution") "ApplyScaler uses authored Phone/Pad refs"
Assert-True ($handler -notmatch "phoneReferenceResolution = ResponsiveRectState.GetDefaultDesignCanvasSize") "ApplyScaler does not overwrite authored Phone scaler refs"
Assert-True ($handler -match "GetSceneLayoutSize") "GetSceneLayoutSize exists"
Assert-Match $handler 'void LateUpdate\(\)[\s\S]{0,300}RefreshLayoutIfWatchSizeChanged\(\);[\s\S]{0,120}_layoutRefreshRequested' "screen/orientation detection runs before queued geometry refresh"
Assert-Match $handler 'notifyAllLayoutClients = _layoutRefreshRequested[\s\S]{0,900}ApplySceneLayout\([\s\S]{0,100}notifyAllLayoutClients: true' "size changes preserve queued safe-area notify-all"
Assert-True ($handler -match "ResolveLiveLayout") "handler uses shared ResolveLiveLayout"
Assert-True ($handler -match "OnApplicationFocus") "handler re-applies on focus (device + Editor Play)"
Assert-True ($handler -notmatch "void OnApplicationPause") "resume re-applies once (focus covers iOS / Android resume)"
Assert-True ($handler -match "_hasEditorSimulatorLayout") "handler tracks Simulator-selected layout"
Assert-Match $handler 'RefreshEditorLayoutNow[\s\S]{0,700}Apply\(\)' "Simulator selection forces layout Apply"
Assert-True ($handler -match "NotifyDisplayChanged") "handler invalidates display cache on change"
Assert-True ($handler -notmatch "DetectEditorFormFactor") "former DetectEditorFormFactor removed"
Assert-True ($handler -notmatch "EditorApplication\.update|EditorPollWatchSize|BindEditorLayoutPolling") "handler has no edit-mode update poll"
Assert-True ($handler -match "GetWatchSize") "handler exposes GetWatchSize"
Assert-True ($handler -match "GetWatchScreenSize") "handler watch size uses simulated device pixels"
Assert-True ($handler -notmatch "GetMainGameViewSize") "handler avoids Game View size reads"
Assert-True ($handler -notmatch "ResponsiveDevice\.SetFormOverride|ResponsiveDevice\.SetOrientationOverride") "handler keeps layout state instance-local"
Assert-True ($handler -match "namespace FDPlus\.ResponsiveUI") "handler lives in FDPlus.ResponsiveUI"
Assert-True ($handler -match "ShouldForceCanvasUpdate") "Apply can skip ForceUpdateCanvases when layout is unchanged"
Assert-True ($handler -match "FoldablePostureSource\.ActiveInstanceChanged") "handler reacts to posture source lifecycle events"
Assert-True ($handler -match "FoldablePostureSource\.LayoutChanged") "handler listens via FoldablePostureSource"
Assert-True ($handler -notmatch "FoldableModeManager") "handler has no FoldableModeManager binds"
Assert-True ($handler -match "layoutEvents\.Invoke\(CurrentLayout\)") "Play Mode Apply fires the exact layout slot event"
Assert-Match $handler 'void InvokeLayoutEventsIfChanged\(\)[\s\S]{0,120}if \(!Application\.isPlaying \|\| !_started\)[\s\S]{0,120}_lastEventLayout\.Equals\(CurrentLayout\)' "layout events fire in Play Mode after Start, and only when the layout changed"
Assert-True ($handler -notmatch "onPhone|onPad|CurrentType") "removed onPhone / onPad events stay absent"
Assert-True ($handler -match "ResponsiveLayoutEvents layoutEvents") "handler serializes per-slot layout events"
Assert-True ($handler -notmatch "\(this as ResponsiveUI hub\)|\(this is ResponsiveUI hub\)") "handler does not cast to its subclass"
Assert-True ($handler -match "protected virtual void NotifyLayoutClients") "hub ApplySceneLayout notifies clients through a virtual"
Assert-Match $handler 'EnterApplyInFlight\(\);[\s\S]{0,400}BeginFormTransitionLoadingIfNeeded[\s\S]{0,2500}ApplySceneLayout\(\);[\s\S]{0,300}KeepFormTransitionLoadingVisible[\s\S]{0,300}EndFormTransitionLoading' "full Apply shows form-transition loading under ApplyInFlight around scene layout"
Assert-True ($handler -match "_applyInFlightDepth") "ApplyInFlight uses a nesting depth so full Apply + scene layout stay guarded"
Assert-Match $handler 'void EnterApplyInFlight\(\) => _applyInFlightDepth\+\+' "ApplyInFlight enter increments depth"
Assert-Match $handler 'if \(layoutOnly\)[\s\S]{0,40}ApplyLayout\(\)' "full Apply does not cover-fit backgrounds twice"
Assert-Match $handler 'NotifyLayoutClients\(layoutOnly, notifyAllLayoutClients\);[\s\S]{0,80}if \(layoutOnly\)[\s\S]{0,40}ApplyLayout\(\)' "layout-only cover runs after widget Target hide/show"
Assert-Match $handler 'RefreshEditorLayoutAfterCanvas[\s\S]{0,1400}notifyAllLayoutClients: true' "canvas-ready pass notifies all hub clients after Simulator settle"
Assert-True ($handler -notmatch "ISerializationCallbackReceiver") "handler no longer migrates Active Objects"
Assert-True ($handler -notmatch "ResponsiveActiveObjects\.Apply") "Active Objects Apply moved off the hub"
Assert-True ($handler -notmatch "phoneOnlyObjects") "legacy phone-only list moved off the handler"
Assert-True ($handler -notmatch "SetActiveGroup") "former phone/pad SetActiveGroup removed"

$client = Get-Content (Join-Path $runtime "IResponsiveUiClient.cs") -Raw
Assert-True ($client -match "void ApplyFromHub") "clients apply from the Canvas hub"
Assert-True ($client -match "class ResponsiveUiHub") "hub resolve helper exists"
Assert-True ($client -match "GetComponentInParent<Canvas>\(true\)") "hub resolve uses the parent Canvas"
Assert-True ($client -match "GetComponentInParent<ResponsiveUI>\(true\)") "hub resolve includes a disabled ancestor hub"
Assert-True ($client -match "BindClient") "clients share one hub bind path"
Assert-True ($client -match "RebindIfChanged") "clients can rebind when the assigned hub changes while enabled"
Assert-True ($client -match "ApplyNow") "clients share one hub ApplyNow path"
Assert-True ($client -match "hub == null \|\| !hub\.enabled") "shared ApplyNow does not apply from a disabled hub"

$controller = Get-Content (Join-Path $runtime "ResponsiveUiController.cs") -Raw
Assert-True ($controller -match "class ResponsiveUiController\s*:\s*MonoBehaviour,\s*IResponsiveUiClient") "controller subscribes to the Canvas hub"
Assert-True ($controller -match "ResponsiveWidgetLayout\[\] widgets") "controller owns widget slots"
Assert-True ($controller -match "ResponsiveBackground\[\] backgrounds") "controller owns backgrounds"
Assert-True ($controller -match "ResponsiveActiveObjectEntry\[\] activeObjects") "controller owns Active Objects"
Assert-True ($controller -match "EditorApplyBackgrounds") "controller Inspector can live-apply Match Mode"
Assert-True ($controller -notmatch "ApplyTargetActive") "controller does not SetActive widget Targets from slot Enabled"
Assert-True ($controller -notmatch "\.SetActive\(") "controller Apply does not SetActive widget Targets"
Assert-True ($controller -notmatch "ExactSlotEnabled") "controller size reapply uses the resolved fallback slot"
Assert-Match $controller 'void ApplyFromHub[\s\S]{0,1500}ApplyWidgets\(hub, layout\);[\s\S]{0,400}ApplyBackgrounds\(hub\)' "direct full Apply shows widget Targets before Match Mode cover"
Assert-Match $controller 'void EditorApplyWidgets[\s\S]{0,220}!isActiveAndEnabled' "Inspector widget preview requires an enabled controller"
Assert-True ($controller -match "!isActiveAndEnabled \|\| !hub\.enabled") "controller rejects notifications from a disabled hub"
Assert-True ($controller -match "hub == null \|\| !hub\.enabled") "controller background LivePush rejects a disabled hub"
Assert-Match $controller 'Application\.isPlaying && !isActiveAndEnabled' "Edit Mode cover applies on inactive panels"
Assert-True ($controller -match "ApplyBackgrounds") "controller retries cover after ForceUpdateCanvases when parent size is 0"
Assert-True ($controller -match "scaleFromDesignCanvas") "controller size reapply is the active slot's Design Scale only (cover uses hub ApplyLayout)"
Assert-True ($controller -match "RequiresDesignScaleSizeReapply") "controller skips redundant size re-Apply under a root CanvasScaler, not nested panes"
Assert-True ($controller -match "HierarchyDepth") "ApplyWidgets sorts parents before children"
Assert-NotMatch $controller 'backgrounds != null && backgrounds.Length > 0[\s\S]{0,80}return true' "backgrounds alone do not force a widget client size reapply"
Assert-True ($controller -match "ResponsiveUiHub.BindClient") "controller uses shared hub bind"
Assert-True ($controller -match "RebindIfChanged") "controller rebinds when Hub is reassigned while enabled"
Assert-True ($controller -match "_seedDefaultLayout = true") "controller seeds one widget only on Add Component / Reset"
Assert-True ($controller -match "UnityEngine\.Object\[\]") "Selection.objects uses UnityEngine.Object[]"
Assert-True ($controller -notmatch "EditorTakeLists") "removed hub-list migration intake stays absent"
Assert-True ($controller -match "ResponsiveUiHub\.BeginApplyFromHub") "controller uses the shared ApplyFromHub opening"

$scrollView = Get-Content (Join-Path $runtime "ResponsiveScrollView.cs") -Raw
Assert-True ($scrollView -match "class ResponsiveScrollView\s*:\s*MonoBehaviour,\s*IResponsiveUiClient") "scroll view subscribes to the Canvas hub"
Assert-True ($scrollView -match "ResponsiveScrollLayout scrollMetrics") "scroll view owns scroll slots"
Assert-True ($scrollView -match "EditorApplyTarget") "scroll exposes its callback owner for editor Undo"
Assert-True ($scrollView -match "EditorBeginApply") "scroll hub Apply notifies editor sync to suppress slot recapture"
Assert-True ($scrollView -match "EditorPushHost") "scroll hub Apply writes host metrics through editor sync"
Assert-True ($scrollView -match "EditorInvokeApply") "scroll slot LivePush can Relayout without a second host SerializedObject write"
Assert-True ($scrollView -match "PushHostThenApply") "scroll Edit Mode Apply writes host SerializedObject before Bind"
Assert-True ($scrollView -match "_hub\.CurrentLayout") "scroll apply uses the bound hub layout when stored layout is missing"
Assert-True ($scrollView -match "_hubLayout") "scroll apply stores the hub layout argument"
Assert-True ($scrollView -match "RebindIfChanged") "scroll rebinds when Hub is reassigned while enabled"
Assert-True ($scrollView -match "ResponsiveScrollDriver") "scroll view falls back to the driver without a hub"
Assert-True ($scrollView -match "ResponsiveUiHub.BindClient") "scroll view uses shared hub bind"
Assert-True ($scrollView -match "public bool ApplyChanged") "scroll host can skip unchanged metric applications"
Assert-True ($scrollView -match "ResolveCurrentLayout") "scroll apply paths share orientation and product-state resolution"
Assert-True ($scrollView -match "SizeChangeRequiresReapply\(\) => true") "scroll Relayouts on same-form size ticks when the viewport may have changed"
Assert-True ($scrollView -match "!hub\.enabled") "scroll apply waits for an enabled hub"
Assert-Match $scrollView '_hubLayout = layout;\s*_hasHubLayout = true;[\s\S]*?if \(layoutOnly && !layoutChanged\)\s*\{\s*InvokeApply\(\);\s*return;' "scroll adopts the hub layout on every pass; unchanged layout-only passes only Relayout"
Assert-True ($scrollView -match "void ApplyUnboundHost\(\)") "scroll writes the sibling FancyScrollView when no callback is bound"
Assert-True ($scrollView -match "_unboundUpdatePosition") "scroll unbound Relayout falls back to UpdatePosition when Relayout is stripped"
Assert-True ($scrollView -match "HostMembersByType") "scroll caches reflected host members per type"
Assert-Match $scrollView 'catch \(Exception e\)\s*\{\s*Debug\.LogException\(e, this\);' "scroll host exceptions are logged and do not abort the hub pass"
$hubRuntime = Get-Content (Join-Path $runtime "ResponsiveUI.cs") -Raw
Assert-Match $hubRuntime 'client\.ApplyFromHub\(this, layout, layoutOnly\);\s*\}[\s\S]{0,200}catch \(Exception e\) when \(!\(e is ExitGUIException\)\)' "hub isolates each client ApplyFromHub (ExitGUIException still unwinds)"
$handlerRuntime = Get-Content (Join-Path $runtime "ResponsiveUiHandler.cs") -Raw
Assert-Match $handlerRuntime 'ResolveCurrent\(\);\s*if \(!CurrentLayout\.Equals\(previous\)\)\s*\{\s*Apply\(\);' "Edit Mode canvas-settle pass re-detects and runs a full Apply when the layout changed"
$widgetSyncSrc = Get-Content (EditorPath "ResponsiveUiWidgetSync.cs") -Raw
Assert-True ($widgetSyncSrc -match "owner\.isActiveAndEnabled && hub != null && hub\.isActiveAndEnabled") "widget capture skips owners / hubs that missed the Simulator switch"
$cameraSyncSrc = Get-Content (EditorPath "ResponsiveCameraSync.cs") -Raw
Assert-True ($cameraSyncSrc -match "static bool FollowsHubLayout\(ResponsiveCamera owner\)") "camera capture skips cameras / hubs that missed the Simulator switch"
$slotFocusSrc = Get-Content (EditorPath "ResponsiveUiSimulatorSlotFocus.cs") -Raw
Assert-Match $slotFocusSrc 'public static void RefreshNow\(\)[\s\S]{0,1600}_hasPushedApply = false;\s*RefreshHandlersIfNeeded\(\);' "settings RefreshNow forces a hub Apply even when the Simulator identity is unchanged"
Assert-Match $slotFocusSrc 'public static void RefreshNow\(\)\s*\{\s*int appliesBefore = _handlerRefreshCount;' "settings RefreshNow does not Apply twice when the Simulator also changed"
$detectionSrc = Get-Content (EditorPath "ResponsiveUiDetectionDrawer.cs") -Raw
Assert-True ($detectionSrc -match "EditorHideOtherSlots" -and $detectionSrc -match "EditorOverrideInEditor") "client Inspectors read hub flags without a SerializedObject per slot draw"
$bannerRuntime = Get-Content (Join-Path $runtime "ResponsiveTextBanner.cs") -Raw
Assert-Match $bannerRuntime 'void RestoreSlot\(\)[\s\S]{0,900}if \(!_hasSlot \|\| !RectStillOwned\(\)\)\s*\{\s*AdoptLiveSlot\(\);' "banner restore undoes only its own writes; a controller-rewritten rect is kept and adopted"
Assert-True ($bannerRuntime -match "RememberBannerWrite\(\);") "banner records what it writes (Alignment pivot / fitted width)"
Assert-True ($bannerRuntime -match "value == _visibleText") "banner skips re-measuring TMP when the same message is re-shown"
$twoSideRuntime = Get-Content (Join-Path $pkg "Runtime\ResponsiveTwoSideUiTweenController.cs") -Raw
Assert-True ($twoSideRuntime -match "ApplyFinishedStaleWidget") "two-side re-applies a widget whose tween outlived a skipped layout-only pass"
Assert-True ($scrollView -notmatch "\.SetActive\(") "scroll slot Enabled does not SetActive the host"

$banner = Get-Content (Join-Path $runtime "ResponsiveTextBanner.cs") -Raw
Assert-True ($banner -match "class ResponsiveTextBanner\s*:\s*MonoBehaviour,\s*IResponsiveUiClient") "text banner subscribes to the Canvas hub"
Assert-True ($banner -match "DefaultExecutionOrder\(1000\)") "text banner recaptures after default-order widget Apply"
Assert-True ($banner -match "ResponsiveUiHub.BindClient") "text banner uses shared hub bind"
Assert-True ($banner -match "ResponsiveUiHub\.BeginApplyFromHub") "text banner rebinds when Hub is reassigned while enabled"
Assert-True ($banner -notmatch "QueueEditorFit|_editorFitQueued") "text banner has no unreachable Edit Mode fit queue"
Assert-True ($banner -match "!isActiveAndEnabled \|\| !hub\.enabled") "text banner rejects notifications from a disabled hub"
Assert-True ($banner -match "SizeChangeRequiresReapply\(\) => _visible") "text banner Play Mode size ticks recapture only while visible"
Assert-True ($banner -match 'public void Show\(') "text banner exposes Show"
Assert-True ($banner -match 'public void Hide\(') "text banner exposes Hide"
Assert-True ($banner -match 'public void SetPreviewLock\(') "text banner exposes preview lock"
Assert-True ($banner -match "public static float CalculateWidth") "text banner width math is testable"
Assert-True ($banner -match "enum Align") "text banner uses Left Up / Right Middle alignment names"
Assert-True ($banner -match "Align alignment = Align.LeftMiddle") "text banner defaults to Left Middle"
Assert-True ($banner -match "OnTransformParentChanged") "text banner rebinds hub after Instantiate then SetParent"
Assert-True ($banner -match "LeftMiddle") "text banner has Left Middle"
Assert-True ($banner -match "CenterMiddle") "text banner has Center Middle"
Assert-True ($banner -match "RightMiddle") "text banner has Right Middle"
Assert-True ($banner -notmatch "LeftCenter") "text banner does not use LeftCenter"
Assert-True ($banner -notmatch "RightCenter") "text banner does not use RightCenter"
Assert-True ($banner -notmatch "CenterCenter") "text banner does not use CenterCenter"
Assert-True ($banner -match "public static Vector2 AlignmentAnchor") "text banner alignment anchors are testable"
Assert-True ($banner -match "public static Vector2 CalculateAlignedPosition") "text banner aligned position is testable"
Assert-True ($banner -match "banner.pivot = alignPivot") "text banner writes Alignment to RectTransform.pivot"
Assert-True ($banner -match "PivotMatchesAlignment") "text banner skips rewriting position when pivot already matches Alignment"
Assert-Match $banner '!_pendingHubCapture && !_visible' "text banner recaptures the slot only on the first Show"
Assert-True ($banner -match "public static Vector2 ConvertAnchoredPositionForPivot") "text banner pivot conversion is testable"
Assert-True ($banner -notmatch "HorizontalPin") "text banner does not use a custom HorizontalPin enum"
Assert-True ($banner -match "if \(_pendingHubCapture\)") "text banner defers capture while a hub Apply is pending"
Assert-True ($banner -match "SetBannerDisplayed") "text banner hide does not disable a self-hosted component"
Assert-True ($banner -match "EditorIgnoreSlotCapture") "text banner skips widget slot capture while fitting"
Assert-True ($banner -match "RectStillOwned") "text banner does not sample a fitted rect as the widget slot"
Assert-True ($banner -match "TextWrappingModes\.NoWrap") "text banner measures TMP without wrapping"

$runtimeAsmdef = Get-Content (Join-Path $runtime "com.futuredigitalplus.responsiveui.asmdef") -Raw
Assert-True ($runtimeAsmdef -match "Unity\.TextMeshPro") "runtime asmdef references TextMeshPro"

$assets = Split-Path $pkg -Parent
$gameScrollPath = Join-Path $assets "_Resources\Script\Play Mode\Main\ScrollView.cs"
if (Test-Path $gameScrollPath) {
    $gameScroll = Get-Content $gameScrollPath -Raw
    Assert-True ($gameScroll -match "responsiveScroll\.ApplyChanged") "gameplay scroll still applies resolved metrics via ApplyChanged"
    Assert-Match $gameScroll 'ApplyChanged\(ref cellInterval, ref scrollOffset\);\s*if \(initialized\)\s*Relayout\(\);' "gameplay scroll Relayouts after apply even when metrics are unchanged"
    Assert-True ($gameScroll -match "QueueEditorCellCleanup") "gameplay scroll queues cleanup for legacy Edit Mode clones"
    Assert-True ($gameScroll -match "ClearEditorCells") "gameplay scroll invokes FancyScrollView Edit Mode cleanup"
} else {
    Skip-Host "host FancyScrollView consumer _Resources\Script\Play Mode\Main\ScrollView.cs absent - gameplay scroll checks skipped"
}

$fancyScrollCorePath = Join-Path $assets "_Custom Packages\GUI\FancyScrollView-master\Assets\FancyScrollView\Sources\Runtime\Core\FancyScrollView.cs"
if (Test-Path $fancyScrollCorePath) {
    $fancyScrollCore = Get-Content $fancyScrollCorePath -Raw
    $fancyScrollPreviewPath = Join-Path (Split-Path $fancyScrollCorePath) "FancyScrollView.Preview.cs"
    if (Test-Path $fancyScrollPreviewPath) {
        $fancyScrollCore += "`n" + (Get-Content $fancyScrollPreviewPath -Raw)
    }
    Assert-True ($fancyScrollCore -match "void RemoveDestroyedCells\(\)") "project FancyScrollView repairs destroyed pooled cells"
    Assert-True ($fancyScrollCore -match "currentPosition = position;\s+RemoveDestroyedCells\(\);\s+var p") "FancyScrollView repairs its pool before layout calculations"
    Assert-True ($fancyScrollCore -match "if \(pool\[i\] == null\)\s+pool\.RemoveAt\(i\)") "FancyScrollView removes Unity destroyed-object references"
    Assert-True ($fancyScrollCore -match "protected void ClearEditorCells\(\)") "FancyScrollView exposes Edit Mode generated-cell cleanup"
    Assert-True ($fancyScrollCore -match "if \(!Application\.isPlaying\)\s+return;\s+#endif\s+if \(!initialized\)") "FancyScrollView never initializes runtime cells in Edit Mode"
    Assert-True ($fancyScrollCore -match "CellPrefab\.name \+ `"\(Clone\)`"") "FancyScrollView cleans clones serialized by older versions"
    Assert-True ($fancyScrollCore -match "HideFlags.DontSaveInEditor") "FancyScrollView preview cells are not saved"
    Assert-True ($fancyScrollCore -match "ScheduleEditorPreview") "FancyScrollView can preview cells in Edit Mode"
    Assert-True ($fancyScrollCore -match "CollectEditorPreviewItems") "FancyScrollView preview reads the scroll view item list"
} else {
    Skip-Host "host FancyScrollView core absent - pooled-cell repair checks skipped"
}

$activeObjects = Get-Content (Join-Path $runtime "ResponsiveActiveObjects.cs") -Raw
Assert-True ($activeObjects -notmatch "PhoneLike|PadLike|ForPad") "no legacy Phone / Pad Active Objects masks"
Assert-True ($activeObjects -match "public static ResponsiveActiveObjectSlots FormTransitionDefaults") "form-transition default Show On mask factory"
Assert-True ($activeObjects -match "public static void Apply") "Active Objects Apply toggles listed GameObjects"
Assert-True ($activeObjects -notmatch "FromLegacy|AppendLegacy") "no legacy phoneOnlyObjects / padOnlyObjects migration"
foreach ($legacyHost in @("ResponsiveUI.cs", "ResponsiveCamera.cs", "ResponsiveUiController.cs")) {
    $legacyHostSrc = Get-Content (Join-Path $runtime $legacyHost) -Raw
    Assert-True ($legacyHostSrc -notmatch "phoneOnlyObjects|padOnlyObjects|activeObjectsMigrated") "$legacyHost has no legacy Active Objects fields"
}
foreach ($shimHost in @("ResponsiveWidgetLayout.cs", "ResponsiveUiHandler.cs", "ResponsiveCameraState.cs", "ResponsiveTwoSideUiTweenController.cs")) {
    $shimHostSrc = Get-Content (Join-Path $runtime $shimHost) -Raw
    Assert-True ($shimHostSrc -notmatch "FormerlySerializedAs|MovedFrom") "$shimHost has no serialization back-compat shims (no old data)"
}
Assert-True ($activeObjects -match "flipPhoneCoverPortrait") "Active Objects include Flip Cover slots"
Assert-True ($activeObjects -match "foldCoverHalfOpenedPortrait") "Active Objects include Fold Cover Half Opened slots"
Assert-True ($activeObjects -notmatch "flipClosedPortrait") "Flip Cover YAML field is flipPhoneCoverPortrait, not flipClosedPortrait"

$layoutEvents = Get-Content (Join-Path $runtime "ResponsiveLayoutEvents.cs") -Raw
Assert-True ($layoutEvents -match "public void Invoke") "layout events Invoke the exact live slot"
Assert-True ($layoutEvents -match "StatePropertyIndex\(layout\)") "layout events use the same slot index as widgets"
Assert-True ($layoutEvents -match "foldPhoneCoverLandscape") "layout events include Fold Cover slots"
Assert-True ($layoutEvents -match "flipPhoneCoverLandscape") "layout events include Flip Cover slots"
Assert-True ($layoutEvents -match "foldHalfOpenedPortrait") "layout events include Fold HalfOpened slots"
Assert-True ($layoutEvents -match "foldCoverHalfOpenedPortrait") "layout events include Fold Cover Half Opened slots"
Assert-True ($layoutEvents -match "flipHalfOpenedLandscape") "layout events include Flip HalfOpened slots"
Assert-True ($layoutEvents -notmatch "flipClosedPortrait") "Flip Cover YAML field is flipPhoneCoverPortrait, not flipClosedPortrait"
Assert-True ($layoutEvents -notmatch "= new UnityEvent") "layout events have no struct field initializers (C# 9)"

$device = Get-Content (Join-Path $runtime "ResponsiveDevice.cs") -Raw
Assert-True ($device -match "DetectActiveFormFactor") "DetectActiveFormFactor exists"
Assert-True ($device -match "ResolvePlayerHardwareFallback") "player form fallback uses hardware identity"
Assert-Match $device 'IsKnownInnerPadSize\(w, h\)[\s\S]{0,80}return FormFactor\.Pad' "player Expand-before-posture maps known inner Pad WxH"
Assert-Match $device 'IsKnownInnerPadSize\(float width, float height\) =>[\s\S]{0,120}MatchesIphoneDuoSizes' "iPhone Duo inner Pad WxH is skipped on Android players"
Assert-True ($device -match "productState == FoldableProductState.Undefined") "HUD waits for first Jetpack snapshot before mapping Fold Expand"
Assert-True ($device -match "ResolveLiveLayout") "ResolveLiveLayout is the shared host layout entry"
Assert-True ($device -match "SimulatorLayoutChanged") "Simulator pin notifies non-handler hosts"
Assert-True ($device -match "SetSimulatorLayout") "Device Simulator can pin Fold Cover layout"
Assert-True ($device -match "GetWatchScreenSize") "watch size prefers Simulator catalog pixels then Device.Screen"
Assert-True ($device -match "GetSimulatorScreenSize") "Simulator pin can store catalog WxH"
Assert-True ($device -match "UnityEngine\.Device\.Screen") "watch size uses UnityEngine.Device.Screen not Game View widget"
Assert-True ($device -match "_hasSimulatorLayout") "simulator layout pin is tracked"
Assert-True ($device -match "IsKnownFoldCoverPhoneSize") "Fold8 Cover size recognized as FoldPhoneCover without pin"
Assert-True ($device -match "IsKnownFlipCoverPhoneSize") "Flip Cover size recognized as FlipPhoneCover without pin"
Assert-True ($device -match "IsKnownCoverPhoneSize") "shared Cover WxH helper covers Fold and Flip"
Assert-Match $device 'MapKnownWideCoverSize\(w, h, out|MapKnownWideCoverSize\(watch' "Fold8 / iPhone Duo Cover beats stale Pad Simulator pin"
Assert-Match $device 'IsKnownWideCoverPhoneSize\(float width, float height\) =>[\s\S]{0,120}MatchesIphoneDuoSizes' "iPhone Duo Cover WxH is skipped on Android players"
Assert-True ($device -notmatch "IsKnown(Fold|Wide)CoverPhoneSize\(Screen\.width") "Cover size uses watch pixels not Game View Screen"
Assert-Match $device 'CurrentFormFactor[\s\S]{0,900}GetWatchScreenSize' "CurrentFormFactor uses Simulator watch for Cover upgrade"
Assert-Match $device '_hasSimulatorLayout[\s\S]{0,250}MapFromFoldable' "selected Simulator pin beats leftover Fold Opened/Pad mapping"
Assert-Match $device 'Screen\.dpi <= 0f' "Android without DPI defaults Phone not Pad"

$focus = Get-Content (EditorPath "ResponsiveUiSimulatorSlotFocus.cs") -Raw
Assert-True ($focus -match "ConstrainFocusKey\(raw\)") "Simulator focus uses the build target's form (pin stays raw)"
Assert-Match $focus 'void QuietUpdateFocus\([\s\S]{0,400}key = ConstrainFocusKey\(RefineCoverLayout\(' "quiet focus updates use the build target's form"
Assert-True ($focus -match "BindLifecycle") "slot focus rebinds lifecycle hooks after assembly reload"
Assert-True ($focus -notmatch "afterAssemblyReload") "slot focus rebinds from its [InitializeOnLoad] constructor only"
Assert-True ($focus -match "MapKnownWideCoverSize") "Simulator pin forces Fold Cover / iPhone Duo Cover from their Cover WxH"
Assert-True ($focus -match "IsKnownFlipCoverPhoneSize") "Simulator pin forces FlipPhoneCover from Flip Cover WxH"
Assert-True ($focus -match "IsIgnoringStaleFlipCoverSize") "slot focus does not force FlipPhoneCover from leftover Flip Cover chrome"
Assert-True ($device -match "ShouldIgnoreStaleFlipCoverSize") "Editor Flip Opened can ignore leftover Cover WxH"
Assert-True ($device -match "IsIgnoringStaleFlipCoverSize") "stale Flip Cover override is queryable"
Assert-True ($device -match "MapKnownFlipCoverSize") "Flip Cover size maps to Phone while Flip Opened"
Assert-True ($focus -match "GetWatchScreenSize") "Cover override uses Simulator watch not Game View Screen"
Assert-True ($focus -notmatch "IsKnown(Fold|Wide)CoverPhoneSize\(Screen") "slot focus Cover check does not read Game View Screen"
Assert-True ($focus -match "SetSimulatorLayout|SyncSimulatorDeviceLayout") "slot focus publishes Simulator layout to ResponsiveDevice"
Assert-True ($focus -match "OrientedScreenSize") "slot focus falls back to oriented catalog WxH when no Fold session is bound"
Assert-True ($focus -match "GetAuthoritativeScreenSize") "Flip first-bind pins Fold session catalog WxH not lagged Cover chrome"
Assert-True ($focus -match "GetSessionCatalogSize") "slot focus reads FoldableEditorSession metrics when bound"
Assert-True ($focus -match "MetricsWidth") "session catalog pin uses MetricsWidth"
Assert-True ($focus -match "QueuePlayModeHandlerApply|ApplyPlayModeHandlers") "Play Mode reapplies handlers on Fold posture change"
Assert-True ($focus -match "preferLiveSession") "Play Mode ignores stale Edit Mode focus for Cover→FoldPhoneCover"
Assert-True ($focus -match "preferLiveSession:\s*true") "session refresh prefers live Cover over stale Expand focus"
Assert-True ($focus -match "SyncPlayModeFromSimulator") "Play Mode selection updates Simulator layout without Inspector churn"

$layoutResolver = Get-Content (EditorPath "ResponsiveUiSimulatorLayoutResolver.cs") -Raw
Assert-True ($layoutResolver -match "IsFoldableDeviceBound") "layout resolver prefers bound Fold session over aspect"
Assert-True ($layoutResolver -match "InferProductFromScreen|CoverOrOuter") "layout resolver maps Cover screens to Closed/Phone"
Assert-True ($layoutResolver -match "IsKnownFlipCoverPhoneSize") "layout resolver maps Flip Cover WxH to FlipPhoneCover"
Assert-True ($layoutResolver -match "ResolveProductPreferringScreen") "layout resolver honors session Opened/Closed/HalfOpened over Cover WxH"
Assert-True ($layoutResolver -match "IsIgnoringStaleFlipCoverSize") "layout resolver does not map leftover Flip Cover chrome to FlipPhoneCover while Opened"
$simLayoutTests = Get-Content (Join-Path $pkg "Tests\Editor\ResponsiveDeviceSimulatorLayoutTests.cs") -Raw
Assert-True ($simLayoutTests -match "IsToolkitListName_DeletedDualAndGeneric_IsFalse") "Unity tests reject deleted Dual/Generic as toolkit names"
Assert-True ($simLayoutTests -match "MapFoldableProduct_FlipClosed_UsesFlipPhoneCoverSlot") "Unity tests Flip Closed maps to FlipPhoneCover"
Assert-True ($simLayoutTests -match "DetectActiveFormFactor_FlipCoverWithoutSimulator_IsFlipPhoneCover") "Unity tests Flip Cover WxH is FlipPhoneCover without a pin"
Assert-True (([regex]::Matches($simLayoutTests, "DetectActiveFormFactor_FlipCoverWithoutSimulator_IsFlipPhoneCover")).Count -eq 1) "Flip Cover detect test is unique (no duplicate method)"
Assert-True ($simLayoutTests -match "GetWatchScreenSize_PrefersSimulatorPin") "Unity tests Simulator pin watch size is catalog pixels"
Assert-True ($simLayoutTests -match "RefineFlipPhoneCover_FlipCoverSize_UpgradesPhonePadAndStaleFoldCoverPin") "Unity tests Flip Cover size upgrades Phone, Pad, and leftover FoldPhoneCover"
Assert-True ($simLayoutTests -match "CurrentFormFactor_FlipCoverPinSize_IsFlipPhoneCover") "Unity tests Flip Cover pin upgrades CurrentFormFactor"
Assert-True ($simLayoutTests -match "CurrentFormFactor_StaleFoldCoverPin_FlipCoverWatch_IsFlipPhoneCover") "Unity tests leftover Fold Cover pin yields to Flip Cover WxH"
Assert-True ($simLayoutTests -match "LayoutResolver_FlipCoverLandscape_IsFlipPhoneCover") "Unity tests Flip Cover landscape resolves to FlipPhoneCover"
Assert-True ($simLayoutTests -match "LayoutResolver_FlipOpenedSession_CoverWxH_IsPhone") "Unity tests Flip Opened session Cover WxH is Phone not FlipPhoneCover"
Assert-True ($simLayoutTests -match "IgnoreWhenLiveFlipSessionBlocksCoverWxH") "Unity tests skip Cover→FlipPhoneCover asserts when Flip Opened is bound"
Assert-True (([regex]::Matches($simLayoutTests, "LayoutResolver_FlipCoverLandscape_IsFlipPhoneCover")).Count -eq 1) "Flip Cover landscape resolver test is unique"
Assert-True ($simLayoutTests -notmatch "LayoutResolver_FlipCoverLandscape_IsPhone\b") "Flip Cover landscape resolver does not still assert Phone"
Assert-True ($simLayoutTests -match "CurrentFormFactor_CoverPinSize_IsFoldPhoneCover") "Unity tests Cover pin upgrades CurrentFormFactor"
Assert-True ($simLayoutTests -match "CurrentFormFactor_CompactPhonePin_StaysPhone") "Unity tests selected compact Phone stays Phone"
Assert-True ($simLayoutTests -match "CurrentFormFactor_PhoneReferencePin_IsPhone") "Unity tests default Phone profile pin stays Phone"
Assert-True ($simLayoutTests -match "DetectActiveFormFactor_PhonePin_CompactWatch_IsPhone") "Unity tests selected compact Phone pin stays Phone"

$hingeOverlay = Get-Content (EditorPath "ResponsiveUiHingeGuideOverlay.cs") -Raw
Assert-True ($hingeOverlay -match "hideTransition|s_LastDraw") "hinge overlay clears stroke when leaving Half Opened"
Assert-True ($device -match "NotifyDisplayChanged") "NotifyDisplayChanged exists"

$capture = Get-Content (EditorPath "ResponsiveUiWidgetSync.cs") -Raw
Assert-True ($capture -match "ResolveCaptureSlot") "capture resolves slot"
Assert-True ($capture -match "WriteCapturedLive") "capture writes live scale"
Assert-True ($capture -match "ResolveInspectorLayout") "widget Scene capture uses the same focused Simulator layout as LivePush"
Assert-True ($capture -match "Undo\.postprocessModifications") "capture listens to real Undo modifications only"
Assert-True ($capture -match "SuppressCapture|SuppressScope") "capture can suppress LivePush echo"
Assert-True ($capture -match "IgnoreTargetCapture") "capture can ignore LivePush target echo"
Assert-True ($capture -match "IsLayoutPropertyPath|m_AnchoredPosition") "capture filters layout properties"
Assert-True ($capture -match "GetEditorRectTransform") "capture reuses shared RectTransform resolve"
Assert-True ($capture -match "FindTwoSideOwners") "Scene capture supports two-side responsive widgets"
Assert-Match $capture 'IsNullOrEmpty\(propertyPath\)[\s\S]{0,40}return false' "empty property paths are not treated as layout"
Assert-True ($capture -notmatch "EditorApplication\.update") "capture does not idle-poll"
Assert-True ($capture -notmatch "selectionChanged") "capture does not run on selection change"
Assert-True ($capture -match "NotifyWidgetLivePreview") "capture skips validate Apply after write"
Assert-True ($capture -notmatch "BeginExternalPush") "former BeginExternalPush removed"
Assert-True ($capture -match "ResponsiveRectStateEditorUtil\.FindLoadedControllers") "capture reuses shared FindLoadedControllers"

$drawer = Get-Content (EditorPath "ResponsiveWidgetLayoutDrawer.cs") -Raw
Assert-True ($drawer -match "IsActiveEditorSlot") "LivePush only pushes the active slot"
Assert-True ($drawer -match "IgnoreTargetCapture") "LivePush marks target ignore for capture"
Assert-True ($drawer -match "ShouldDrawState") "widget drawer hides inactive slots when Show Other Slots is off"
Assert-True ($drawer -match "ApplyDetectedFoldouts") "widget drawer forces detected form foldouts"
Assert-True ($drawer -match "ResponsiveUiDetectionDrawer\.ApplyDetectedFoldouts") "widget drawer applies foldouts before height and OnGUI"
Assert-True ((Get-Content (EditorPath "ResponsiveUiDetectionDrawer.cs") -Raw) -match "public static void ApplyDetectedFoldouts\(SerializedProperty layoutProp\)") "widget / camera / scroll drawers share one detected-foldout helper"
Assert-True ($drawer -match "DrawPropertyFoldout") "widget Target and slots sit under the item title"
Assert-True ($drawer -notmatch "indentLevel\+\+") "widget item children do not use a second Unity indent"

$cameraDrawer = Get-Content (EditorPath "ResponsiveCameraLayoutDrawer.cs") -Raw
Assert-True ($cameraDrawer -match "UnscaleLiveToDesign") "camera scale-toggle recapture preserves default-profile identity (shared unscale helper)"
Assert-True ($cameraDrawer -match "GetWatchScreenSize") "camera LivePush fallback uses Simulator watch not Game View Screen"
Assert-True ($cameraDrawer -notmatch "Screen\.width") "camera layout drawer does not read Game View Screen"
Assert-True ($cameraDrawer -match "ShouldDrawState") "camera drawer hides inactive slots when Show Other Slots is off"
Assert-True ($cameraDrawer -match "ApplyDetectedFoldouts") "camera drawer forces detected form foldouts"
Assert-True ($cameraDrawer -match "ApplyInspectorTargetChange") "camera Target field uses shared assign/clear path"
Assert-True ($cameraDrawer -match "objectReferenceInstanceIDValue") "clearing a Missing camera Target still resets slots"
Assert-True ($cameraDrawer -match "ResponsiveUiDetectionDrawer\.ApplyDetectedFoldouts") "camera drawer applies foldouts before height and OnGUI"
Assert-True ($cameraDrawer -match "scaleBefore != scaleAfter") "camera UI-scale toggle recaptures instead of jumping the Target"
Assert-True ($cameraDrawer -match "orthoScaleBefore != orthoScaleAfter|projectionScaleBefore != projectionScaleAfter") "camera projection-scale toggle recaptures instead of jumping the Target"
Assert-True ($cameraDrawer -match "modeChanged") "camera Scale Mode change recaptures instead of jumping the Target"
Assert-True ($cameraDrawer -match "RecaptureLiveForScaleToggle") "camera UI-scale toggle preserves live position and ortho"
Assert-True ($cameraDrawer -match "scalePosition \|\|") "camera Scale From UI Size recapture stores Local Position"
Assert-True ($cameraDrawer -match "ResponsiveCameraEditorUtil\.UnscaleLiveToDesign") "camera UI-scale recapture shares the design-unscale helper"
Assert-True ($cameraDrawer -notmatch '"designUiSize"') "camera UI-scale recapture does not touch the legacy Design UI Size field"
Assert-True ($cameraDrawer -match "ApplyFocus") "camera drawer LivePush applies focus"
Assert-True ($cameraDrawer -match "SameAuthoring") "camera LivePush ignores foldout-only GUI.changed"
Assert-True ($cameraDrawer -match "IsActiveEditorSlot") "camera LivePush only pushes the active slot"
Assert-Match $cameraDrawer 'boundsScaleChanged[\s\S]{0,400}IsActiveEditorSlot' "Bounds Layout Scale toggle recaptures the active slot only"
Assert-True ($cameraDrawer -match "skipIfMatches: false") "Bounds Layout Scale toggle recaptures like widget Design Scale toggle"
Assert-True ($cameraDrawer -match "EditorApplyCameras") "camera LivePush applies the production camera and background path"
Assert-True ($cameraDrawer -match "PreviewCameras") "camera LivePush previews through EditorApplyCameras"
Assert-Match $cameraDrawer 'EditorApplication\.delayCall \+= \(\) =>[\s\S]{0,500}EditorApplyCameras' "camera LivePush applies after Inspector slot fields flush"
Assert-True ($cameraDrawer -match "hasModifiedProperties") "camera LivePush still runs when nested drawers swallow EndChangeCheck"
Assert-True ($cameraDrawer -notmatch "!hub\.enabled") "camera LivePush does not require a hub for Target preview"
Assert-True ($cameraDrawer -notmatch "Focus Bounds") "camera row drawer does not draw Focus Bounds"
Assert-True ($cameraDrawer -match "DrawPropertyFoldout") "camera Target and slots sit under the item title"
Assert-True ($cameraDrawer -notmatch "indentLevel\+\+") "camera item children do not use a second Unity indent"
Assert-True ($cameraUtil -match "SameAuthoring") "camera SameAuthoring exists for LivePush"

Assert-True ($cameraUtil -match "!ResponsiveWidgetLayout\.IsSeedDisabledSlot\(i\)") "first camera Target assignment leaves Flip Cover Landscape disabled"
Assert-True ($cameraUtil -match "currentlyEnabled \|\|") "camera first capture preserves already-enabled slots"
Assert-True ($cameraUtil -match "if \(!applyEnabledDefaults\)") "camera retarget capture writes only the active slot"
Assert-True ($cameraDrawer -notmatch "enabledProp\.boolValue = enable") "camera drawer does not overwrite Enabled after capture"

$rectDrawer = Get-Content (EditorPath "ResponsiveRectStateDrawer.cs") -Raw
Assert-True ($rectDrawer -match "ShouldExpandState") "widget state foldout follows detected slot"
Assert-True ($rectDrawer -match "DrawFoldoutRow") "widget state foldout allows other slots when Show Other Slots is on"
Assert-True ($rectDrawer -match "DrawPresetGeometry") "widget state Inspector adapts geometry fields to anchor presets"
Assert-True ($rectDrawer -match '"Left"') "horizontal stretch shows Left"
Assert-True ($rectDrawer -match '"Right"') "horizontal stretch shows Right"
Assert-True ($rectDrawer -match '"Top"') "vertical stretch shows Top"
Assert-True ($rectDrawer -match '"Bottom"') "vertical stretch shows Bottom"
Assert-True ($rectDrawer -match '"Pos X"') "fixed horizontal anchors show Pos X"
Assert-True ($rectDrawer -match '"Width"') "fixed horizontal anchors show Width"
Assert-True ($rectDrawer -match "CopyFallback") "widget Enabled checkbox copies its fallback source (Phone, or Pad for iPhone Duo Seated)"
Assert-True ($rectDrawer -match "ResponsiveUiSlotClipboard") "widget slots have Copy / Paste / Reset"
Assert-True ($rectDrawer -match "DrawLayoutFields") "widget and camera Focus Bounds share layout fields"
Assert-True ($rectDrawer -match "LayoutFieldsHeight") "shared layout fields report Inspector height"
Assert-True ($rectDrawer -match "DrawSlotSection") "widget slots use collapsible tinted Layout / Scale sections"
Assert-True ($rectDrawer -match '"Layout"') "widget slots group geometry under Layout"
Assert-True ($rectDrawer -match '"Scale"') "widget slots group Design Scale under Scale"
Assert-True ($rectDrawer -match "wideMode") "layout Vector2 fields stay on one Inspector row"
Assert-True ($rectDrawer -match "DrawFloatPair") "geometry fields draw Left/Right and Top/Bottom as pairs"
Assert-True ($rectDrawer -match "PrefixLabel") "geometry pair rows use the shared Inspector label column"
Assert-True ($rectDrawer -match "Left / Right") "stretched X geometry is labeled Left / Right"
Assert-True ($rectDrawer -match "DrawNestedContent") "widget Enabled and Layout sit under the slot title"
Assert-True ($rectDrawer -notmatch "indentLevel\+\+") "widget slot children do not use a second Unity indent"
Assert-True ($rectDrawer -match "GetStatePropertyIndex") "nested layout properties do not use detected-slot foldout collapse"
Assert-True ($rectDrawer -match "MultiFloatField") "layout fields draw compact Vector2 values instead of PropertyField foldouts"
Assert-True ($rectDrawer -notmatch "DrawEmbedded") "camera Focus Bounds does not use a second embed API"
Assert-True ($rectDrawer -notmatch "IsWidgetSlotProperty") "slot-name lookup lives on ResponsiveWidgetLayout"
Assert-True ($rectDrawer -match "DrawLockedDesignCanvasSize") "widget Design Canvas Size is locked like camera Design UI Size"
Assert-True ($rectDrawer -match "Locked to Responsive UI settings") "widget Design Canvas Size reports its locked source"
Assert-True ($rectDrawer -notmatch "design\.vector2Value = Vector2\.zero") "widget Design Canvas Size row does not write serialized data every repaint"
Assert-True ($rectDrawer -match "OrientSize") "widget Design Canvas Size orients to the slot"
Assert-True ($rectDrawer -match "FormatAutoScaleHint") "widget Inspector shows which Auto axis was picked"

$camStateDrawer = Get-Content (EditorPath "ResponsiveCameraStateDrawer.cs") -Raw
Assert-True ($camStateDrawer -match "ShouldExpandState") "camera state foldout follows detected slot"
Assert-True ($camStateDrawer -match "DrawSlotSection") "camera slot values use collapsible tinted sections"
Assert-True ($camStateDrawer -match 'SlotKind = "CamSlot"') "camera slot section foldouts are per component"
Assert-True ($camStateDrawer -match 'SectionView = "View"') "camera slot has a View section"
Assert-True ($camStateDrawer -match 'SectionBoundsLayout = "Bounds Layout"') "camera slot has a Bounds Layout section"
Assert-True ($camStateDrawer -match "ViewTint") "camera slot sections use header colors"
Assert-True ($camStateDrawer -match "DrawFoldoutRow") "camera state foldout allows other slots when Show Other Slots is on"
Assert-True ($camStateDrawer -match "CopyFallback") "camera Enabled checkbox copies its fallback source (Phone, or Pad for iPhone Duo Seated)"
Assert-True ($camStateDrawer -match "Focus Target") "camera slot drawer draws Focus Target"
Assert-True ($camStateDrawer -match "Focus Screen") "camera slot drawer draws Focus Screen"
Assert-True ($camStateDrawer -match "Focus Bounds") "camera slot drawer draws Focus Bounds"
Assert-True ($camStateDrawer -match "DrawLayoutFields") "camera Focus Bounds draws widget layout fields"
Assert-True ($camStateDrawer -match "LayoutFieldsHeight") "camera Focus Bounds height includes the widget layout"
Assert-True ($camStateDrawer -match "Reference Model") "camera Focus Bounds shows the slot reference model"
Assert-True ($camStateDrawer -match "focusBoundsLayout") "camera slot drawer draws per-slot Focus Bounds layout"
Assert-True ($camStateDrawer -match "CaptureFocusBoundsLayoutInto") "assigning Focus Bounds captures this slot's widget layout"
Assert-True ($cameraUtil -match "public static bool CaptureFocusBoundsLayoutInto") "camera Focus Bounds capture lives in the camera editor util"
Assert-True ($cameraUtil -match "WriteCapturedLive") "camera Focus Bounds capture uses the shared widget live write"
Assert-True ($cameraUtil -match "StatePropertyIndex\(state.name\)") "camera Focus Bounds capture uses the camera slot index, not the nested layout name"
Assert-True ($cameraUtil -match "GetUiLayoutSize") "camera Focus Bounds capture without Hub uses the same UI size Apply uses"
Assert-True ($camStateDrawer -match "FocusBoundsLabel,\s*\r?\n\s*false\)") "camera Focus Bounds object field does not expand RectTransform children"
Assert-True ($cameraUtil -match "ReadFocusBounds") "camera editor ApplyFocus reads slot Focus Bounds"
Assert-True ($cameraUtil -match "ReadFocusBoundsLayout") "camera editor ApplyFocus reads per-slot Focus Bounds layout"
Assert-True ($cameraUtil -match "focusBoundsLayout") "camera editor util copies Focus Bounds layout"
Assert-True ($camStateDrawer -match "FormatAutoScaleHint") "camera Inspector shows which Auto axis was picked"
Assert-True ($camStateDrawer -match "ResponsiveUiSlotClipboard") "camera slots have Copy / Paste / Reset"
Assert-True ($camStateDrawer -match "DrawNestedContent") "camera Enabled and View sit under the slot title"
Assert-True ($camStateDrawer -notmatch "indentLevel\+\+") "camera slot children do not use a second Unity indent"

$activeDrawer = Get-Content (EditorPath "ResponsiveActiveObjectEntryDrawer.cs") -Raw
$activeSlotsUtil = Get-Content (EditorPath "ResponsiveActiveObjectSlotsEditorUtil.cs") -Raw
Assert-True ($activeDrawer -match "ResponsiveActiveObjectSlotsEditorUtil\.DrawFormRows") "Active Object rows draw through the shared slot-row helper"
Assert-True ($activeSlotsUtil -match "GetFormDisplayName") "Active Object form labels use GetFormDisplayName"
Assert-True ($activeSlotsUtil -match "ResponsiveWidgetLayout\.FormFactors") "Active Object form order aliases ResponsiveWidgetLayout.FormFactors"
Assert-True ($activeSlotsUtil -match "BeginChangeCheck") "Active Object slot toggles write only on change"
Assert-True ($activeDrawer -notmatch '"Fold Cover Phone"') "Active Object labels do not hardcode Fold Cover Phone"
Assert-True ($activeDrawer -match "SeedAllowed") "new Active Object rows start with allowed slots on"
Assert-True ($activeDrawer -match "AllOff") "assigning a Target on an empty mask seeds allowed slots"
Assert-True ($activeSlotsUtil -match "EditorGUI\.ToggleLeft") "Active Object forms expose Portrait / Landscape toggles"
Assert-True ($activeDrawer -match "public static void DrawSection") "Active Objects Inspector section is shared"
Assert-True ($activeDrawer -match "InspectorSectionKey") "Active Objects foldouts are per component"
Assert-True ($activeDrawer -match "ResetNewListRow") "Active Object list + clears the duplicated Target"
Assert-True ($activeDrawer -match "DrawNestedContent") "Active Object form rows sit under Target"

$eventsDrawer = Get-Content (EditorPath "ResponsiveLayoutEventsDrawer.cs") -Raw
Assert-True ($eventsDrawer -match "ShouldDrawState") "Events slots follow Show Other Slots and Allowed Orientations"
Assert-True ($eventsDrawer -match "layoutEvents") "Events section draws the per-slot layoutEvents struct"
Assert-True ($eventsDrawer -match "public static void DrawSection") "Events Inspector section is shared"
Assert-True ($eventsDrawer -match "InspectorSectionKey") "Events foldouts are per component"

$backgroundDrawer = Get-Content (EditorPath "ResponsiveBackgroundDrawer.cs") -Raw
Assert-True ($backgroundDrawer -match "image == null \|\| mode == null") "background drawer guards missing serialized properties"
Assert-True ($backgroundDrawer -match "ResponsiveBackground serialization is missing") "background drawer reports serialization mismatch"
Assert-True ($backgroundDrawer -match "ResetNewListRow") "background list + starts empty"
Assert-Match $backgroundDrawer 'ModeValues[\s\S]{0,200}Cover,[\s\S]{0,80}Fit,[\s\S]{0,80}Stretch,[\s\S]{0,80}FitWidth,[\s\S]{0,80}FitHeight,[\s\S]{0,80}WidthOnly,[\s\S]{0,80}HeightOnly,[\s\S]{0,80}Manual' "background Match Mode popup is Cover→Fit→Stretch→FitWidth→FitHeight→WidthOnly→HeightOnly→Manual"
Assert-True ($backgroundDrawer -match "Width Only") "background drawer exposes WidthOnly Match Mode"
Assert-True ($backgroundDrawer -match "Fit Width") "background drawer exposes FitWidth Match Mode"
Assert-True ($backgroundDrawer -match "Fit Height") "background drawer exposes FitHeight Match Mode"
Assert-True ($backgroundDrawer -match "Manual") "background drawer exposes Manual Match Mode"

$formTransitionDrawer = Get-Content (EditorPath "ResponsiveFormTransitionLoadingDrawer.cs") -Raw
Assert-True ($formTransitionDrawer -match "CustomPropertyDrawer\(typeof\(ResponsiveFormTransitionLoading\)\)") "form-transition loading has a property drawer"
Assert-True ($formTransitionDrawer -match "Form Transition Loading") "form-transition drawer exposes enable flag"
Assert-True ($formTransitionDrawer -match "Black Overlay") "form-transition drawer exposes black overlay"
Assert-True ($formTransitionDrawer -match "Loading Controller") "form-transition drawer exposes controller"
Assert-True ($formTransitionDrawer -match "Show On Slots") "form-transition drawer exposes Show On Slots"
Assert-True ($formTransitionDrawer -match "ResponsiveActiveObjectSlotsEditorUtil\.DrawFormRows") "form-transition drawer draws slot toggles with the Active Objects rows"
Assert-True ($formTransitionDrawer -match "EditorGUI\.BeginProperty") "form-transition drawer wraps BeginProperty for prefab overrides"
Assert-True ($formTransitionDrawer -notmatch "SeedShowOnSlots") "form-transition drawer does not seed Show On Slots mid-OnGUI (runtime owns defaults)"
Assert-True ($formTransitionDrawer -match "Min Visible") "form-transition drawer exposes min visible"
Assert-True ($formTransitionDrawer -match "HelpBox") "form-transition drawer shows setup guidance"
Assert-True ($formTransitionDrawer -match "EditorGUI\.Slider") "form-transition min visible uses a 0..2s slider"
Assert-True ($formTransitionDrawer -match "PrepareAssignedCover") "form-transition drawer prepares the black overlay / loading controller CanvasGroup on assign"
Assert-True ($formTransitionDrawer -notmatch "showOnSlotsInitialized") "form-transition drawer does not seed Show On Slots (runtime owns defaults)"
Assert-True ($formTransitionDrawer -match "Undo\.AddComponent<CanvasGroup>") "form-transition drawer adds CanvasGroup with Undo"

$monoImporterFields = @(
    "MonoImporter:",
    "externalObjects:",
    "serializedVersion:",
    "defaultReferences:",
    "executionOrder:",
    "icon:",
    "userData:",
    "assetBundleName:",
    "assetBundleVariant:"
)
foreach ($metaName in @("ResponsiveBackgroundDrawer.cs.meta", "ResponsiveLayoutEventsDrawer.cs.meta", "ResponsiveFormTransitionLoadingDrawer.cs.meta")) {
    $meta = Get-Content (EditorPath $metaName) -Raw
    foreach ($field in $monoImporterFields) {
        Assert-True ($meta -match [regex]::Escape($field)) "$metaName includes $field"
    }
    foreach ($emptyStringField in @("userData", "assetBundleName", "assetBundleVariant")) {
        $emptyStringPattern = "(?m)^  $([regex]::Escape($emptyStringField)):[^\S\r\n]+\r?$"
        Assert-True ($meta -match $emptyStringPattern) "$metaName uses Unity-compatible empty $emptyStringField syntax"
    }
}

$foldouts = Get-Content (EditorPath "ResponsiveUiInspectorFoldouts.cs") -Raw
Assert-True ($foldouts -match "EnsureHubField") "Inspector auto-assigns the Canvas hub"
Assert-True ($foldouts -match "ApplyModifiedProperties") "hub auto-assign is committed before later Inspector Update"
Assert-True ($foldouts -match "InspectorSectionKey") "list sections key foldouts per component"

$scrollDrawer = Get-Content (EditorPath "ResponsiveScrollLayoutDrawer.cs") -Raw
Assert-True ($scrollDrawer -match "ShouldDrawState") "scroll drawer hides inactive slots when Show Other Slots is off"
Assert-True ($scrollDrawer -match "ApplyDetectedFoldouts") "scroll drawer forces detected form foldouts"
Assert-True ($scrollDrawer -match "Detected Slot") "scroll drawer shows live detected Simulator / device slot"
Assert-True ($scrollDrawer -match "CopySlotToHost") "scroll LivePush writes host metrics via shared util"
Assert-True ($scrollDrawer -match "hasModifiedProperties") "scroll LivePush still runs when nested drawers swallow EndChangeCheck"
Assert-True ($scrollDrawer -match "SameAuthoring") "scroll LivePush ignores foldout expand"
Assert-True ($scrollDrawer -notmatch "Capture From Scroll View") "scroll layout has no Capture From Scroll View button"

$scrollEditorUtil = Get-Content (EditorPath "ResponsiveScrollEditorUtil.cs") -Raw
Assert-True ($scrollEditorUtil -match "ResponsiveUiDetectionDrawer.ResolveInspectorLayout") "scroll Inspector layout uses hub Detection / Simulator"
Assert-True ($scrollEditorUtil -match "ResponsiveRectStateEditorUtil.ResolveActiveSlot") "scroll slot resolve shares the widget fallback-chain helper"
Assert-True ($scrollEditorUtil -notmatch "DetectedSlotName|public static bool ShouldExpandState|public static bool DrawStateFoldout") "scroll util has no Detection pass-through wrappers"
Assert-True ($scrollDrawer -match "ResponsiveUiDetectionDrawer.ResolveFoldoutSlotName") "scroll foldouts use the shared Detection slot name"
Assert-True ($scrollEditorUtil -match "CopySlotToHost") "scroll LivePush copies slot metrics onto the host"
Assert-True ($scrollEditorUtil -match "public static bool CanLivePush") "scroll LivePush requires an enabled component and hub"
Assert-True ($scrollEditorUtil -match "public static Component FindHost") "scroll LivePush finds the FancyScrollView sibling"
Assert-True ($scrollEditorUtil -match "public static bool WriteHostMetrics") "scroll LivePush writes host cellInterval / scrollOffset through SerializedObject"
Assert-True ($scrollEditorUtil -match "public static bool ApplyLayoutToHost") "scroll hub Apply writes resolved slot metrics onto the host"
Assert-True ($scrollEditorUtil -match "public static bool CaptureHostToActiveSlot") "scroll host Inspector edits write the active slot"
Assert-True ($scrollEditorUtil -match "public static bool SameAuthoring") "scroll LivePush skips foldout-only Inspector changes"
Assert-True ($scrollEditorUtil -match "public static void LivePushIfActive") "scroll sliders LivePush from the state drawer"
Assert-True ($scrollEditorUtil -match "SyncOpenHostInspectors") "scroll LivePush syncs an open FancyScrollView Inspector"
Assert-True ($scrollEditorUtil -match "SuppressCapture") "scroll LivePush suppresses host recapture"
Assert-True ($scrollEditorUtil -match "EditorInvokeApply") "scroll slot LivePush Relayouts before writing host YAML"
Assert-True ($scrollEditorUtil -match "public static bool CopyFallback") "enabling a scroll slot copies its fallback source metrics (Phone, or Pad for iPhone Duo Seated)"
Assert-True ($scrollEditorUtil -match "public static bool CopySlot") "scroll utility copies one exact slot to another"
Assert-True ($scrollEditorUtil -match "public static void WriteState") "scroll slots share ReadState / WriteState with the clipboard"
Assert-True ($scrollEditorUtil -match "public static void ResetSlot") "scroll slots can reset to CreateDefault"
Assert-True ($scrollEditorUtil -notmatch "public static void CopySlot\(") "scroll Copy lives on the shared clipboard"

$scrollStateDrawer = Get-Content (EditorPath "ResponsiveScrollStateDrawer.cs") -Raw
Assert-True ($scrollStateDrawer -match "ResponsiveUiDetectionDrawer.ShouldExpandState") "scroll state foldout follows driver layout"
Assert-True ($scrollStateDrawer -match "DrawFoldoutRow") "scroll state foldout draws detected expansion"
Assert-True ($scrollStateDrawer -match "DrawSlotSection") "scroll slots use a collapsible tinted Metrics section"
Assert-True ($scrollStateDrawer -match "CopyFallback") "scroll Enabled checkbox copies its fallback source (Phone, or Pad for iPhone Duo Seated)"
Assert-True ($scrollStateDrawer -match "LivePushIfActive") "scroll Cell Interval / Scroll Offset sliders LivePush the host"
Assert-True ($scrollStateDrawer -match "ResponsiveUiSlotClipboard") "scroll slots have Copy / Paste / Reset"
Assert-True ($scrollStateDrawer -notmatch "Capture From Scroll View") "scroll state has no Capture From Scroll View button"
Assert-True ($scrollStateDrawer -match "DrawNestedContent") "scroll Enabled and Metrics sit under the slot title"
Assert-True ($scrollStateDrawer -notmatch "indentLevel\+\+") "scroll slot children do not use a second Unity indent"

$uiEditor = Get-Content (EditorPath "ResponsiveUIEditor.cs") -Raw
Assert-True ($uiEditor -match "phoneReferenceResolution") "hub Inspector authors Phone scaler resolution"
Assert-True ($uiEditor -notmatch "phoneOnlyObjects") "ResponsiveUI Inspector no longer draws the phone-only list"
Assert-True ($uiEditor -notmatch "EnsureDefaultWidget") "hub Inspector does not seed widgets"
Assert-True ($uiEditor -notmatch "ResponsiveActiveObjectEntryDrawer.DrawSection") "hub Inspector does not draw Active Objects"
Assert-True ($uiEditor -match "GetComponentInParent<Canvas>\(true\)") "hub Inspector finds an inactive parent Canvas"
Assert-True ($uiEditor -match "Override Background Size") "ResponsiveUI can override Project Settings background size"
Assert-True ($uiEditor -match "formTransitionLoading") "hub Inspector draws form-transition loading"
Assert-True ($uiEditor -notmatch "DrawFormTransitionLoading") "hub Inspector uses property drawer for form-transition loading"
Assert-True ($uiEditor -match "OpenProjectSettings") "Inspector can open Project Settings for background size"
Assert-True ($uiEditor -match "ResponsiveUiInspectorFoldouts") "ResponsiveUI Inspector collapses utility sections above Layout"
Assert-True ($uiEditor -match "InspectorSectionKey") "hub Inspector foldouts are per component"
Assert-True ($uiEditor -match "ColoredFoldoutSection") "hub Inspector uses tinted collapsible sections"
Assert-True ($uiEditor -match "DrawHingeSection") "hub Inspector owns Fold Half-Open Guide fields"
Assert-True ($uiEditor -notmatch "ApplyListFoldouts") "hub Inspector has no widget list foldouts"
Assert-True ($drawer -notmatch "CanCacheInspectorGUI") "drawer avoids obsolete Inspector cache override"
Assert-Match $drawer 'NotifyWidgetLivePreview\(\);[\s\S]{0,80}ApplyModifiedProperties' "LivePush suppresses validate Apply before push"
Assert-True ($drawer -match "RecaptureLiveForDesignScaleToggle") "toggling Design Scale recaptures live Target instead of pushing ratio"
Assert-True ($drawer -match "modeChanged") "changing Design Scale Mode recaptures live Target instead of jumping"
Assert-True ($drawer -match "SameAuthoring") "slot EndChangeCheck ignores foldout-only GUI.changed"
Assert-True ($drawer -notmatch "EndChangeCheck\(\) && ResponsiveRectStateEditorUtil.IsEnabled") "Inspector Enabled off still previews resolved widget layout"
Assert-True ($drawer -match "PreviewResolvedLiveSlot") "Inspector Enabled on the live slot previews resolved widget layout"
Assert-True ($drawer -match "PreviewTwoSideWidgets") "Inspector Enabled preview also applies two-side controllers"
Assert-True ($drawer -notmatch "PreviewControllerExactLiveSlot") "former controller-only Enabled hide preview is gone"
Assert-True ($drawer -match "IsInLiveFallbackChain") "Inspector Enabled preview covers every slot in the live fallback chain"
Assert-True ($drawer -match "PreviewControllerWidgets") "assigning a controller Target previews widget layout"
Assert-True ($drawer -match "RecaptureSlotFromLive") "toggle recapture writes the slot that was toggled"
Assert-Match $drawer 'EditorApplication\.delayCall[\s\S]{0,80}SceneView\.RepaintAll' "LivePush defers SceneView repaint"
Assert-True ($drawer -match "ForceUpdateCanvases") "LivePush refreshes parent rect before stretch Apply"
Assert-True ($drawer -match "!responsiveUi\.enabled") "LivePush does not mutate Target when hub is disabled"
Assert-True ($drawer -match "ApplyInspectorTargetChange") "clearing widget Target resets every slot"
Assert-True ($drawer -match "objectReferenceInstanceIDValue") "clearing a Missing widget Target still resets slots"

$objectRef = Get-Content (Join-Path $sharedEditor "ResponsiveUiObjectReference.cs") -Raw
Assert-True ($objectRef -match "WasClearedToNone") "shared ObjectField clear detects Missing refs"
Assert-True ($objectRef -match "IsFirstAssign") "shared ObjectField first-assign includes Missing"

$focus = Get-Content (EditorPath "ResponsiveUiSimulatorSlotFocus.cs") -Raw
$simulatorPlugin = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldableDeviceSimulatorPlugin.cs") -Raw
Assert-Match $simulatorPlugin 'deviceSimulator\.deviceChanged \+= OnDeviceChanged' "Simulator plugin subscribes to Unity device profile changes"
Assert-Match $simulatorPlugin 'void OnDeviceChanged\(\)[\s\S]{0,400}RequestSelectionPublish' "Unity device profile event asks the bridge to publish a settled selection"
Assert-True ($simulatorPlugin -notmatch "void OnDeviceChanged\(\)[\s\S]{0,400}NotifySelectionChanged") "plugin does not publish selection itself"
Assert-True ($simulatorPlugin -match "SimulatorDeviceBridge\.IsSelectionPending") "stale Fold controls hide while a new device selection settles"
$bridge = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\SimulatorDeviceBridge.cs") -Raw
Assert-True ($bridge -match "RequestSelectionPublish") "bridge owns Simulator selection publish"
Assert-True ($bridge -match "GetActiveSelection") "bridge prefers the active visible Simulator window"
Assert-True ($bridge -match "SelectionSettleRetries") "bridge settles identity with a bounded retry chain"
Assert-True ($bridge -match "BuildSelectionIdentity") "bridge identity ignores W×H flicker"
Assert-True ($bridge -notmatch "EditorApplication\.update") "bridge does not idle-poll Simulator selection"
Assert-True ($focus -match "GetFocusedSlotName") "Simulator slot focus API exists"
Assert-True ($focus -match "GetFocusedLayout") "focus exposes Simulator-selected layout"
Assert-True ($focus -match "ResolveFoldoutSlotName") "Simulator focus exposes foldout slot names"
Assert-True ($focus -match "SimulatorDeviceBridge\.SelectionChanged") "focus is event-driven from Simulator selection"
Assert-True ($focus -match "ResponsiveUiSimulatorLayoutResolver\.Resolve") "focus uses shared Simulator layout resolver"
Assert-True ($focus -match "FoldableEditorSession\.Changed") "focus reacts to Fold posture changes"
Assert-True ($focus -match "OnFoldableScreenSynced") "Phone to Flip Cover to Main sync forces Apply after Unity Screen commits"
Assert-True ($focus -match "ApplySettledLayout") "Fold/Flip Edit Mode Apply is one settled-layout path"
Assert-True ($focus -match "if \(!FoldableSimulatorScreenCommit\.InFlight\)") "Fold/Flip does not Apply widgets from Cover chrome SelectionChanged"
Assert-True ($focus -match "FoldableSimulatorScreenCommit.InFlight") "Fold session refresh skips widget Apply while Cover to Main is in flight"
Assert-True ($focus -match "ScreenSynced") "focus subscribes to Fold session screen-synced after SyncFromSession"
Assert-True ($focus -match "RefreshCachedSelectionForSession") "focus refreshes selection identity on Fold panel changes"
Assert-True ($focus -match "ProcessSessionRefresh") "Fold panel Apply waits for cover/inner Simulator sync"
Assert-True ($focus -match "layoutChanged") "focus revises Inspector when layout key changes"
Assert-True ($focus -notmatch "EditorApplication\.update") "focus does not idle-poll"
Assert-True ($focus -notmatch "ActiveEditorTracker\.ForceRebuild|ForceRebuild\(") "focus must not rebuild editors (Raw Input)"
Assert-True ($focus -match "ResponsiveUiInspectorRepaint\.Queue") "focus queues soft Inspector repaint"
Assert-True ($focus -match "isPlayingOrWillChangePlaymode") "focus skips soft-repaint / refresh in Play Mode"
Assert-True ($focus -match "RefreshHandlersIfNeeded") "handlers refresh on layout or ScreenIndex identity change"
Assert-True ($focus -match "BuildApplyFingerprint") "handler Apply fingerprint includes ScreenIndex and ProductState"
Assert-True ($focus -match "ScreenSizeRequiresLayoutApply") "Flip Cover to Main WxH still re-Applies after Phone to Flip first-bind"
Assert-True ($focus -match "_pushedApplySize") "handler Apply fingerprint also tracks catalog WxH with flicker hysteresis"
$scheduler = Get-Content (EditorPath "ResponsiveUiEditorLayoutScheduler.cs") -Raw
Assert-True ($scheduler -match "ScheduleCanvasCatchup") "one editor scheduler owns the canvas-ready pass"
Assert-True ($scheduler -match "ApplyFocusedLayoutAfterCanvas") "scheduler catch-up uses the canvas-ready focus pass"
Assert-True ($scheduler -match "_canvasPassAgain") "Cover Apply catch-up still schedules a second pass after Main screen commits"
Assert-True ($focus -match "ResponsiveUiEditorLayoutScheduler\.Apply") "focus applies through the shared editor scheduler"
Assert-True ($focus -match "ApplyFocusedLayoutAfterCanvas") "focus exposes a canvas-ready layout pass"
Assert-True ($focus -notmatch "_canvasSizeRefreshQueued") "focus no longer owns a parallel canvas catch-up flag"
Assert-True ($focus -notmatch "QueueHandlerRefreshIfLayoutChanged") "former layout-only handler gate removed"
Assert-True ($focus -match "BuildIdentityFingerprint") "focus ignores incomplete / resolution-only Simulator flicker"
Assert-True ($focus -notmatch "GetMainGameViewSize") "focus avoids Game View size reads"
Assert-True ($focus -match "RefreshEditorLayoutNow") "focus refreshes handlers from events"
Assert-True ($focus -match "EditorOnHandlerEnabled") "focus pushes layout when a handler enables"
Assert-Match $focus 'OnHandlerEnabled[\s\S]{0,800}ScheduleCanvasCatchup' "enable uses the shared canvas-ready scheduler"
Assert-True ($focus -match "EnsureFocusFromSimulator") "enable path syncs focus without global refresh"
Assert-True ($focus -notmatch "EditorApplication\.hierarchyChanged") "focus avoids broad hierarchy refreshes"

$handler = Get-Content (Join-Path $pkg "Runtime\ResponsiveUiHandler.cs") -Raw
Assert-True ($handler -match "EditorOnHandlerEnabled") "handler exposes EditorOnHandlerEnabled hook"
Assert-True ($handler -match "EditorOnHandlerEnabled\.Invoke") "edit-mode OnEnable pulls Simulator layout via hook"
Assert-True ($handler -match "QueuePostEnableSceneLayout") "enable fallback queues canvas-ready scene layout pass when the focus hook is unset"
Assert-True ($handler -match "_skipNextValidateApply = false") "enable clears stale LivePush suppress"
Assert-True ($handler -match "QueuePlayModePostLoadApply") "Play Mode scene load re-detects after first layout"
Assert-Match $handler 'NotifyDisplayChanged\(\);[\s\S]{0,120}Apply\(\)' "scene load invalidates display cache then Apply"
Assert-Match $handler '!Application\.isPlaying && _hasEditorSimulatorLayout' "Play Mode ignores stale Editor Simulator layout"
Assert-True ($handler -match "RefineCoverForms") "Edit Mode focused layout Cover-refines WxH"
Assert-True ($handler -match "HasSimulatorLayout") "handler prefers Simulator orientation when pinned"
Assert-Match $handler '!Application\.isPlaying && ResponsiveDevice\.HasSimulatorLayout' "Edit Mode prefers live Simulator pin over stale focus"
Assert-True ($handler -match "RefreshEditorLayoutAfterCanvas") "handler canvas-ready pass is shared with Simulator catch-up"
Assert-True ($handler -match "ApplyInFlight") "handler exposes ApplyInFlight so clients skip nested ApplyNow"
Assert-True ($handler -match "QueuePostEnableSceneLayout[\s\S]{0,500}RefreshEditorLayoutAfterCanvas") "enable catch-up reuses the canvas-ready pass"

$applyOrder = Get-Content (Join-Path $runtime "ResponsiveUiApplyOrder.cs") -Raw
Assert-True ($applyOrder -match "InsertionSortByPriority") "client notify order is explicit"
Assert-True ($applyOrder -match "case ResponsiveUiController") "widgets settle before other clients"
Assert-True ($applyOrder -match "case ResponsiveCamera") "camera Apply follows widgets"
Assert-True ($applyOrder -match "case ResponsiveTextBanner") "banners stay last"
Assert-True ($applyOrder -match "GetConstrainedCanvasSize") "nested canvases still reapply Design Scale on size changes"
Assert-True ($applyOrder -match "HierarchyDepth") "widget apply order shares hierarchy depth"

$controllerSrc = Get-Content (Join-Path $runtime "ResponsiveUiController.cs") -Raw
Assert-True ($controllerSrc -notmatch "UsesCanvasScalerResolutionFit\)") "controller size reapply does not shortcut the whole hub scaler"
Assert-True ($controllerSrc -match "RequiresDesignScaleSizeReapply") "controller size reapply uses shared Design Scale helper"

$twoSideSrc = Get-Content (Join-Path $runtime "ResponsiveTwoSideUiTweenController.cs") -Raw
Assert-True ($twoSideSrc -match "RequiresDesignScaleSizeReapply") "two-side size reapply uses shared Design Scale helper"
Assert-True ($twoSideSrc -match "skipAnimating") "layout-only passes skip a running two-side tween"
Assert-True ($twoSideSrc -match "EditorApplyLayouts") "two-side Inspector Enabled preview applies resolved layout"
Assert-True ($twoSideSrc -notmatch "\.SetActive\(") "two-side slot Enabled does not SetActive Targets"

$captureGuard = Get-Content (EditorPath "ResponsiveUiEditorCaptureGuard.cs") -Raw
Assert-True ($captureGuard -match "IgnoreUntilDelayCall") "capture guard owns ignored-target lifetime"
Assert-True ($captureGuard -match "struct Scope") "capture guard returns a deferred suppress scope"

$bulkCopy = Get-Content (EditorPath "ResponsiveUiBulkCopyUtil.cs") -Raw
Assert-True ($bulkCopy -match "RevertAllDownToGroup") "bulk copy reverts Undo when nothing copied"
Assert-True ((Get-Content (EditorPath "ResponsiveUiControllerEditor.cs") -Raw) -match "ResponsiveUiBulkCopyUtil\.Execute") "controller bulk copy uses the shared Undo helper"
Assert-True ((Get-Content (EditorPath "ResponsiveCameraEditor.cs") -Raw) -match "ResponsiveUiBulkCopyUtil\.Execute") "camera bulk copy uses the shared Undo helper"
Assert-True ((Get-Content (EditorPath "ResponsiveScrollViewEditor.cs") -Raw) -match "ResponsiveUiBulkCopyUtil\.Execute") "scroll bulk copy uses the shared Undo helper"
Assert-True ((Get-Content (EditorPath "ResponsiveTwoSideUiTweenControllerEditor.cs") -Raw) -match "ResponsiveUiBulkCopyUtil\.Execute") "two-side bulk copy uses the shared Undo helper"

$controllerEditor = Get-Content (EditorPath "ResponsiveUiControllerEditor.cs") -Raw
Assert-True ($controllerEditor -match "EnsureDefaultWidget") "controller Inspector seeds one widget on Add Component"
Assert-True ($controllerEditor -match "ResponsiveActiveObjectEntryDrawer.DrawSection") "controller Inspector draws per-slot Active Objects"
Assert-True ($controllerEditor -match "_lastActiveCount") "controller Inspector seeds new Active Object rows"
Assert-True ($controllerEditor -match "ConsumeSeedDefaultLayout") "controller does not seed existing empty widget lists"
Assert-True ($controllerEditor -match "ApplyListFoldouts") "controller Inspector synchronizes Simulator foldouts before drawing"
Assert-True ($controllerEditor -match "Bulk Copy Widget Slots") "controller Inspector exposes bulk widget slot copy"
Assert-True ($controllerEditor -match 'InspectorKey\("Widgets"\)') "controller Inspector groups bulk copy and items under Widgets"
Assert-Match $controllerEditor 'new GUIContent\("Widget Items"\)' "widget list is labeled as a child of the Widgets section"
Assert-True ($controllerEditor -match "BoxedFoldoutSection") "Widgets panel uses the shared boxed parent layout"
Assert-True ($controllerEditor -match "ColoredFoldoutSection") "controller Inspector uses tinted collapsible Layout / Widgets sections"
Assert-True ($controllerEditor -match "ResponsiveUiBulkSlotCopyPanel") "controller Inspector uses the shared bulk-copy panel"
Assert-True ($bulkPanel -match "bool _expanded;") "bulk widget copy panel starts collapsed"
Assert-True ($bulkPanel -match "EditorApplication\.delayCall") "bulk widget copy defers mutation and preview past the GUI event"
Assert-True ($bulkPanel -match "DrawBulkCopyPanel") "bulk-copy panel state draws the shared clipboard panel"
Assert-True ($controllerEditor -match "DrawDesignScaleHints") "controller Inspector shows shared Design Scale hints"
Assert-True ((Get-Content (EditorPath "ResponsiveUiInspectorFoldouts.cs") -Raw) -notmatch "new SerializedObject\(hub\)") "Design Scale hints do not build a hub SerializedObject per repaint"
Assert-True ($controllerEditor -match "ResponsiveUiBulkCopyUtil\.Execute") "bulk widget copy records component and preview targets"
Assert-True ($bulkCopy -match "Undo\.RevertAllDownToGroup") "bulk widget copy cleans up a deferred no-op Undo group"
Assert-True ($controllerEditor -match "controller\.EditorApplyWidgets") "bulk widget copy previews only the edited controller"
Assert-True ($controllerEditor -notmatch "target\.gameObject") "bulk widget copy Undo does not record Target GameObjects for Enabled hide/show"
Assert-True ($controllerEditor -notmatch "hub\.Apply\(\)") "bulk widget copy does not re-apply unrelated hub clients"
Assert-True ($controllerEditor -match "LogBulkCopyResult") "bulk widget copy reports completion in the Unity Console"
Assert-True ($controllerEditor -notmatch "NicifyVariableName") "controller Editor static initialization avoids forbidden Unity API calls"
Assert-True ($controllerEditor -match "CanMutateInspectorLayout") "controller Inspector defers foldout sync off MouseDown"
Assert-True ($controllerEditor -match "ResetNewListRow") "controller Inspector resets duplicated widget rows to empty"
Assert-True ($controllerEditor -match "ForEachNewRow") "controller Inspector resets new rows before the list draws"
Assert-True ($controllerEditor -match "_lastWidgetCount") "controller Inspector remembers widget list size"
Assert-Match $controllerEditor 'orientationRevision[\s\S]{0,120}CanMutateInspectorLayout' "controller Inspector defers orientation Enabled sync off MouseDown"
Assert-True ($controllerEditor -match "EnsureHubField") "controller Inspector auto-assigns the Canvas hub"
Assert-True ($controllerEditor -match "DrawHubField") "controller Hub field uses the shared label column"
Assert-True ($controllerEditor -match "responsiveUi") "controller Inspector assigns the Canvas hub"

$scrollViewEditor = Get-Content (EditorPath "ResponsiveScrollViewEditor.cs") -Raw
Assert-True ($scrollViewEditor -match "EnsureHubField") "scroll Inspector auto-assigns the Canvas hub"
Assert-True ($scrollViewEditor -match "DrawHubField") "scroll Hub field uses the shared label column"
Assert-True ($scrollViewEditor -match "responsiveUi") "scroll Inspector assigns the Canvas hub"
Assert-True ($scrollViewEditor -match "scrollMetrics") "scroll Inspector draws scroll slots"
Assert-True ($scrollViewEditor -match "IsPortraitAllowedByPlayer") "scroll seed uses Player Allowed Orientations"
Assert-True ($scrollViewEditor -match "Bulk Copy Scroll Slots") "scroll Inspector exposes exact-slot copy"
Assert-True ($scrollViewEditor -match "ResponsiveUiBulkSlotCopyPanel") "scroll Inspector uses the shared bulk-copy panel"
Assert-True ($scrollViewEditor -match 'InspectorKey\("Layout"\)') "scroll Inspector groups bulk copy and slots under Scroll Layout"
Assert-Match $scrollViewEditor 'new GUIContent\("Scroll Slots"\)' "scroll slots are labeled as a child of Scroll Layout"
Assert-True ($scrollViewEditor -match "BoxedFoldoutSection") "scroll Inspector uses the shared boxed parent panel"
Assert-True ($scrollViewEditor -match "TintGreen") "scroll Inspector tints the Scroll Layout section"
Assert-True ($bulkPanel -match "EditorApplication\.delayCall") "scroll bulk copy defers mutation past the GUI event"
Assert-True ($scrollViewEditor -match "ResponsiveUiBulkCopyUtil\.Execute") "scroll bulk copy includes its live host in Undo"
Assert-True ($scrollViewEditor -match "EditorApplyTarget") "scroll bulk copy records the bound callback owner"
Assert-True ($bulkCopy -match "Undo\.RevertAllDownToGroup") "scroll bulk copy cleans up a deferred no-op Undo group"
Assert-True ($bulkCopy -match "Undo\.CollapseUndoOperations") "scroll bulk copy is one Undo operation"
Assert-True ($scrollViewEditor -match "scroll\.EditorLivePush") "scroll bulk copy previews active host metrics"
Assert-True ($scrollViewEditor -match "LogBulkCopyResult") "scroll bulk copy reports completion in the Unity Console"

$bannerEditor = Get-Content (EditorPath "ResponsiveTextBannerEditor.cs") -Raw
Assert-True ($bannerEditor -match "EnsureHubField") "banner Inspector auto-assigns the Canvas hub"
Assert-True ($bannerEditor -match "DrawHubField") "banner Hub field uses the shared label column"
Assert-True ($bannerEditor -match "ColoredFoldoutSection") "banner Inspector uses tinted collapsible sections"
Assert-True ($bannerEditor -match 'InspectorKey\("Preview"\)') "banner Inspector groups Preview / Hide under Preview"
Assert-True ($bannerEditor -match "responsiveUi") "banner Inspector assigns the Canvas hub"
Assert-True ($bannerEditor -match "DrawAlignmentGrid") "banner Inspector draws the alignment grid"
Assert-True ($bannerEditor -match "RightMiddle") "banner Inspector includes Right Middle"
Assert-True ($bannerEditor -match "CenterMiddle") "banner Inspector includes Center Middle"
Assert-True ($bannerEditor -match "LeftMiddle") "banner Inspector includes Left Middle"
Assert-True ($bannerEditor -match "stays put") "banner Inspector alignment points have tooltips"
Assert-True ($bannerEditor -match "NicifyVariableName") "banner Inspector shows the selected alignment name"
Assert-True ($bannerEditor -match "RefreshLayout") "banner Inspector re-fits when alignment changes"

$inspector = Get-Content (EditorPath "ResponsiveUiControllerEditor.cs") -Raw
Assert-True ($inspector -match "ApplyListFoldouts") "Inspector foldouts follow detected Simulator layout"
Assert-True ($inspector -match "ResponsiveUiDetectionDrawer\.ApplyListFoldouts") "Inspector foldouts prefer live Simulator focus"
Assert-True (Test-Path (EditorPath "ResponsiveUiDetectionDrawer.cs")) "Detection drawer exists"
$detectionDrawer = Get-Content (EditorPath "ResponsiveUiDetectionDrawer.cs") -Raw
Assert-True ($detectionDrawer -match "ResponsiveWidgetLayout\.FormFactors") "Detection form popup aliases ResponsiveWidgetLayout.FormFactors"
Assert-True ($detectionDrawer -match "HubSerializedObject") "slot foldouts read Detection from the Canvas hub"
Assert-True ($detectionDrawer -match "IResponsiveUiClient") "foldouts resolve the hub from client components"
Assert-True ($detectionDrawer -match "overrideInEditor") "Inspector foldouts honor Override In Editor"
Assert-True ($detectionDrawer -match "ApplyListFoldouts") "shared list foldout helper expands detected form slots"
Assert-True ($detectionDrawer -match "Detected Form Factor") "Detection drawer shows live form factor"
Assert-True ($detectionDrawer -match "ResponsiveDevice\.ResolveLiveLayout") "Detection drawer fallback uses ResolveLiveLayout"
Assert-True ($detectionDrawer -match "GetWatchScreenSize") "Inspector layout fallback uses Simulator watch not Game View Screen"
Assert-True ($detectionDrawer -notmatch "Screen\.width") "Detection drawer does not read Game View Screen"
Assert-True ($detectionDrawer -match "Show Other Slots") "Detection drawer has Show Other Slots toggle"
Assert-True ($detectionDrawer -match "SectionTitle") "Detection foldout title shows live form / orientation"
Assert-True ($detectionDrawer -match "collapseOthers") "shared foldouts collapse siblings only once per detected form"
Assert-True ($detectionDrawer -match "generationChanged") "shared foldouts re-collapse when Simulator / product state changes"
Assert-True ($detectionDrawer -match "foldoutSlot != _foldoutSlot") "shared foldouts re-collapse on detected slot, not inspected host"
Assert-True ($detectionDrawer -notmatch "_foldoutHostId") "shared foldouts do not reset when Canvas Inspectors draw several clients"
Assert-True ($detectionDrawer -match "DrawFoldout") "listed other slots stay user-expandable"
Assert-True ((Get-Content (Join-Path $runtime "ResponsiveUiHandler.cs") -Raw) -match "hideOtherSlots") "handler serializes Hide Other Slots"
Assert-True ((Get-Content (EditorPath "ResponsiveUIEditor.cs") -Raw) -match "ResponsiveUiDetectionDrawer\.Draw") "ResponsiveUI Inspector draws live Detection"
Assert-True ((Get-Content (EditorPath "ResponsiveCameraEditor.cs") -Raw) -notmatch "ResponsiveUiDetectionDrawer\.Draw") "ResponsiveCamera Inspector does not draw Detection"
Assert-True ($focus -match "RefreshHandlers\(\)") "focus refreshes handlers before Inspector repaint"
Assert-True ($focus -notmatch "EditorApplication\.delayCall \+= RefreshHandlers") "handler refresh is synchronous for foldout ordering"

$util = Get-Content (EditorPath "ResponsiveRectStateEditorUtil.cs") -Raw
Assert-True ($util -match "WriteCaptureToActiveSlot") "retarget capture writes only the active slot"
Assert-True ($util -match "ratio = Vector2\.one") "inactive first-assign slots capture at ratio 1"
Assert-True ($util -match "GetLayoutRatio") "active first-assign slot unscales with live canvas ratio"
Assert-True ($util -match "WriteCapturedLive") "shared live capture writes design-space layout and origin scale"
Assert-True ($util -match "ResolveIdentityLiveSize") "hub-less live capture uses default-profile identity"
Assert-True ($util -match "skipIfMatches") "shared live capture can force a Design Scale toggle recapture"
Assert-True ($util -match "RecaptureSlotFromLive") "Design Scale toggle recaptures the toggled slot from live Target"
Assert-True ($util -match "SlotOrientation\(slotIndex\)") "toggle recapture orients the live canvas ratio from the slot"
Assert-True ($util -match "SameAuthoring") "authoring equality ignores foldout-only Inspector churn"
Assert-True ($util -match "IsActiveEditorSlot") "IsActiveEditorSlot helper exists"
Assert-True ($util -match "ResponsiveWidgetSlotResolver") "editor capture uses shared slot fallback resolver"
Assert-True ($util -match "SyncSlotEnabled") "slot Enabled syncs from orientations"
Assert-Match $util 'seedNewWidget[\s\S]{0,220}ScaleFromDesignCanvasProp' "new widget slots start with Design Scale on"
Assert-True ($util -match "IsOptInSlot") "existing Cover/HalfOpened stay authored unless seeding a new widget"
Assert-True ($util -match "!seedNewWidget") "existing opt-in slots are not force-enabled"
Assert-True ($util -match "SyncLoaded") "scene-wide enabled sync exists"
Assert-True ($util -match "FindLoadedControllers") "scene-wide sync loads ResponsiveUiController"
Assert-True ($util -match "SlotAllowed") "slot Enabled syncs from allowed orientations"
Assert-True ($util -match "SyncCameras") "camera list Enabled sync exists"
Assert-Match $util 'IsSeedDisabledSlot\(i\)[\s\S]{0,140}targetEnabled = false' "new widget rows leave Flip Cover Landscape disabled"
Assert-Match $util 'captured\.enabled =[\s\S]{0,140}!ResponsiveWidgetLayout\.IsSeedDisabledSlot\(i\)' "first widget Target assignment leaves Flip Cover Landscape disabled"
Assert-True ($util -match "public static bool CopyFallback") "enabling Cover / Pad / Fold / Flip copies Phone layout"
Assert-Match $util "FindFallbackSource[\s\S]{0,1500}GetSeedCandidate" "seed source walks the Editor seed chain (GetSeedCandidate: iPhone Duo Seated and Wide Screen → Pad)"
Assert-Match $util 'SlotFormFactor\(slotIndex\) == ResponsiveDevice\.FormFactor\.Phone\)\s*return null;' "Phone slots are copy sources, not destinations"
Assert-True ($util -notmatch "HasAuthored") "enable copies Phone even when the destination already has values"
Assert-True ($util -match "GetNewRows") "new list rows are seeded after MouseDown array growth"
Assert-True ($util -match "public static void ResetNewListRow") "list + clears Unity duplicated widget rows"
Assert-True ($util -match "public static bool ForEachNewRow") "list + walks new rows with one reset helper"
Assert-True ($util -match "public static void ResetSlot") "widget slots can reset to Unity RectTransform defaults"
Assert-True ($util -match "public static void ResetSlots") "clearing widget Target resets every slot"
Assert-True ($util -match "ApplyInspectorTargetChange") "widget Target field uses shared assign/clear path"
Assert-True ($util -match "reset.sizeDelta = size") "widget Reset keeps Size Delta"
Assert-True ($util -notmatch "public static void CopySlot\(") "widget Copy lives on the shared clipboard"
Assert-True ($util -match "public static int CopySlotAcrossWidgets") "widget utility bulk-copies one exact slot across all rows"
Assert-True ($util -match "public static int CopySlotAcrossTwoSideWidgets") "two-side utility bulk-copies the currently shown layout across all rows"
Assert-True ($util -match "public static int CopySideAcrossTwoSideWidgets") "two-side utility bulk-copies a mirrored side onto the other side"
Assert-True ($util -match "public static int CopyWidgetSideAcrossTwoSideWidgets") "two-side utility copies one widget side onto another widget"
Assert-True ($util -match "public static int SwapWidgetSideAcrossTwoSideWidgets") "two-side utility swaps one widget side with another widget"
Assert-True ($util -match "public static ResponsiveRectState MirrorHorizontally") "two-side side copy mirrors Anchor X and Pos X"
Assert-True ($util -match "public static void SeedTwoSideWidget") "new two-side rows seed all four layouts"
Assert-True ($util -match "SyncTwoSideWidgets") "Player Allowed Orientations sync nested two-side layouts"
Assert-True ($util -match "size.vector2Value = state.designCanvasSize") "WriteDesignScale persists authored Design Canvas Size"
Assert-True ($util -notmatch "size.vector2Value = .*GetResolvedDesignCanvasSize") "WriteDesignScale does not bake resolved Design Canvas Size"
Assert-True ($util -notmatch "defaultScale.vector2Value = .*GetResolvedDefaultScale") "WriteDesignScale does not bake resolved Default Scale"

$clipboard = Get-Content (EditorPath "ResponsiveUiSlotClipboard.cs") -Raw
Assert-True ($clipboard -match "public static bool DrawFoldoutRow") "slot foldouts share Copy / Paste / Reset"
Assert-True ($clipboard -match "DrawBulkCopyPanel") "widgets, two-side, cameras, and scroll share one bulk-copy panel"
Assert-True ($clipboard -match "IsValidBulkCopy") "bulk-copy execution validates deferred slot indexes"
Assert-True ($clipboard -match "public static void LogBulkCopyResult") "bulk-copy Console reporting is centralized"
Assert-True ($clipboard -match "public static void LogBulkSideCopyResult") "two-side side copy Console reporting is centralized"
Assert-True ($clipboard -match "public static void LogBulkWidgetRowCopyResult") "two-side widget-row copy Console reporting is centralized"
Assert-Match $clipboard '\[ResponsiveUI\] Bulk Copy Widget Row' "widget-row Console messages use a distinct searchable prefix"
Assert-True ($clipboard -match "WidgetRowBulkCopyOptOutKey") "two-side widget-row confirmation uses a separate session opt-out"
Assert-True ($clipboard -match "WidgetSideBulkCopyOptOutKey") "two-side side copy confirmation uses a separate session opt-out"
Assert-Match $clipboard '\[ResponsiveUI\] Bulk Copy' "bulk-copy Console messages use a searchable package prefix"
Assert-True ($clipboard -match "expanded = ResponsiveUiInspectorFoldouts.DrawGuiFoldout") "shared bulk-copy panel uses the same foldout renderer as item rows"
Assert-True ($clipboard -match "DrawGuiFoldout") "bulk-copy From / To sit under the panel title"
Assert-True ($clipboard -notmatch "BeginVertical") "bulk-copy foldout uses its parent's indent without a nested margin"
Assert-True ($clipboard -match "DialogOptOutDecisionType\.ForThisSession") "bulk-copy confirmation has a safe session-scoped do-not-show option"
Assert-True ($clipboard -match "BulkCopyDialogOptOutKeyPrefix \+ kind") "bulk-copy confirmation stores opt-out separately per client kind"
Assert-True ($clipboard -match "ShouldDrawState") "bulk-copy menus list the same available slots as slot rows"
Assert-True ($clipboard -match "availableSlots\.Length == 0") "bulk-copy panel handles no available orientations"
Assert-True ($clipboard -match "public static void Copy\(") "clipboard Copy stores the slot"
Assert-True ($clipboard -match "public static bool Paste\(") "clipboard Paste writes the stored slot"
Assert-True ($clipboard -match "public static void Reset\(") "clipboard Reset uses type-specific ResetSlot"
Assert-True ($clipboard -match "GUI.changed = true") "Paste / Reset mark the Inspector dirty for LivePush"
Assert-True ($clipboard -match "LivePushIfActive") "scroll Paste / Reset LivePush the FancyScrollView host"
Assert-True ($clipboard -notmatch "Copy\(state, kind\);\s*\r?\n\s*GUI.changed") "Copy does not mark the Inspector dirty"

$settings = (Get-Content (EditorPath "ResponsiveUiEditorSettings.cs") -Raw) +
    (Get-Content (EditorPath "ResponsiveUiSettingsProvider.cs") -Raw) +
    (Get-Content (EditorPath "ResponsiveUiSettingsWatchers.cs") -Raw)
Assert-True ($settings -match "DesignForms =\s*\r?\n\s*ResponsiveWidgetLayout\.FormFactors") "DesignForms aliases ResponsiveWidgetLayout.FormFactors"
Assert-True ($settings -match "foldPhoneCover") "Project Settings has Fold Cover design defaults"
Assert-True ($settings -match "flipPhoneCover") "Project Settings has Flip Cover design defaults"
Assert-True ($settings -match "foldCoverHalfOpened") "Project Settings has Fold Cover HalfOpened design defaults"
Assert-True ($settings -match "GetCoverCanvas") "Fold Cover catalog uses Cover/outer canvas"
Assert-True ($settings -match '"Fold Cover"') "Settings UI lists Fold Cover row"
Assert-True ($settings -match 'LabelField\(ResponsiveDevice\.GetFormDisplayName\(form\), EditorStyles\.boldLabel\)') "Settings UI names each row with the form's display name"
Assert-Match $settings 'FormVisible\(forms\[i\]\)\)\s*DrawFormDefaults\(settings, forms\[i\]\)' "Settings UI lists the build target's forms only (rows follow ResponsiveWidgetLayout.FormFactors)"
Assert-True ($settings -match '"Use Pad Design Canvas"') "Settings UI Wide Screen row can copy the Pad Design Canvas once"
Assert-True ($settings -match "DrawEditableFormDefaults") "Settings UI Wide Screen row edits Design Canvas Size directly (no catalog device)"
Assert-True ($settings -match "Fold Cover Half Opened") "Settings UI lists Fold Cover Half Opened row"
Assert-True ($settings -match "Flip Cover") "Settings UI lists Flip Cover row"
Assert-True ($settings -match "galaxy-z-fold7-like") "Fold preferred catalog is Fold7"
$sampleSetup = Get-Content (EditorPath "ResponsiveUiSampleSetup.cs") -Raw
Assert-True ($sampleSetup -match "FormDisplayNameList") "sample Canvas dialog names every slot form (Fold Cover, Flip Cover, iPhone Duo Seated, …)"
Assert-True ($settings -match "FormDisplayNameList") "Settings builds the form list from GetFormDisplayName"
Assert-True ($settings -match "Background Cover Size") "Settings UI has Background Cover Size"
Assert-True ($settings -match "Fold Half-Open Guide") "Settings UI has project-wide hinge guide"
Assert-True ($settings -match "Draw Half-Open Hinge Line") "Settings UI can toggle the Simulator hinge line"
Assert-True ($settings -match "SetHingeGuide") "settings persist hinge guide color and thickness"
Assert-True ($settings -notmatch "SanitizeHingeGuide|EnsureHingeGuideSanitized") "hinge guide settings keep an authored alpha-0 color (no legacy sanitize)"
Assert-True ($settings -match "internal static void DrawHingeGuideFields") "Project Settings and the hub Inspector share one hinge-guide field drawer"
Assert-True ((Get-Content (EditorPath "ResponsiveUiEditorSettings.cs") -Raw) -notmatch "class ResponsiveUiSettingsProvider") "Project Settings page lives in its own file"
Assert-True ($settings -match "Follow Design Canvas Reference") "background can follow a Design Canvas reference"
Assert-True ($settings -match "ResolveCatalogSize") "catalog resolve supports portrait and landscape"
Assert-True ($settings -match "GetBackgroundArtSize") "settings expose background art size"
Assert-True ($settings -match "ResponsiveUiBackgroundSizeWatch") "background size change watcher exists"
Assert-True ($settings -match "EditorBakeProjectBackgroundSize") "watcher bakes size onto loaded ResponsiveUI"
Assert-True ($settings -match "ui\.Apply\(\)") "watcher re-Applies loaded ResponsiveUI hubs (clients follow)"
Assert-True ($settings -match "IsPortraitAllowedByPlayer") "portrait allowed helper exists"
Assert-True ($settings -match "IsLandscapeAllowedByPlayer") "landscape allowed helper exists"
Assert-True ($settings -match "public static bool SlotAllowed") "slot-index orientation helper exists"
Assert-True ($settings -match "ResponsiveUiOrientationWatch") "orientation change watcher exists"
Assert-True ($settings -match "public static int Revision") "orientation watch exposes Revision for Inspector"
Assert-True ($settings -match "SyncLoaded") "watcher syncs Enabled on change"
Assert-True ($settings -match "IActiveBuildTargetChanged") "watcher uses modern build-target change API"
Assert-True ($settings -notmatch "EditorUserBuildSettings\.activeBuildTargetChanged") "watcher avoids obsolete build-target event"
Assert-True ($settings -notmatch "EditorApplication\.update") "orientation watcher is event-driven"
Assert-True ($settings -match "playModeStateChanged") "settings watchers resume deferred sync after Play Mode"
Assert-True ($settings -match "_syncAfterPlayMode") "settings watchers remember changes skipped during Play Mode"
Assert-True ($settings -match "FoldablePlayerOrientation\.IsPortraitAllowed") "settings delegates portrait to FoldablePlayerOrientation"
Assert-True ($settings -match "BindPreferredCatalogProfiles\(\);[\s\S]{0,200}EnsureReferenceSimulatorSkinsEnabled\(\)") "delayCall syncs Design Canvas Simulator skins after preferred profiles bind"
Assert-True ($settings -match "int foldLabels = FoldableDeviceCatalog\.SyncDesignReferenceSimulatorLabels\(\)") "EnsureReferenceSimulatorSkinsEnabled tracks Foldable label writes"
Assert-True ($settings -match "skinsChanged && foldLabels == 0") "Phone/Pad-only Simulator refresh still runs when Foldable wrote no labels"

$foldSkin = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldableDeviceSimulatorSkin.cs") -Raw
Assert-True ($foldSkin -match "AssetDatabase\.ImportAsset\(assetPath, ImportAssetOptions\.ForceUpdate\)") "Foldable Design Canvas label sync ImportAsset's patched .device skins"
Assert-True ($foldSkin -match "public static int SyncDesignReferenceSimulatorLabels") "Foldable Design Canvas label sync returns write count"
Assert-True ($foldSkin -match "patchedAbs") "Foldable Design Canvas label sync tracks patched .device paths"
Assert-True ($foldSkin -match "SimulatorDeviceBridge\.ForceReloadDeviceLists\(\)") "Foldable Design Canvas label sync reloads Device Simulator lists"
# Refresh must follow label patches in SyncDesignReferenceSimulatorLabels (not only in SetSimulatorEnabled).
Assert-True ($foldSkin -match "(?s)PatchDeviceFriendlyName[\s\S]*?if \(written > 0\)[\s\S]*?AssetDatabase\.Refresh\(\)") "Foldable Design Canvas label sync Refresh's after patching .device names"

$devicesDir = Join-Path $pkg "FoldAndFlip\Editor\Devices"
$fold8Device = Get-Content (Join-Path $devicesDir "Galaxy Z Fold8-like.device") -Raw
$flip8Device = Get-Content (Join-Path $devicesDir "Galaxy Z Flip8-like.device") -Raw
Assert-True ($fold8Device -match '"friendlyName"\s*:\s*"\\u0001Fold Cover / Galaxy Z Fold8-like"') "Fold Cover (Fold8) Simulator Design Canvas label is Fold Cover / at top"
Assert-True ($flip8Device -match '"friendlyName"\s*:\s*"\\u0001Flip / Galaxy Z Flip8-like"') "Flip8 Simulator Design Canvas label is Flip / at top"
$duoDevice = Get-Content (Join-Path $devicesDir "iPhone Duo-like.device") -Raw
Assert-True ($duoDevice -match '"friendlyName"\s*:\s*"\\u0001iPhone Duo / iPhone Duo-like"') "iPhone Duo Simulator Design Canvas label is iPhone Duo / at top"
Assert-True ($fold8Device -notmatch 'Fold / Galaxy Z Fold8-like') "Fold8 Design Canvas ref is Fold Cover /, not Fold /"

# Fold HalfOpened Design Canvas may be retargeted in ProjectSettings (Fold7 default → Fold2, …).
$foldProfileToDevice = @{
    "galaxy-z-fold7-like" = "Galaxy Z Fold7-like.device"
    "galaxy-z-fold2-like" = "Galaxy Z Fold2-like.device"
    "galaxy-z-fold8-like" = "Galaxy Z Fold8-like.device"
}
$foldRefDevice = "Galaxy Z Fold7-like.device"
$settingsAssetPath = Join-Path (Split-Path (Split-Path $pkg -Parent) -Parent) "ProjectSettings\ResponsiveUiSettings.asset"
if (Test-Path -LiteralPath $settingsAssetPath) {
    $settingsAsset = Get-Content -LiteralPath $settingsAssetPath -Raw
    # Any field order: the top-level `fold:` block (two-space indent), then its own profileId.
    $foldMatched = $settingsAsset -match '(?m)^  fold:[ \t]*\r?\n(?:    \w+:.*\r?\n)*?    profileId:[ \t]*(\S+)'
    Assert-True $foldMatched "ResponsiveUiSettings.asset has a Fold Half Opened profileId"
    if ($foldMatched) {
        $foldProfileId = $Matches[1].Trim()
        Assert-True ($foldProfileToDevice.ContainsKey($foldProfileId)) "Fold Half Opened profile '$foldProfileId' has a known Simulator device"
        if ($foldProfileToDevice.ContainsKey($foldProfileId)) {
            $foldRefDevice = $foldProfileToDevice[$foldProfileId]
        }
    }
    $settingsForms = [regex]::Matches($settingsAsset, '(?m)^  (\w+):[ \t]*\r?\n    profileId:') | ForEach-Object { $_.Groups[1].Value }
    Assert-True (($settingsForms -join ",") -eq "phone,pad,foldPhoneCover,fold,foldCoverHalfOpened,flipPhoneCover,flip,iPhoneDuoCover,iPhoneDuoSeated,wideScreen") `
        "ResponsiveUiSettings.asset holds all ten design forms in declaration order"
    Assert-True ($settingsAsset -match '(?m)^  wideScreen:[ \t]*\r?\n    profileId:[ \t]*\r?\n') "Wide Screen design defaults have no catalog profileId"
}
$foldRefRaw = [System.IO.File]::ReadAllText((Join-Path $devicesDir $foldRefDevice))
Assert-True ($foldRefRaw -match '"friendlyName"\s*:\s*"\\u0001Fold / ') "Fold HalfOpened Design Canvas Simulator label is Fold / at top ($foldRefDevice)"
$fold7Device = [System.IO.File]::ReadAllText((Join-Path $devicesDir "Galaxy Z Fold7-like.device"))
$starFold7 = [char]0x2605 + ' Fold / Galaxy Z Fold7-like'
if ($foldRefDevice -ne "Galaxy Z Fold7-like.device") {
    Assert-True ($fold7Device.Contains($starFold7)) "Former Fold Design Canvas (Fold7) drops to toolkit ★ Fold / when retargeted"
} else {
    Assert-True (-not $fold7Device.Contains($starFold7)) "Fold7 Design Canvas ref is not ★ Fold / toolkit-style"
}

$orient = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldablePlayerOrientation.cs") -Raw
Assert-True ($orient -match "GetAllowedAxes") "GetAllowedAxes is the single Player orientation source"
Assert-True ($orient -match "IsPortraitAllowed") "FoldablePlayerOrientation.IsPortraitAllowed exists"
Assert-True ($orient -match "IsLandscapeAllowed") "FoldablePlayerOrientation.IsLandscapeAllowed exists"

# Unqualified System BCL names without `using System;` (the CS0103 class of miss).
# Not a compiler: UnityEngine types, missing members, and wrong overloads still need EditMode.
Write-Host ""
Write-Host "=== System using hygiene ===" -ForegroundColor Cyan
$bclNames = @("StringComparison", "StringComparer", "StringSplitOptions", "IDisposable")
$hygieneIssues = New-Object System.Collections.Generic.List[string]
Get-ChildItem -Path $pkg -Filter *.cs -Recurse | ForEach-Object {
    $text = Get-Content -LiteralPath $_.FullName -Raw
    $code = [regex]::Replace($text, '(?s)/\*.*?\*/', "")
    $code = [regex]::Replace($code, '//[^\n]*', "")
    $hasUsingSystem = [regex]::IsMatch($code, '(?m)^\s*(global\s+)?using\s+System\s*;')
    foreach ($name in $bclNames) {
        if ([regex]::Matches($code, "(?<!System\.)\b$([regex]::Escape($name))\b").Count -eq 0) {
            continue
        }
        if ($hasUsingSystem) {
            continue
        }
        $rel = $_.FullName.Substring(([string]$pkg).Length).TrimStart("\", "/")
        $hygieneIssues.Add("$rel uses $name without using System; or System.$name")
    }
}
if ($hygieneIssues.Count -eq 0) {
    Assert-True $true "unqualified StringComparison / StringComparer / StringSplitOptions / IDisposable require using System"
} else {
    foreach ($issue in $hygieneIssues) {
        Assert-True $false $issue
    }
}

# Former roots must be gone
Assert-True (-not (Test-Path "Assets/_Resources/Script/UI_Responsible")) `
    "former UI_Responsible folder removed"
Assert-True (-not (Test-Path "Assets/_Resources/Script/Editor/ResponsiveUIEditor.cs")) `
    "former Editor ResponsiveUIEditor.cs removed"

Write-Host ""
Write-Host "Result: $passed passed, $failed failed, $skipped skipped (host-project assets)" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Red" })
if ($failed -gt 0) { exit 1 }
exit 0
