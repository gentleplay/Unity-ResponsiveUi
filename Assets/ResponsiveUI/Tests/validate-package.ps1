# Structural + packaging gate for Assets/ResponsiveUI (no Unity required).
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\validate-package.ps1

$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI; docs/sources contain → ◀ ▶.
$PSDefaultParameterValues['Get-Content:Encoding'] = 'UTF8'
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
    "LICENSE.md",
    "package.json",
    "package.json.meta",
    "Docs\README.md",
    "Docs\GETTING-STARTED.md",
    "Docs\CONCEPTS.md",
    "Docs\COMPONENTS.md",
    "Docs\SETTINGS.md",
    "Docs\PLATFORMS.md",
    "Docs\STRUCTURE.md",
    "Docs\PRODUCTION.md",
    "Docs\TESTING.md",
    "Docs\DEVICE-TESTING.md",
    "Docs\MIGRATION.md",
    "Runtime\com.futuredigitalplus.responsiveui.asmdef",
    "Runtime\AssemblyInfo.cs",
    "Runtime\AssemblyInfo.cs.meta",
    "Runtime\ResponsiveDevice.cs",
    "Runtime\ResponsiveUiHandler.cs",
    "Runtime\ResponsiveActiveObjects.cs",
    "Runtime\ResponsiveLayoutEvents.cs",
    "Runtime\IResponsiveUiClient.cs",
    "Runtime\ResponsiveUiController.cs",
    "Runtime\ResponsiveTwoSideUiTweenController.cs",
    "Runtime\ResponsiveScrollView.cs",
    "Runtime\ResponsiveTextBanner.cs",
    "Runtime\ResponsiveUI.cs",
    "Runtime\ResponsiveUiHingeGuide.cs",
    "Runtime\ResponsiveCamera.cs",
    "Runtime\ResponsiveCameraState.cs",
    "Runtime\ResponsiveCameraLayout.cs",
    "Runtime\ResponsiveCameraSpriteFit.cs",
    "Runtime\ResponsiveCameraFocus.cs",
    "Runtime\ResponsiveRectState.cs",
    "Runtime\ResponsiveWidgetLayout.cs",
    "Runtime\ResponsiveWidgetSlotResolver.cs",
    "Runtime\ResponsiveScrollState.cs",
    "Runtime\ResponsiveScrollLayout.cs",
    "Runtime\ResponsiveScrollDriver.cs",
    "Runtime\ResponsiveUiApplyOrder.cs",
    "Editor\com.futuredigitalplus.responsiveui.editor.asmdef",
    "Editor\AssemblyInfo.cs",
    "Editor\AssemblyInfo.cs.meta",
    "Editor\Banner.meta",
    "Editor\Camera.meta",
    "Editor\Hub.meta",
    "Editor\Package.meta",
    "Editor\Scroll.meta",
    "Editor\Simulator.meta",
    "Editor\Widgets.meta",
    "SharedEditor\ResponsiveUiEditorMenus.cs",
    "Editor\Camera\ResponsiveCameraEditor.cs",
    "Editor\Camera\ResponsiveCameraStateDrawer.cs",
    "Editor\Camera\ResponsiveCameraLayoutDrawer.cs",
    "Editor\Camera\ResponsiveCameraSync.cs",
    "Editor\Camera\ResponsiveCameraEditorUtil.cs",
    "Editor\Package\ResponsiveUiPathUtil.cs",
    "Editor\Package\ResponsiveUiPackageExport.cs",
    "Editor\Package\ResponsiveUiPackageSetup.cs",
    "Editor\Package\ResponsiveUiSampleSetup.cs",
    "Editor\Package\ResponsiveUiSampleSetup.cs.meta",
    "Editor\Package\ResponsiveUiEditorSettings.cs",
    "Editor\Package\ResponsiveUiSettingsWatchers.cs",
    "Editor\Package\ResponsiveUiSettingsWatchers.cs.meta",
    "Editor\Hub\ResponsiveUIEditor.cs",
    "Editor\Simulator\ResponsiveUiHingeGuideOverlay.cs",
    "Editor\Widgets\ResponsiveUiWidgetSync.cs",
    "Editor\Widgets\ResponsiveUiEditorCaptureGuard.cs",
    "Editor\Widgets\ResponsiveRectStateEditorUtil.cs",
    "Editor\Widgets\ResponsiveRectStateDrawer.cs",
    "Editor\Widgets\ResponsiveWidgetLayoutDrawer.cs",
    "Editor\Widgets\ResponsiveTwoSideWidgetDrawer.cs",
    "Editor\Widgets\ResponsiveBackgroundDrawer.cs",
    "Editor\Hub\ResponsiveUiDetectionDrawer.cs",
    "Editor\Hub\ResponsiveUiInspectorFoldouts.cs",
    "Editor\Hub\ResponsiveUiSlotClipboard.cs",
    "Editor\Hub\ResponsiveUiSlotClipboard.cs.meta",
    "Editor\Hub\ResponsiveUiBulkCopyUtil.cs",
    "Editor\Widgets\ResponsiveActiveObjectEntryDrawer.cs",
    "Editor\Hub\ResponsiveLayoutEventsDrawer.cs",
    "Editor\Widgets\ResponsiveUiControllerEditor.cs",
    "Editor\Scroll\ResponsiveScrollViewEditor.cs",
    "Editor\Banner\ResponsiveTextBannerEditor.cs",
    "Editor\Scroll\ResponsiveScrollStateDrawer.cs",
    "Editor\Scroll\ResponsiveScrollLayoutDrawer.cs",
    "Editor\Scroll\ResponsiveScrollEditorUtil.cs",
    "Editor\Scroll\ResponsiveScrollSync.cs",
    "Editor\Simulator\ResponsiveUiSimulatorSlotFocus.cs",
    "Editor\Simulator\ResponsiveUiSimulatorLayoutResolver.cs",
    "Editor\Simulator\ResponsiveUiEditorLayoutScheduler.cs",
    "Editor\Simulator\ResponsiveUiEditorReloadGuard.cs",
    "Editor\Simulator\ResponsiveUiEditorReloadGuard.cs.meta",
    "FoldAndFlip\Editor\AssemblyInfo.cs",
    "FoldAndFlip\Editor\AssemblyInfo.cs.meta",
    "FoldAndFlip\Editor\DeviceSimulatorSelectionAdapter.cs",
    "FoldAndFlip\Editor\DeviceSimulatorSelectionAdapter.cs.meta",
    "Tests\test-design-scale.ps1",
    "Tests\test-camera-projection.ps1",
    "Tests\validate-structure.ps1",
    "Tests\validate-export-manifest.ps1",
    "Tests\run-editmode-tests.ps1",
    "Tests\Editor\com.futuredigitalplus.responsiveui.tests.editor.asmdef",
    "Tests\Editor\ResponsiveCatalogDevices.cs",
    "Tests\Editor\ResponsiveEditorTestUtil.cs",
    "Tests\Editor\ResponsiveEditorTestUtil.cs.meta",
    "Tests\Editor\ResponsiveDesignScaleTests.cs",
    "Tests\Editor\ResponsiveCameraTests.cs",
    "Tests\Editor\ResponsiveScrollLayoutTests.cs",
    "Tests\Editor\ResponsiveScrollSyncTests.cs",
    "Tests\Editor\ResponsiveScrollSyncTests.cs.meta",
    "Tests\Editor\ResponsiveUiProductionContractsTests.cs",
    "Tests\Editor\ResponsiveUiProductionContractsTests.cs.meta",
    "Tests\Editor\ResponsiveSlotSeedTests.cs",
    "Tests\Editor\ResponsiveSlotSeedTests.cs.meta",
    "Tests\Editor\ResponsiveActiveObjectTests.cs",
    "Tests\Editor\ResponsiveActiveObjectTests.cs.meta",
    "Tests\Editor\ResponsiveWidgetTargetActiveTests.cs",
    "Tests\Editor\ResponsiveWidgetTargetActiveTests.cs.meta",
    "Tests\Editor\ResponsiveLayoutEventsTests.cs",
    "Tests\Editor\ResponsiveUiHubTests.cs",
    "Tests\Editor\ResponsiveTextBannerTests.cs",
    "Tests\Editor\ResponsiveUiInspectorChromeTests.cs",
    "Tests\Editor\ResponsiveUiInspectorChromeTests.cs.meta",
    "Tests\Editor\ResponsiveDeviceSimulatorLayoutTests.cs",
    "Tests\Editor\ResponsiveUiHingeGuideTests.cs",
    "Tests\Editor\ResponsiveTwoSideUiControllerTests.cs",
    "Tests\Editor\ResponsiveWidgetSlotResolverTests.cs",
    "Tests\Editor\ResponsiveUiSimulatorIntegrationTests.cs",
    "Tests\Editor\ResponsiveUiSimulatorIntegrationTests.cs.meta",
    "Tests\Editor\ResponsiveUiPackageExportTests.cs",
    "Tests\Editor\ResponsiveUiPackageExportTests.cs.meta",
    "Tests\Editor\ResponsivePerformanceReliabilityTests.cs",
    "Tests\Editor\ResponsivePerformanceReliabilityTests.cs.meta",
    "Samples\README.md"
)

foreach ($rel in $required) {
    Assert-True (Test-Path (Join-Path $pkg $rel)) "exists $rel"
}

$export = Get-Content (Join-Path $pkg "Editor\Package\ResponsiveUiPackageExport.cs") -Raw
Assert-True ($export -match "ExportPackage") "export uses AssetDatabase.ExportPackage"
Assert-True ($export -match "ExportUnityPackageBatch") "batch export entry exists"
Assert-True ((Get-Content (Join-Path $pkg "Tests\export-production.ps1") -Raw) -match "FDPlus\.ResponsiveUI\.Editor\.ResponsiveUiPackageExport\.ExportUnityPackageBatch") "batch export uses namespaced executeMethod"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\ResponsiveUiHandler.cs") -Raw) -match "namespace FDPlus\.ResponsiveUI") "runtime handler is namespaced"
Assert-True ((Get-Content (Join-Path $pkg "package.json") -Raw) -match '"name":\s*"com.futuredigitalplus.responsiveui"') "package.json kit id"
Assert-True ($export -match "ResponsiveUiEditorMenus.ExportUnityPackage") "export menu wired"
Assert-True ($export -match "CollectProductionAssetPaths") "export collects production asset paths"
Assert-True ($export -match '"package.json"') "production export includes package.json identity"
Assert-True ($export -notmatch "CHANGELOG") "production export does not include CHANGELOG.md"
Assert-True ($export -match "com\.futuredigitalplus\.responsiveui\.export") "export prefs branded"

$menus = Get-Content (Join-Path $pkg "SharedEditor\ResponsiveUiEditorMenus.cs") -Raw
Assert-True ($menus -match "Create Sample Canvas") "menu path includes Create Sample Canvas"
Assert-True ($menus -match "Export Unity Package") "menu path includes Export Unity Package"
Assert-True ($menus -match "Phone & Pad Catalog") "menu path includes Phone & Pad Catalog"
Assert-True ($menus -match "Fold & Flip Catalog") "menu path includes Fold & Flip Catalog"
Assert-True ($menus -match "Enable Phone & Pad Simulator Skins") "menu path includes Enable Phone & Pad Simulator Skins"
Assert-True ($menus -match "Import Photoreal Frames \(Local\)") "menu path includes Import Photoreal Frames"
Assert-True ($menus -match "Clear Local Photoreal Frames") "menu path includes Clear Local Photoreal Frames"
Assert-True ($menus -match 'Root = "Responsive UI"') "single Responsive UI menu root"
Assert-True ($menus -notmatch 'Root = "Phone & Pad"|Root = "Fold & Flip"') "no former top-level menu roots in ResponsiveUiEditorMenus"
Assert-True ($menus -match "iPhone Duo Settings") "menu path includes iPhone Duo Settings"
Assert-True (-not (Test-Path (Join-Path $pkg "FoldAndFlip\Editor\FoldableEditorMenus.cs")) -and -not (Test-Path (Join-Path $pkg "PhonePad\Editor\PhonePadEditorMenus.cs"))) "modules use the shared menu paths (no mirrored copies)"

$runtimeAsm = Get-Content (Join-Path $pkg "Runtime\com.futuredigitalplus.responsiveui.asmdef") -Raw | ConvertFrom-Json
Assert-True ($runtimeAsm.name -eq "com.futuredigitalplus.responsiveui") "runtime asmdef package id"
Assert-True ($runtimeAsm.rootNamespace -eq "FDPlus.ResponsiveUI") "runtime rootNamespace FDPlus.ResponsiveUI"
Assert-True ($runtimeAsm.references -contains "com.futuredigitalplus.foldandflip") "runtime refs foldandflip"
Assert-True ($runtimeAsm.references -contains "UnityEngine.UI") "runtime refs UnityEngine.UI"
Assert-True ($runtimeAsm.references -contains "Unity.TextMeshPro") "runtime refs TextMeshPro"

$editorAsm = Get-Content (Join-Path $pkg "Editor\com.futuredigitalplus.responsiveui.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True ($editorAsm.name -eq "com.futuredigitalplus.responsiveui.editor") "editor asmdef package id"
Assert-True ($editorAsm.rootNamespace -eq "FDPlus.ResponsiveUI.Editor") "editor rootNamespace FDPlus.ResponsiveUI.Editor"
Assert-True ($editorAsm.references -contains "com.futuredigitalplus.responsiveui") "editor refs runtime"
Assert-True ($editorAsm.references -contains "com.futuredigitalplus.foldandflip.editor") "editor refs foldandflip.editor"
Assert-True ($editorAsm.references -contains "com.futuredigitalplus.phonepad.simulator.editor") "editor refs phonepad.simulator.editor"

$testAsm = Get-Content (Join-Path $pkg "Tests\Editor\com.futuredigitalplus.responsiveui.tests.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True ($testAsm.references -contains "com.futuredigitalplus.foldandflip") "editor tests ref foldandflip"
Assert-True ($testAsm.references -contains "com.futuredigitalplus.foldandflip.editor") "editor tests ref fold catalog"
Assert-True ($testAsm.references -contains "com.futuredigitalplus.phonepad.simulator.editor") "editor tests ref phone catalog"
Assert-True ($testAsm.references -contains "com.futuredigitalplus.responsiveui.editor") "editor tests ref layout editor utils"

$setup = Get-Content (Join-Path $pkg "Editor\Package\ResponsiveUiPackageSetup.cs") -Raw
Assert-True ($setup -match "com\.futuredigitalplus\.responsiveui") "setup package id"
Assert-True ($setup -match "futuredigitalplus") "setup mentions owner"
Assert-True ($setup -match "BuildValidationReport") "setup includes FoldAndFlip validation report"
Assert-True ($setup -match "PhonePadDeviceCatalog") "setup checks PhonePad catalog"
Assert-True ($setup -match "FoldAndFlip") "setup requires nested FoldAndFlip"
Assert-True ($setup -match "PhonePad") "setup requires nested PhonePad"
Assert-True ($setup -match "IphoneDuo/Runtime/com\.futuredigitalplus\.iphoneduo\.asmdef") "setup requires the iPhone Duo module"
Assert-True ($setup -match "MinimumUnityMajor\s*=\s*2023") "setup enforces Unity 2023+ floor"
Assert-True ($setup -match "RequireSupportedUnityVersion") "setup validates Unity version"
Assert-True ($setup -notmatch "foldandflip\.setup\.optin|phonepad\.setup\.simulator") "setup has no module-pref suppression"
Assert-True ($setup -match "InitializeOnLoad") "setup owns first-run wizard"
Assert-True ($setup -match "OfferFirstRunSetup|First-time setup") "setup has first-run dialog"
$dialogIdx = $setup.IndexOf("DisplayDialogComplex")
$prefIdx = $setup.LastIndexOf("EditorPrefs.SetBool(PrefKey")
Assert-True ($dialogIdx -ge 0 -and $prefIdx -gt $dialogIdx) "first-run pref is set after the dialog"
Assert-True ($setup -match "PlayerSettings\.productGUID") "first-run pref is keyed per project"
Assert-True ($setup -match "LICENSE\.md") "setup validates LICENSE.md"
Assert-True ($setup -notmatch "CHANGELOG\.md") "setup does not require CHANGELOG.md"
Assert-True ($setup -notmatch "PhonePadDeviceCatalogWindow\.Open|FoldableDeviceCatalogWindow\.Open") "setup does not duplicate device menu Open handlers"

$foldSetup = Get-Content (Join-Path $pkg "FoldAndFlip\Editor\FoldablePackageSetup.cs") -Raw
Assert-True ($foldSetup -match 'Version = "1\.0\.0"') "FoldAndFlip module version aligned to 1.0.0"
Assert-True ($foldSetup -notmatch "InitializeOnLoad") "FoldAndFlip setup has no separate first-run"
Assert-True ($foldSetup -notmatch "setup\.optin|EditorPrefs\.SetBool") "FoldAndFlip setup has no module setup pref"

$phoneSetup = Get-Content (Join-Path $pkg "PhonePad\Editor\PhonePadPackageSetup.cs") -Raw
Assert-True ($phoneSetup -match 'Version = "1\.0\.0"') "PhonePad module version aligned to 1.0.0"
Assert-True ($phoneSetup -notmatch "InitializeOnLoad") "PhonePad setup has no separate first-run"
Assert-True ($phoneSetup -notmatch "setup\.simulator|EditorPrefs\.SetBool") "PhonePad setup has no module setup pref"

$readme = Get-Content (Join-Path $pkg "README.md") -Raw
Assert-True ($readme -match "\*\*Version:\*\* 1\.0\.0") "root README version 1.0.0"
Assert-True ($readme -match "Fold Cover") "root README lists Fold Cover"
Assert-True ($readme -notmatch "Fold Cover Phone") "root README does not use stale Fold Cover Phone"
Assert-True ($readme -match "Disabled Cover / HalfOpened / Pad falls back to Phone") "root README documents controller Phone fallback"
Assert-True ($readme -match "The Target stays as authored") "root README documents widget Targets stay authored"
Assert-True ($readme -match "slot \*\*Enabled\*\* chooses which layout") "root README documents controller Enabled chooses layout"
Assert-True ($readme -match "Cover / Fold / Flip slot Disabled → Phone design canvas") "root README Design Scale documents Phone fallback"
Assert-True ($readme -match "Toggle \*\*Enabled\*\* on any slot in the live fallback chain") "root README LivePush previews resolved layout"
Assert-True ($readme -match "no code in the host script") "root README documents ResponsiveScrollView needs no host code"
Assert-True ($readme -match "First-time setup") "root README documents first-run wizard"
Assert-True ($readme -match "Background Cover Size") "root README documents Background Cover Size"
Assert-True ($readme -match "Design UI Size") "root README documents camera Design UI Size"
Assert-True ($readme -match "Docs/PLATFORMS\.md") "root README links platforms / future adapter doc"
Assert-True ($readme -match "Leave Hub empty") "root README documents empty Hub on prefabs"
Assert-True ($readme -match "ResponsiveTextBanner") "root README documents text banners"
Assert-True ($readme -match "Focus Target") "root README documents camera Focus Target"
Assert-True ($readme -match "Editor/Package") "root README documents production Editor folders"

$foldReadme = Get-Content (Join-Path $pkg "FoldAndFlip\README.md") -Raw
Assert-True ($foldReadme -notmatch "Fold & Flip →") "FoldAndFlip README has no former menu paths"
Assert-True ($foldReadme -match "Responsive UI") "FoldAndFlip README points at Responsive UI menus"

$prod = Get-Content (Join-Path $pkg "Docs\PRODUCTION.md") -Raw
$structureDoc = Get-Content (Join-Path $pkg "Docs\STRUCTURE.md") -Raw
# FEATURES.md was split into CONCEPTS.md (the model), COMPONENTS.md (per-component reference) and
# SETTINGS.md (Project Settings); a few claims landed in STRUCTURE.md / PRODUCTION.md. Each claim
# below is asserted against the doc that now carries it.
$concepts = Get-Content (Join-Path $pkg "Docs\CONCEPTS.md") -Raw
$components = Get-Content (Join-Path $pkg "Docs\COMPONENTS.md") -Raw
$settingsDoc = Get-Content (Join-Path $pkg "Docs\SETTINGS.md") -Raw
Assert-True ($settingsDoc -match "Background Cover Size") "SETTINGS documents Background Cover Size"
Assert-True ($settingsDoc -match "Follow Design Canvas Reference") "SETTINGS documents Follow Design Canvas"
Assert-True ($components -match "Override Background Size") "COMPONENTS documents per-component background Override"
Assert-True ($structureDoc -match "## Inspector slot contract") "STRUCTURE documents the shared widget/camera/scroll Inspector slots"
Assert-True ($components -match "Does not hide the widget Target") "COMPONENTS documents widget Enabled does not SetActive the Target"
Assert-True ($structureDoc -match "Copy / Paste / Reset") "STRUCTURE documents slot Copy / Paste / Reset"
Assert-True ($components -match "(?m)^## Active Objects") "COMPONENTS documents per-slot Active Objects"
Assert-True ($components -match "do \*\*not\*\* list a widget Target in Active Objects") "COMPONENTS documents widget Targets stay off Active Objects"
Assert-True ($components -match "ApplyLayout") "COMPONENTS documents hub cover ApplyLayout"
Assert-True ($components -match "notifyAllLayoutClients") "COMPONENTS documents Simulator canvas-ready notify-all"
Assert-True ($components -match "Match Mode") "COMPONENTS documents background Match Mode"
Assert-True ($components -match "\*\*Stretch\*\*") "COMPONENTS documents Stretch Match Mode"
Assert-True ($components -match "ResponsiveUiController") "COMPONENTS documents the Canvas controller"
Assert-True ($concepts -match "IResponsiveUiClient") "CONCEPTS documents hub clients"
Assert-True ($structureDoc -match "ResponsiveUiHub\.Resolve") "STRUCTURE documents Canvas hub resolve"
Assert-True ($components -match "Leave Hub empty") "COMPONENTS documents empty Hub on prefabs for Play Mode Instantiate"
Assert-True ($concepts -match "OnTransformParentChanged") "CONCEPTS documents parent-change hub rebind"
Assert-True ($components -match "ResponsiveTwoSideUiTweenController") "COMPONENTS lists two-side tween as a hub client"
Assert-True ($components -match "ResponsiveTextBanner") "COMPONENTS documents ResponsiveTextBanner"
Assert-True ($components -match '## `ResponsiveTextBanner`') "COMPONENTS has a ResponsiveTextBanner section"
Assert-True ($components -match "Assign \*\*Banner\*\* and \*\*Text\*\*") "COMPONENTS documents the text banner Inspector"
Assert-True ($structureDoc -match "Editor/Package") "STRUCTURE documents production Editor folders"
Assert-True ($structureDoc -match "ForEachNewRow") "STRUCTURE documents ForEachNewRow"
Assert-True ($components -match "Right Middle") "COMPONENTS documents Right Middle banner alignment"
Assert-True ($components -match "Left Middle") "COMPONENTS documents Left Middle banner alignment"
Assert-True ($components -match "Center Middle") "COMPONENTS documents Center Middle banner alignment"
Assert-True ($components -match "Left Up") "COMPONENTS documents Left Up banner alignment"
Assert-True ($components -notmatch "Right Center") "COMPONENTS does not use Right Center banner alignment"
Assert-True ($components -notmatch "Left Center") "COMPONENTS does not use Left Center banner alignment"
Assert-True ($components -notmatch "Center Center") "COMPONENTS does not use Center Center banner alignment"
Assert-True ($components -match "SetAlignment") "COMPONENTS documents SetAlignment"
Assert-True ($components -match "writes the Alignment pivot") "COMPONENTS documents Alignment writing pivot"
Assert-True ($components -notmatch "first hub in the scene") "COMPONENTS does not fall back to a random scene hub"
Assert-True ($structureDoc -match "InspectorSectionKey") "STRUCTURE documents per-component Inspector foldout keys"
Assert-True ($structureDoc -match "NestedContentRect") "STRUCTURE documents nested foldout bodies under titles"
Assert-True ($structureDoc -match "DrawNestedContent") "STRUCTURE documents shared nested property-drawer bodies"
Assert-True ($structureDoc -match "DrawGuiFoldout") "STRUCTURE documents shared GUILayout foldout bodies"
Assert-True ($structureDoc -match "DrawHubField") "STRUCTURE documents the shared Hub field"
Assert-True ($structureDoc -match "DrawFoldout") "STRUCTURE documents foldout arrows without a second indent"
Assert-True ($prod -match "DrawSlotSection") "PRODUCTION documents tinted slot sections"
Assert-True ($structureDoc -match "ColoredFoldoutSection") "STRUCTURE documents tinted Inspector sections"
Assert-True ($structureDoc -match "EnsureHubField") "STRUCTURE documents Inspector hub auto-assign"
Assert-True ($components -match '`ResponsiveCamera` does \*\*not\*\* use that size') "COMPONENTS states cameras do not use Background Cover Size"
Assert-True ($components -match "\*\*Focus Target\*\* \| A world Transform to keep framed") "COMPONENTS documents per-slot camera Focus Target"
Assert-True ($components -match '\*\*Focus Bounds\*\* \| A HUD `RectTransform`') "COMPONENTS documents per-slot camera Focus Bounds"
Assert-True ($components -match "never recapture Bounds Layout back into slots") "COMPONENTS states Simulator Apply does not recapture Focus Bounds into slots"
Assert-True ($components -match "with the \*\*widget\*\* canvas ratio") "COMPONENTS states Focus Bounds Apply uses the widget canvas ratio"
Assert-True ($components -match "Apply write slot . HUD") "COMPONENTS states hub and Simulator Apply write slot to HUD only"
Assert-True ($components -notmatch "row \*\*Focus Bounds\*\*") "COMPONENTS does not keep Focus Bounds on the camera row"
Assert-True ($prod -match "resolves to the built-in form size in player builds") "PRODUCTION documents locked camera player fallback"
Assert-True ($components -match "Create Sample Canvas") "COMPONENTS documents Create Sample Canvas"
Assert-True ($prod -match "Android players") "PRODUCTION documents Fold/Flip Android player scope"
Assert-True ($prod -match "iPhones and iPads stay Phone / Pad") "PRODUCTION scopes Phone/Pad-only to non-Duo iPhones / iPads"
Assert-True ($prod -match "Windows / macOS / Linux / WebGL use Wide Screen only") "PRODUCTION scopes Standalone / WebGL players to Wide Screen"
Assert-True ($prod -match "iPhone Duo") "PRODUCTION documents the iPhone Duo hinge bridge"
Assert-True ($prod -match "UIHingeInteraction") "PRODUCTION names the Apple hinge API"
Assert-True ($prod -match "ShouldPreserveHalfOpened") "PRODUCTION keeps HalfOpened across a missing FoldingFeature"
Assert-True ($structureDoc -match "RequestSelectionPublish") "STRUCTURE documents the single Simulator selection publisher"
Assert-True ($structureDoc -match "ResponsiveUiEditorLayoutScheduler") "STRUCTURE documents the canvas-ready editor scheduler"
Assert-True ($prod -match "validate-structure\.ps1") "PRODUCTION documents the structural validation gate"
Assert-True ($prod -match "export-manifest") "PRODUCTION documents the export manifest gate"

$platforms = Get-Content (Join-Path $pkg "Docs\PLATFORMS.md") -Raw
Assert-True ($platforms -match "IsRuntimeSupported") "PLATFORMS documents IsRuntimeSupported gate"
Assert-True ($platforms -match "MapFoldableProduct") "PLATFORMS documents MapFoldableProduct contract"
Assert-True ($platforms -match "UNITY_IOS") "PLATFORMS documents the UNITY_IOS gate"
Assert-True ($platforms -match "Android player") "PLATFORMS documents Android player fold"
Assert-True ($platforms -match "future iOS adapter|iOS adapter") "PLATFORMS documents the iOS adapter"
Assert-True ($platforms -match "iPhone19,4") "PLATFORMS documents the iPhone Duo hardware gate"
Assert-True ($platforms -match "iOS 27\.1 SDK") "PLATFORMS documents the Xcode / iOS 27.1 SDK requirement"
Assert-True ($platforms -notmatch "ApplyTargetActive") "PLATFORMS does not document controller exact-slot Target hide"
Assert-True ($platforms -match "Flip Cover Landscape starts Disabled") "PLATFORMS documents Flip Cover Landscape seed Disabled"

$license = Get-Content (Join-Path $pkg "LICENSE.md") -Raw
Assert-True ($license -match "Permission is hereby granted") "LICENSE looks like MIT"
Assert-True ($license -match "Import Photoreal Frames|Device Frames Media|third-party") "LICENSE notes local photoreal frames are not MIT artwork"

Assert-True ($prod -match "DeviceOverlays") "PRODUCTION excludes PhonePad DeviceOverlays"
Assert-True ($prod -match "Import Photoreal Frames") "PRODUCTION documents optional local photoreal import"
Assert-True ($prod -match "Background Cover Size") "PRODUCTION mentions Background Cover Size before player builds"
Assert-True ($prod -match "best-effort code contracts") "PRODUCTION does not treat empty fold matrix as a kit ship gate"
Assert-True ($prod -match "Android players") "PRODUCTION documents Fold/Flip Android player scope"
Assert-True ($prod -match "ResponsiveTextBanner") "PRODUCTION documents text banners as hub clients"
Assert-True ($prod -match "ForEachNewRow") "PRODUCTION documents empty Inspector list + rows"
Assert-True ($prod -match "ColoredFoldoutSection") "PRODUCTION documents shared Inspector section chrome"
Assert-True ($prod -match "FieldLabelWidth") "PRODUCTION documents the shared Inspector value column"
Assert-True ($prod -match "NestedContentRect") "PRODUCTION documents nested foldout bodies under titles"
Assert-True ($prod -match "DrawHubField") "PRODUCTION documents the shared Hub field"
Assert-True ($prod -match "DrawFoldout") "PRODUCTION documents foldout arrows without a second indent"
Assert-True ($prod -match "Editor/Package") "PRODUCTION documents production Editor folders"
Assert-True ($prod -match "validate-export-manifest") "PRODUCTION documents the export-manifest gate"
Assert-True ($prod -match "run-editmode-tests") "PRODUCTION documents the EditMode runner"
Assert-True ($prod -match "structural") "PRODUCTION documents the structural gate"
Assert-True ($prod -match "Disabled Cover / HalfOpened slots fall back to Phone") "PRODUCTION documents Cover off reuses Phone"
Assert-True ($prod -match "the widget Target stays as authored") "PRODUCTION documents widget Targets stay authored"
Assert-True ($prod -match "banner GameObject inactive") "PRODUCTION documents hiding banners before save"

Assert-True ($structureDoc -match "Editor/Package") "STRUCTURE documents Editor/Package"
Assert-True ($structureDoc -match "Editor/Hub") "STRUCTURE documents Editor/Hub"
Assert-True ($structureDoc -match "Editor/Simulator") "STRUCTURE documents Editor/Simulator"
Assert-True ($structureDoc -match "RequestSelectionPublish") "STRUCTURE documents the single Simulator publisher"
Assert-True ($structureDoc -match "ResponsiveUiEditorLayoutScheduler") "STRUCTURE documents the editor layout scheduler"
Assert-True ($structureDoc -match "InspectorSectionKey") "STRUCTURE documents per-component Inspector foldout keys"
Assert-True ($structureDoc -match "ColoredFoldoutSection") "STRUCTURE documents tinted Inspector sections"
Assert-True ($structureDoc -match "FieldLabelWidth") "STRUCTURE documents the shared Inspector value column"
Assert-True ($structureDoc -match "same arrow X") "STRUCTURE documents boxed and unboxed section arrows sharing X"
Assert-True ($structureDoc -match "DrawFoldout") "STRUCTURE documents foldout arrows without a second indent"
Assert-True ($structureDoc -match "ForEachNewRow") "STRUCTURE documents ForEachNewRow"
Assert-True ($structureDoc -match "Disabled Cover / HalfOpened / Pad slots fall back to Phone") "STRUCTURE documents controller Phone fallback"
Assert-True ($structureDoc -match "Widget Targets stay as authored") "STRUCTURE documents widget Targets stay authored"
Assert-True ($structureDoc -match "IsSeedDisabledSlot") "STRUCTURE names IsSeedDisabledSlot"
Assert-True ($structureDoc -match "Cover/HalfOpened → Phone fallback") "STRUCTURE documents the shared slot resolver fallback"

Assert-True (Test-Path (Join-Path $pkg "FoldAndFlip\Runtime\com.futuredigitalplus.foldandflip.asmdef")) "nested FoldAndFlip runtime asmdef"
Assert-True (Test-Path (Join-Path $pkg "PhonePad\Editor\com.futuredigitalplus.phonepad.simulator.editor.asmdef")) "nested PhonePad editor asmdef"
$assetsDir = Split-Path $pkg -Parent
Assert-True (-not (Test-Path (Join-Path $assetsDir "FoldAndFlipResponsiveKit"))) "former FoldAndFlipResponsiveKit root removed"
Assert-True (-not (Test-Path (Join-Path $assetsDir "PhonePadSimulatorKit"))) "former PhonePadSimulatorKit root removed"

$export = Get-Content (Join-Path $pkg "Editor\Package\ResponsiveUiPackageExport.cs") -Raw
Assert-True ($export -match "FoldAndFlip") "export includes FoldAndFlip module"
Assert-True ($export -match "PhonePad") "export includes PhonePad module"
Assert-True ($export -match '"IphoneDuo"') "export includes the iPhone Duo module"
Assert-True ($export -match "IsExcludedExportPath|\.device") "export excludes generated PhonePad devices"
Assert-True ($export -match "DeviceOverlays") "export excludes generated PhonePad DeviceOverlays"
Assert-True ($export -match 'EndsWith\("/PhonePad/Editor/Devices/DeviceOverlays"') "export excludes empty DeviceOverlays folder asset"
Assert-True ($export -notmatch "FoldableEditorMenus\.ExportUnityPackage") "no Fold & Flip export menu alias"
Assert-True ($export -notmatch "PhonePadEditorMenus\.ExportUnityPackage") "no Phone & Pad export menu alias"

$settings = Get-Content (Join-Path $pkg "Editor\Package\ResponsiveUiEditorSettings.cs") -Raw
Assert-True ($settings -match "ApplyCatalogProfile") "settings can apply catalog profile"
Assert-True ($settings -match "GetBackgroundArtSize") "settings expose background cover size from catalog"
Assert-True ($settings -match "Reference Device|profileId") "settings expose reference device"

$editModeRunner = Get-Content (Join-Path $here "run-editmode-tests.ps1") -Raw
Assert-True ($editModeRunner -match "Start-Process[\s\S]*-Wait[\s\S]*-PassThru") `
    "EditMode runner waits for Unity and reads a reliable process exit code"
Assert-True ($editModeRunner -match 'Remove-Item[\s\S]*\$results') `
    "EditMode runner removes stale test results before launch"
Assert-True ($editModeRunner -match "Aborting batchmode due to fatal error|another Unity instance is running") `
    "EditMode runner recognizes Unity fatal startup failures"
Assert-True ($editModeRunner -match '-not \$hasResults') `
    "EditMode runner fails when Unity produces no test results"

# Nested gates must not share a stdout pipe with this process. `& powershell -File`
# deadlocks once child PASS spam fills the OS pipe buffer (seen mid FoldAndFlip audit).
function Invoke-NestedGate([string]$scriptPath) {
    $outFile = Join-Path ([IO.Path]::GetTempPath()) ("rui-gate-{0}-out.log" -f [guid]::NewGuid().ToString("n"))
    $errFile = Join-Path ([IO.Path]::GetTempPath()) ("rui-gate-{0}-err.log" -f [guid]::NewGuid().ToString("n"))
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
Write-Host "=== Nested gates ===" -ForegroundColor Cyan
if ((Invoke-NestedGate (Join-Path $here "validate-structure.ps1")) -ne 0) { $failed++ }
if ((Invoke-NestedGate (Join-Path $here "test-design-scale.ps1")) -ne 0) { $failed++ }
if ((Invoke-NestedGate (Join-Path $here "test-camera-projection.ps1")) -ne 0) { $failed++ }
if ((Invoke-NestedGate (Join-Path $pkg "FoldAndFlip\Tests\validate-package.ps1")) -ne 0) { $failed++ }
if ((Invoke-NestedGate (Join-Path $pkg "IphoneDuo\Tests\validate-package.ps1")) -ne 0) { $failed++ }
if ((Invoke-NestedGate (Join-Path $pkg "PhonePad\Tests\validate-package.ps1")) -ne 0) { $failed++ }

Write-Host ""
Write-Host "=== Export + EditMode gates ===" -ForegroundColor Cyan
if ((Invoke-NestedGate (Join-Path $here "validate-export-manifest.ps1")) -ne 0) { $failed++ }
if ($env:RESPONSIVEUI_RUN_EDITMODE -eq "1") {
    if ((Invoke-NestedGate (Join-Path $here "run-editmode-tests.ps1")) -ne 0) { $failed++ }
} else {
    Write-Host "SKIP  EditMode (set RESPONSIVEUI_RUN_EDITMODE=1 to run Unity tests from this gate)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "Package gate extras: $passed passed locally; nested scripts reported above." -ForegroundColor Cyan
if ($failed -gt 0) { exit 1 }
exit 0
