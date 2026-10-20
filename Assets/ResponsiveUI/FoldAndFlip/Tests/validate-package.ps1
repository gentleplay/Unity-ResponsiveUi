# Static structural + layout-math validation for Assets/ResponsiveUI/FoldAndFlip.
# This packaging gate is not proof of Android runtime behavior. Complete the
# physical-device release matrix before making production compatibility claims.
# Run: powershell -File Assets/ResponsiveUI/FoldAndFlip/Tests/validate-package.ps1

$ErrorActionPreference = "Stop"
# Windows PowerShell 5.1 reads BOM-less UTF-8 as ANSI; docs/sources contain → ◀ ▶.
$PSDefaultParameterValues['Get-Content:Encoding'] = 'UTF8'
$pkg = Resolve-Path (Join-Path $PSScriptRoot "..")
# FoldAndFlip → ResponsiveUI → Assets → project root
$projRoot = Resolve-Path (Join-Path $pkg "..\..\..")
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

# Host-project assets (scenes, prefabs) are not part of the package. In a project that does not
# have them the block that reads them is skipped, not failed; the summary reports the count.
function Skip-Host([string]$msg) {
    Write-Host "SKIP  $msg" -ForegroundColor Yellow
    $script:skipped++
}

Write-Host "Package root: $pkg"
Write-Host "=== Package structure ===" -ForegroundColor Cyan
$required = @(
    "README.md",
    "Runtime\com.futuredigitalplus.foldandflip.asmdef",
    "Runtime\FoldableLayoutState.cs",
    "Runtime\FoldablePostureDetector.cs",
    "Runtime\FoldablePostureSource.cs",
    "Runtime\FoldableProxy.cs",
    "Runtime\FoldableSimulator.cs",
    "Runtime\FoldableDeviceKind.cs",
    "Runtime\FoldableProductState.cs",
    "Runtime\FoldableProductStateResolver.cs",
    "Runtime\FoldableCoverHeuristics.cs",
    "Runtime\FoldableEnums.cs",
    "Editor\com.futuredigitalplus.foldandflip.editor.asmdef",
    "Editor\FoldableDeviceCatalogWindow.cs",
    "Editor\FoldableDeviceCatalogIO.cs",
    "Editor\FoldableDeviceOverlayUtil.cs",
    "Editor\FoldablePackageSetup.cs",
    "Editor\FoldablePostureSimulatorWindow.cs",
    "Editor\FoldableSetupMenu.cs",
    "Editor\FoldableDeviceSimulatorPlugin.cs",
    "Editor\FoldableDeviceProfileNavigationPlugin.cs",
    "Editor\FoldableSimulatorScreenCommit.cs",
    "Editor\FoldableEditorSessionWatcher.cs",
    "Docs\DETECTION.md",
    "..\Docs\STRUCTURE.md",
    "..\Docs\PRODUCTION.md",
    "..\Docs\MIGRATION.md",
    "..\Docs\DEVICE-TESTING.md",
    "Editor\Devices\Pixel Fold-like.device",
    "Editor\Devices\Galaxy Z Flip-like.device",
    "Editor\Devices\Galaxy Z Fold6-like.device",
    "Editor\Devices\Huawei Mate X3-like.device",
    "Editor\Devices\Honor Magic V2-like.device",
    "Editor\Devices\OnePlus Open-like.device",
    "Editor\Devices\Xiaomi Mix Fold3-like.device",
    "Editor\Devices\Motorola Razr+-like.device",
    "Editor\Devices\Galaxy Z Fold7-like.device",
    "Editor\Devices\Galaxy Z Flip7-like.device",
    "Editor\Devices\Pixel 9 Pro Fold-like.device",
    "Editor\Devices\Oppo Find N5-like.device",
    "Editor\Devices\Honor Magic V3-like.device",
    "Editor\Devices\Motorola Razr 50 Ultra-like.device",
    "Editor\Devices\Huawei Mate XT-like.device",
    "Editor\Devices\Vivo X Fold3 Pro-like.device",
    "Editor\Devices\Xiaomi Mix Fold4-like.device",
    "Editor\Devices\Galaxy Z Fold8 Ultra-like.device",
    "Editor\Devices\Galaxy Z Fold8-like.device",
    "Editor\Devices\Galaxy Z Flip8-like.device",
    "Editor\Devices\Galaxy Z TriFold-like.device",
    "Editor\Devices\Galaxy Z Flip7 FE-like.device",
    "Editor\Devices\Galaxy Z Fold Special Edition-like.device",
    "Editor\Devices\Galaxy Z Flip6-like.device",
    "Editor\Devices\Galaxy Z Flip5-like.device",
    "Editor\Devices\Galaxy Z Fold5-like.device",
    "Editor\Devices\Galaxy Z Fold2-like.device",
    "Editor\Devices\iPhone Duo-like.device",
    "Runtime\Plugins\Android\Foldable.androidlib\AndroidManifest.xml",
    "Runtime\Plugins\Android\Foldable.androidlib\build.gradle",
    "Runtime\Plugins\Android\Foldable.androidlib\consumer-rules.pro",
    "Runtime\Plugins\Android\Foldable.androidlib\src\main\java\com\futuredigitalplus\foldandflip\FoldableHelper.java",
    "Tests\Editor\FoldableLayoutTests.cs",
    "Samples\FoldableSetup\README.md"
)

foreach ($rel in $required) {
    Assert-True (Test-Path (Join-Path $pkg $rel)) "exists $rel"
}

$manifest = Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\AndroidManifest.xml") -Raw
Assert-True ($manifest -notmatch "FoldableUnityActivity") "androidlib does not add an Activity"
Assert-True ($manifest -notmatch "com\.samsung") "no Samsung activity branding"

$helper = Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\src\main\java\com\futuredigitalplus\foldandflip\FoldableHelper.java") -Raw
Assert-True ($helper -match "package com\.futuredigitalplus\.foldandflip") "Java package renamed"
Assert-True ($helper -notmatch "init\(Activity") "helper exposes direct listener attach only"
Assert-True ($helper -notmatch "public static void\s+attachNativeListener\(") "helper exposes boolean attach API only"
Assert-True ($helper -match "instanceof FoldingFeature") "safe FoldingFeature parse"
Assert-True ($helper -match "androidx\.window\.layout\.FoldingFeature") "Jetpack WindowManager"
Assert-True ($helper -match "isInMultiWindowMode") "native payload exposes multi-window state"
Assert-True ($helper -match "isInPictureInPictureMode") "native payload exposes picture-in-picture state"
Assert-True ($helper -match "Never publish folding data captured for a previous Activity") "activity rebind clears stale folding data"
Assert-True ($helper -match "detachNativeListener[\s\S]{0,600}resetSessionPeakMetrics\(\)") "listener detach resets session peak metrics"
Assert-True ($helper -notmatch "private static void configure[\s\S]{0,800}resetSessionPeakMetrics\(\)") "activity rebind keeps session peak metrics"
Assert-True ($helper -match "resetSessionPeakMetrics\(\)\s*\{\s*peakMaxWidth = 0;\s*peakMaxHeight = 0;") "session peak reset clears both dimensions"

$gradle = Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\build.gradle") -Raw
Assert-True ($gradle -match "androidx\.window:window") "window dependency"
Assert-True ($gradle -match "androidx\.window:window-java") "window-java dependency"
Assert-True ($gradle -match "androidx\.window:window:1\.4\.0") "WindowManager pinned to 1.4.0"
Assert-True ($gradle -match "androidx\.window:window-java:1\.4\.0") "WindowManager Java adapter pinned to 1.4.0"
Assert-True ($gradle -match "consumerProguardFiles\s+['""]consumer-rules\.pro['""]") "consumer shrinker rules are published"
Assert-True ($gradle -match "com\.futuredigitalplus\.foldandflip") "gradle namespace owned by futuredigitalplus"
$consumerRules = Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\consumer-rules.pro") -Raw
Assert-True ($consumerRules -match "FoldableHelper") "consumer rules retain helper bridge"
Assert-True ($consumerRules -match "WindowInfoLayoutListener") "consumer rules retain Unity callback interface"
Assert-True ($consumerRules -match "FoldableWindowAreaTracker") "consumer rules retain WindowArea tracker"
Assert-True ($consumerRules -notmatch "FoldableUnityActivity") "consumer rules do not retain removed Activity"
Assert-True ($consumerRules -notmatch "keep class [^\r\n]*FoldableLayoutInfo") "payload class needs no keep rule (Unity reads a packed string)"

$proxy = Get-Content (Join-Path $pkg "Runtime\FoldableProxy.cs") -Raw
Assert-True ($proxy -match 'com\.futuredigitalplus\.foldandflip\.FoldableHelper\$WindowInfoLayoutListener') "proxy listener class"
Assert-True ($proxy -match 'isInMultiWindowMode = values\[2\] != 0') "proxy copies multi-window state"
Assert-True ($proxy -match 'isInPictureInPictureMode = values\[3\] != 0') "proxy copies picture-in-picture state"
Assert-True ($proxy -notmatch "public readonly FoldableLayoutState Layout") "proxy exposes ConsumeLatest only"
Assert-True ($proxy -match "ParseCallbackPayload") "proxy parses the Java payload in a pure, testable method"
Assert-True ($proxy -match "version != PayloadVersion") "proxy rejects payloads from another bridge version"
Assert-True ($proxy -notmatch "Mathf\.") "proxy callback avoids Unity API calls from a Java background thread"

# Removed types stay absent: no file, and no owned C# source names them.
$removedTypes = @(
    @{ Name = "FoldableModeManager"; File = "Runtime\FoldableModeManager.cs" },
    @{ Name = "LegacyFlexAliases"; File = "Runtime\LegacyFlexAliases.cs" },
    @{ Name = "FoldableLayoutApplier"; File = "Runtime\FoldableLayoutApplier.cs" },
    @{ Name = "FoldableLayoutTargets"; File = "Runtime\FoldableLayoutApplier.cs" },
    @{ Name = "FoldablePackageExport"; File = "Editor\FoldablePackageExport.cs" },
    @{ Name = "FoldablePreviewWindow"; File = "Editor\FoldablePreviewWindow.cs" },
    @{ Name = "FoldablePathUtil"; File = "Editor\FoldablePathUtil.cs" },
    @{ Name = "FoldableEditorFind"; File = "Editor\FoldableEditorFind.cs" }
)
$ownedCs = @(Get-ChildItem $pkg -Recurse -File -Filter "*.cs")
foreach ($removed in $removedTypes) {
    Assert-True (-not (Test-Path (Join-Path $pkg $removed.File))) "removed $($removed.Name) stays absent"
    $refs = @($ownedCs | Select-String -Pattern ("\b" + $removed.Name + "\b") -List)
    Assert-True ($refs.Count -eq 0) "no C# source names removed $($removed.Name)"
}
$detector = Get-Content (Join-Path $pkg "Runtime\FoldablePostureDetector.cs") -Raw
Assert-True ($detector -match 'UNITY_ANDROID && !UNITY_EDITOR') "detector Android-only native attach"
Assert-True ($detector -match "AttachNative") "Android attach is retryable"
Assert-True ($detector -match "currentActivity is null") "Android attach handles a null currentActivity"
Assert-True ($detector -match "Android bridge attach failed") "Android attach is try/catch guarded"
Assert-True ($detector -match "deviceKind = FoldableDeviceKind\.Unknown") "new detectors do not classify every Android device as foldable"
Assert-True ($detector -match "BecomeActiveOwner") "duplicate detectors do not steal ActiveInstance"
Assert-True ($detector -match "void ActivateOwner\(\)") "all owner transitions share one initialization path"
Assert-True ($detector -match "PromoteAnotherEnabledDetector") "disabling the owner promotes another enabled detector"
Assert-True ($detector -match "!candidate\.isDuplicatePersist") "active lookup skips persistent duplicates pending destruction"
Assert-True ($detector -match "!other\.isDuplicatePersist") "owner promotion skips persistent duplicates pending destruction"
Assert-True ($detector -match "EditorInitializing\?\.Invoke\(this\);[\s\S]{0,100}ApplyPlayLayout\(\);\s*ActiveInstanceChanged\?\.Invoke\(\);") "Simulator kind and Play layout initialize before ownership listeners apply"
Assert-True ($detector -match "IsRuntimeSupported") "detector runtime support: Editor, Android, or the registered posture backend"
Assert-True ($detector -match "persistAcrossScenes") "detector can persist across scenes"
Assert-True ($detector -match "DontDestroyOnLoad") "detector DontDestroyOnLoads when persistAcrossScenes"
Assert-True ($detector -match "s_Persistent") "detector keeps a persistent singleton"
Assert-True ($detector -match "isDuplicatePersist") "duplicate persist copies destroy without stealing native owner"
Assert-True ($detector -match "LayoutChanged") "detector raises LayoutChanged"
Assert-True ($detector -match "ActiveInstanceChanged") "detector raises ActiveInstanceChanged"
Assert-True ($detector -match "forceNotify: false") "detector suppresses duplicate native layout notifications"
Assert-True ($detector -match "layoutState\.ContentEquals\(lastPublishedState\)") "detector compares native layout content before notifying"
Assert-True ($detector -match "#if UNITY_ANDROID && !UNITY_EDITOR\s+void Update\(\)") "detector polling Update only compiles for Android players"
Assert-True ($detector -match "#if !UNITY_EDITOR && !UNITY_ANDROID\s+void Update\(\)") "platform backend polling Update only compiles for non-Android players"
Assert-True ($detector -match "FoldablePostureBackend\.Current\.IsSupportedDevice") "non-Android runtime support comes from the registered posture backend"
Assert-True ($detector -match "DetachBackendIfOwner\(\)") "detector detaches the platform backend"
Assert-True ($detector -notmatch "UNITY_IOS|IphoneDuo") "shared detector has no iPhone Duo code"
Assert-True ($detector -match "nativeListenerMayBeRegistered") "partial Android attachment is detached safely"
Assert-True ($detector -notmatch 'CallStatic\("stop"\);\s*helper\.CallStatic\("detachNativeListener"') "detach relies on detachNativeListener to stop tracking"
Assert-True ($detector -match "void PublishLayoutChanged\(\) =>\s*FoldablePostureSource\.RaiseIsolated\(LayoutChanged, layoutState\)") "detector publishes through the shared isolated fan-out"
Assert-True ($detector -match "previousProduct = layoutState\.productState;\s*FoldableOrientation previousOrientation = layoutState\.orientation;\s*layoutState\.CopyExact\(simulated\)") "ApplySimulatedLayout captures the previous posture before overwriting it"
Assert-True ($detector -match "ApplySimulatedLayout") "detector ApplySimulatedLayout"
Assert-True ($detector -match "SyncSimulatedLayoutState") "detector SyncSimulatedLayoutState"
Assert-True ($detector -match "SetEditorDeviceKind") "detector supports a transient Simulator DeviceKind"
Assert-True ($detector -match "ClearEditorDeviceKind") "detector restores its authored player DeviceKind"
Assert-True ($detector -match "hasEditorDeviceKind \? editorDeviceKind") "detector resolves Simulator kind without overwriting the serialized player hint"
Assert-True ($detector -match "ApplyFlatLayout") "detector ApplyFlatLayout"
Assert-True ($detector -notmatch "mainCamera|CreateTargets|ControlsCamera") "detector has no hinge camera/canvas apply APIs"
$source = Get-Content (Join-Path $pkg "Runtime\FoldablePostureSource.cs") -Raw
Assert-True ($source -match "FoldablePostureDetector\.FindActiveInstance") "posture source uses detector"
Assert-True ($source -match "FoldablePostureDetector\.ActiveInstanceChanged -= OnOwnerLifecycle") "posture source releases owner lifecycle hook when unused"
Assert-True ($source -match "ApplySimulatedLayout") "posture source ApplySimulatedLayout"
Assert-True ($source -match "GetInvocationList") "posture source isolates layout snapshots between subscribers"
Assert-True ($source -match "(?s)static void RaiseIsolated\(.*?catch \(Exception e\)") "posture source isolates LayoutChanged subscriber exceptions"
Assert-True ($source -match "static void ForwardLayout\(FoldableLayoutState layout\) =>\s*RaiseIsolated\(") "posture source forwards through the shared isolated fan-out"
$simPlugin = Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw
$simApply = Get-Content (Join-Path $pkg "Editor\FoldableSimulatorApply.cs") -Raw
Assert-True ($simPlugin -match "FoldableSimulatorApply\.Apply") "Simulator panel applies through the shared Simulator apply"
Assert-True ($simApply -match "FoldablePostureSource\.ApplySimulatedLayout") "Simulator Apply uses FoldablePostureSource"
Assert-True ($simApply -match "FoldablePostureSource\.HasActiveOwner\(\)") "Simulator accepts detector"
Assert-True ($simPlugin -notmatch "SetDirty\(detector\)") "Simulator sync does not dirty transient detector state"
$loginScene = Join-Path $projRoot "Assets\_Scenes\Login.unity"
if (Test-Path -LiteralPath $loginScene) {
    $loginYaml = Get-Content -LiteralPath $loginScene -Raw
    Assert-True ($loginYaml -match "248dd1307748f4c44b27d8ddfdda6a48") "host boot scene hosts FoldablePostureDetector guid"
    Assert-True ($loginYaml -match "persistAcrossScenes: 1") "host boot scene detector persistAcrossScenes"
} else {
    Skip-Host "host boot scene Assets\_Scenes\Login.unity absent - detector wiring checks skipped"
}
$packageSetup = Get-Content (Join-Path $pkg "Editor\FoldablePackageSetup.cs") -Raw
Assert-True ($packageSetup -match "com/futuredigitalplus/foldandflip") "Editor validation uses current Java package path"
Assert-True ($packageSetup -match "MinAndroidTarget\s*=\s*34") "Editor setup enforces Android target API 34+"
Assert-True ($packageSetup -match "targetSdkVersion") "Editor setup validates Android target SDK"
Assert-True ($packageSetup -match "INFO: Only") "Validate Setup notes when only one autorotation axis is enabled (shared with iOS)"
Assert-True ($packageSetup -match "FAIL: Enable at least one autorotation axis") "Validate Setup fails only when no autorotation axis is enabled"
Assert-True ($packageSetup -notmatch "FAIL: Enable portrait \+ landscape autorotation") "Validate Setup does not fail landscape-only projects"
$layoutState = Get-Content (Join-Path $pkg "Runtime\FoldableLayoutState.cs") -Raw
Assert-True ($layoutState -match "public bool isInMultiWindowMode") "layout state has multi-window signal"
Assert-True ($layoutState -match "public bool isInPictureInPictureMode") "layout state has picture-in-picture signal"
Assert-True ($layoutState -match "isInMultiWindowMode\s*=\s*other\.isInMultiWindowMode") "layout copies multi-window signal"
Assert-True ($layoutState -match "isInPictureInPictureMode\s*=\s*other\.isInPictureInPictureMode") "layout copies picture-in-picture signal"

$setupMenu = Get-Content (Join-Path $pkg "Editor\FoldableSetupMenu.cs") -Raw
Assert-True ($setupMenu -match "ResponsiveUiEditorMenus\.CreateFoldableSetup") "setup menu item"
# Menu paths live in the kit's shared editor assembly (SharedEditor/ResponsiveUiEditorMenus.cs).
$sharedMenusPath = Join-Path (Split-Path $pkg -Parent) "SharedEditor\ResponsiveUiEditorMenus.cs"
Assert-True (Test-Path $sharedMenusPath) "shared editor menus exist"
Assert-True (-not (Test-Path (Join-Path $pkg "Editor\FoldableEditorMenus.cs"))) "no module copy of the menu paths"
$menus = Get-Content $sharedMenusPath -Raw
Assert-True ($menus -match 'Root = "Responsive UI"') "FoldAndFlip menus under Responsive UI"
Assert-True ($menus -match "Fold & Flip Catalog") "menu Fold & Flip Catalog path"
Assert-True ($menus -match "Posture Simulator") "menu Posture Simulator path"
Assert-True ($menus -match "Apply Android Fold & Flip Recommended Settings") "menu Apply Recommended path"
Assert-True ($menus -match "Diagnose Simulator Bridge") "menu Diagnose path"
Assert-True ($menus -notmatch '"Fold & Flip"') "no former Fold & Flip top-level root"
Assert-True ($packageSetup -notmatch "MenuItem.*ValidateSetup") "FoldAndFlip Validate menu removed (unified Validate)"
$overlayUtil = Get-Content (Join-Path $pkg "Editor\FoldableDeviceOverlayUtil.cs") -Raw
Assert-True ($overlayUtil -match "ResponsiveUiEditorPath\.ToProjectRelativePath") "overlay util maps stored overlay paths to Assets/Packages"
Assert-True ($overlayUtil -notmatch "_Overlay\.png") "overlay util reads profile overlay paths instead of guessing file names"
Assert-True ($packageSetup -match "public const string Version") "package version constant is shared"
Assert-True ($packageSetup -match 'Version = "1\.0\.0"') "FoldAndFlip module version is 1.0.0"
Assert-True ($packageSetup -notmatch "InitializeOnLoad") "FoldAndFlip first-run owned by root ResponsiveUiPackageSetup"
Assert-True ($packageSetup -match "public const string FolderName") "package folder name constant is shared"
Assert-True ($packageSetup -match "ModuleRelativePath") "module relative path constant exists"
Assert-True ($setupMenu -match "CreateFoldableCompleteScene") "complete scene menu item"
Assert-True ($menus -match "Create Fold & Flip Complete Scene") "menu Create Complete Scene path"
Assert-True ($setupMenu -match "Responsive UI") "setup dialog branding"
Assert-True ($setupMenu -match "BuildSetup") "setup builder logic"
Assert-True ($setupMenu -match "CreateFlexBackground") "setup creates FlexBG"
Assert-True ($setupMenu -match "EnsureEventSystem") "setup ensures EventSystem"

$plugin = Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw
$profilePlugin = Get-Content (Join-Path $pkg "Editor\FoldableDeviceProfileNavigationPlugin.cs") -Raw
$screenCommit = Get-Content (Join-Path $pkg "Editor\FoldableSimulatorScreenCommit.cs") -Raw
$fsim = Get-Content (Join-Path $pkg "Editor\FoldablePostureSimulatorWindow.cs") -Raw
$orientHelper = Get-Content (Join-Path $pkg "Editor\FoldablePlayerOrientation.cs") -Raw
Assert-True ($plugin -match "DeviceSimulatorPlugin") "Device Simulator plugin present"
Assert-True ($plugin -notmatch "SelectAdjacentProfile|◀ Previous|Next ▶") "Fold panel does not contain profile navigation"
Assert-True ($profilePlugin -match 'title => "Device Profiles"') "profile navigation is a separate Device Simulator panel"
Assert-True ($profilePlugin -match 'CreateButton\("◀ Previous"') "Device Profiles panel has Previous control"
Assert-True ($profilePlugin -match 'CreateButton\("Next ▶"') "Device Profiles panel has Next control"
Assert-True ($profilePlugin -match "SelectAdjacentProfile") "profile controls cycle installed Simulator devices"
Assert-True ($profilePlugin -match "GetDeviceNamesInDisplayOrder") "profile controls follow the complete displayed Simulator device-list order"
Assert-True ($profilePlugin -notmatch "SimulatorEnabled") "profile controls do not filter out Unity standard profiles"
Assert-True ($profilePlugin -match "SimulatorDeviceBridge\.SelectDevice") "profile controls select the Simulator device"
Assert-True ($profilePlugin -match "currentProfileIndex = index;") "profile navigation updates its index immediately"
Assert-True ($profilePlugin -match "Current profile \{displayedPosition\}/\{displayedProfileNames\.Count\}") "profile panel displays current installed-profile index"
Assert-True ($profilePlugin -match 'currentProfileLabel = CreateMutedLabel\([^;]*minHeight:\s*36\)') "profile name reserves at least two text lines"
Assert-True ($profilePlugin -match "ResolveProfileIndex") "settled Simulator selection reconciles the profile index"
Assert-True ($profilePlugin -match "pendingProfileName") "profile navigation preserves its pending target across stale Simulator events"
Assert-True ($profilePlugin -match "previousProfileName") "profile navigation distinguishes stale events from manual selection"
Assert-True ($plugin -match "deviceSimulator\.deviceChanged \+=") "plugin binds deviceChanged directly"
Assert-True ($plugin -match "void OnDeviceChanged\(\)[\s\S]{0,400}RequestSelectionPublish") "deviceChanged asks the bridge to publish a settled selection"
Assert-True ($plugin -notmatch "void OnDeviceChanged\(\)[\s\S]{0,400}NotifySelectionChanged") "deviceChanged does not publish selection itself"
Assert-True ($plugin -match "FoldableSimulatorScreenCommit") "plugin commits Simulator Cover/Inner through the shared screen-commit helper"
Assert-True ($plugin -notmatch "void OnDeviceChanged\(\)[\s\S]{0,320}SyncSimulatorToPosture") "deviceChanged does not write Simulator chrome"
Assert-True ($plugin -notmatch "void RefreshBinding\(\)[\s\S]{0,240}ApplyAfterChange") "binding refresh does not apply scene layout"
Assert-True ($plugin -match "Play Mode recreates this panel") "CreateGUI does not force Expand defaults on Play Mode"
Assert-True ($plugin -notmatch "RefreshNow\(forceRebind:\s*true\)") "plugin UI build never forces a rebind"
Assert-True ($plugin -match "AssemblyReloadEvents\.beforeAssemblyReload") "plugin cancels work before assembly reload"
Assert-True ($plugin -match "EditorApplication\.isCompiling|EditorApplication\.isUpdating") "queued plugin work skips editor import states"
Assert-True ($fsim -match "PlayModeWindow\.SetViewType") "Posture Simulator uses Simulator Play Mode view"
Assert-True ($orientHelper -match "PlayerSettings\.Android\.resizeableActivity") "Android resizable uses the Unity 6 API"
Assert-True ($orientHelper -match "defaultInterfaceOrientation") "uses defaultInterfaceOrientation"
Assert-True ($plugin -match "override string title") "plugin title override"
Assert-True ($plugin -match 'Title = "Android Fold & Flip"') "plugin title branding"
Assert-True ($plugin -match "!FoldableEditorSession\.BoundInnerLandscapeNative") "Android Fold & Flip panel leaves iPhone Duo profiles to the iPhone Duo panel"
Assert-True ($screenCommit -match "SyncScreenForFoldablePosture") "Inner auto-switch on apply"
Assert-True ($plugin -match "FoldableSimulatorScreenCommit") "plugin syncs simulator posture/resolution"
Assert-True ($plugin -match "ApplyProductChange|Auto-apply on change") "plugin applies on product change"
$bridgeSrc = Get-Content (Join-Path $pkg "Editor\SimulatorDeviceBridge.cs") -Raw
Assert-True ($bridgeSrc -match "GetDeviceNamesInDisplayOrder") "bridge exposes Unity's complete displayed device order"
Assert-True ($bridgeSrc -match "FindDeviceIndex\(main, friendlyName, allowNormalizedName\)") "bridge resolves the visible list index before selection"
Assert-True ($bridgeSrc -match "SelectDeviceInternal\(friendlyName, preferredScreenIndex, allowNormalizedName: false\)") "general profile navigation requires exact displayed names"
Assert-True ($bridgeSrc -match "m_ScreenIndex") "bridge writes m_ScreenIndex"
Assert-True ($bridgeSrc -match "InitSimulation|InitScreenUI") "bridge rebuilds simulation after screen change"
Assert-True ($bridgeSrc -notmatch "QueuePlayerLoopUpdate") "bridge does not force player-loop updates"
Assert-True ($bridgeSrc -notmatch "EditorWindow\)\?\.Repaint|editorWindow\.Repaint") "bridge does not repaint Simulator windows"
Assert-True ($bridgeSrc -match "OnOrientationChanged") "bridge observes Simulator orientation events"
Assert-True ($bridgeSrc -notmatch "BindSimulatorEvent\(screenSimulation, `"OnResolutionChanged`"\)") "bridge does not bind OnResolutionChanged (W×H flicker / Raw Input)"
Assert-True ($bridgeSrc -match "BindSimulatorEvent\(screenSimulation, `"OnOrientationChanged`"\)") "bridge binds OnOrientationChanged"
Assert-True ($bridgeSrc -match "BindSimulatorEvent\(deviceView, `"OnBoundingBoxShapeChange`"\)") "bridge binds the device view shape change (chrome rotation across the Player lock raises no orientation event)"
Assert-True ($bridgeSrc -match 'AssemblyReloadEvents\.beforeAssemblyReload \+= OnBeforeAssemblyReload') "bridge cleans event bindings before reload"
Assert-True ($bridgeSrc -match 'void OnBeforeAssemblyReload\(\)[\s\S]{0,200}UnbindSelectionEventBindings\(\)') "bridge unbinds selection events from beforeAssemblyReload"
Assert-True ($bridgeSrc -match "RequestSelectionPublish") "bridge is the single Simulator selection publisher"
Assert-True ($bridgeSrc -match "GetActiveSelection") "bridge prefers the focused or visible Simulator window"
Assert-True ($bridgeSrc -match "GetSimulatorWindowObjects") "bridge exposes Simulator windows without reading window.position"
Assert-True ($bridgeSrc -match "SelectionSettleRetries") "bridge settles identity with a bounded retry chain"
Assert-True ($bridgeSrc -match "IsSelectionPending") "bridge exposes pending selection for Fold panel hide"
Assert-True ($plugin -match "IsSelectionPending") "Fold panel hides while selection is pending"
Assert-True ($bridgeSrc -match "IsLandscape") "bridge uses session IsLandscape for rotation"
Assert-True ($bridgeSrc -match "SetRotationDegrees\(chromeLandscape \? 90 : 0\)") "bridge rotates the device chrome axis"
Assert-True ($plugin -match "Screen orientation") "plugin has screen orientation control"
Assert-True ($plugin -match "ApplyAfterChange") "plugin apply path"
Assert-True ($plugin -match "RefreshBinding|ResolveFromSimulator") "plugin rebinds on device change"
Assert-True ($plugin -match "PublishProductState|Product state") "plugin product-state UI"
Assert-True ($plugin -match "FoldableEditorSessionWatcher\.RefreshNow") "plugin refreshes watcher binding"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableEditorSession.cs")) "shared session exists"
Assert-True (Test-Path (Join-Path $pkg "Editor\SimulatorDeviceBridge.cs")) "simulator device bridge exists"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableEditorSessionWatcher.cs")) "session watcher exists"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableDeviceCatalogWindow.cs")) "device catalog window exists"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableDeviceCatalogIO.cs")) "device catalog IO exists"
$catalogWin = Get-Content (Join-Path $pkg "Editor\FoldableDeviceCatalogWindow.cs") -Raw
Assert-True ($catalogWin -match "ResponsiveUiEditorMenus\.FoldFlipCatalog|Fold & Flip Catalog") "catalog menu item"
Assert-True ($catalogWin -match "SaveProfile|UseInSimulator|Use in Simulator") "catalog save + simulator sync"
Assert-True ($catalogWin -match "EnterEditMode|CancelEditMode|ExitEditAndReloadDraft|editing") "catalog edit/save lock mode"
Assert-True ($catalogWin -match "BuildProfileActions") "selected-profile actions live in the profile pane"
Assert-True ($catalogWin -match "previewScreenName") "simulator handoff uses the previewed screen"
Assert-True ($catalogWin -match "RefreshAfterFilterChanged") "catalog filters keep visible selection synchronized"
Assert-True ($catalogWin -match "TwoPaneSplitView|DrawPreviewGui") "catalog has resizable preview panes"
Assert-True ($catalogWin -match "previewLandscape|Landscape") "catalog preview supports landscape"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldableDeviceOverlayUtil.cs")) "shared overlay util exists"
Assert-True (Test-Path (Join-Path $pkg "Editor\FoldablePostureSimulatorWindow.cs")) "Posture Simulator window exists"
Assert-True ($fsim -match "ResponsiveUiEditorMenus\.PostureSimulator|Posture Simulator") "Posture Simulator menu item"
Assert-True ($detector -match "FoldableDeviceKind|deviceKind") "detector has device kind"
$resolver = Get-Content (Join-Path $pkg "Runtime\FoldableProductStateResolver.cs") -Raw
Assert-True ($resolver -match "GetDisplayName") "product state display names"
Assert-True ($resolver -match "ResolveDefaultLandscape") "orientation respects project lock helper"
Assert-True ($resolver -match "GetLockedLandscapeFromScreen") "screen policy lock helper"
Assert-True ($resolver -match "GetLockedHalfOpenHingeFromScreen") "screen policy half-open hinge lock helper"
Assert-True (Test-Path (Join-Path $pkg "Runtime\FoldableSimulationGate.cs")) "simulation gate exists"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -notmatch "RestoreAllInLoadedScenes") "plugin does not double-restore flat"
$watcher = Get-Content (Join-Path $pkg "Editor\FoldableEditorSessionWatcher.cs") -Raw
Assert-True ($watcher -match "RestoreAllInLoadedScenes") "watcher restores flat on non-foldable"
Assert-True ($watcher -match "SimulatorDeviceBridge\.SelectionChanged \+=") "watcher consumes Simulator events"
Assert-True ($watcher -match "FoldablePostureDetector\.EditorInitializing \+= InitializeDetector") "new detector adopts the bound Simulator profile kind before ownership notification"
Assert-True ($watcher -match "FoldablePostureSource\.SetEditorDeviceKind\(FoldableEditorSession\.DeviceKind\)") "Simulator profile kind syncs transiently into the detector"
Assert-True ($watcher -match "FoldableEditorSession\.ScreenSynced \+= SyncDetectorLayout") "stable Simulator screen syncs the complete detector layout"
Assert-True ($watcher -notmatch 'lastModel = "";\s*lastFriendly = "";\s*lastScreenIndex = -1;\s*if \(forceRebind\)') "watcher preserves selection deduplication"
Assert-True ($watcher -match "firstBind && !suppressDefaultsOnce") "catalog defaults only on first Fold/Flip bind"
Assert-True ($watcher -notmatch "applyDefaults = deviceChanged &&") "switching Fold/Flip profiles does not re-apply Expand defaults"
Assert-True ($watcher -match "Half Opened stays Half Opened") "switching Fold/Flip profiles keeps the Fold panel product state"
Assert-True ($watcher -match "FoldableSimulatorScreenCommit.QueueDeferred") "new Fold/Flip profile switches Cover/Inner to the kept product"
Assert-True ($watcher -match "firstBind \|\| profileChanged") "first Flip bind syncs Simulator to Opened Main"
Assert-True ($watcher -match "FoldableSimulatorScreenCommit.InFlight") "Cover to Main HWND flicker does not unbind during screen commit"
Assert-True ($watcher -match "QueueUnbind") "invalid Simulator selection is debounced so Fold/Flip switches do not RestoreFlat"
Assert-True ($watcher -match "Ghost Dual/Generic skins must not UnbindFoldable") "watcher does not unbind-loop on a deleted Simulator skin"
Assert-True ($watcher -match "invalidSelectionChecksRemaining = 3") "invalid Simulator selection uses a bounded debounce"
Assert-True ($watcher -match "UnbindFoldable\(\);") "persistent invalid Simulator selection releases stale binding"
Assert-True ($watcher -match "(?s)static void UnbindFoldable\(\).*?FoldableEditorSession\.ResetProductDefaults\(\)") "Phone/Pad selection resets product so the next Fold bind is Expand"
$layoutTests = Get-Content (Join-Path $pkg "Tests\Editor\FoldableLayoutTests.cs") -Raw
Assert-True ($layoutTests -match "SetDeviceBinding_SwitchProfileWithoutDefaults_KeepsHalfOpened") "Unity tests keep Half Opened when switching Fold/Flip profiles without defaults"
Assert-True ($layoutTests -match "SetDeviceBinding_ApplyDefaultsAfterUnbind_KeepsHalfOpened") "Unity tests keep Half Opened when first-bind defaults run after a flicker unbind"
Assert-True ($layoutTests -match "SetDeviceBinding_FlipProfile_UpdatesActiveDetectorKind") "Unity tests sync Simulator profile kind into the active detector"
Assert-True ($layoutTests -match "NativeDetect_Fold_NoFeatureAfterHalfOpened_KeepsHalfOpened") "Unity tests keep Half Opened when FoldingFeature is missing"
Assert-True ($layoutTests -match "NativeDetect_Fold_FlatAfterHalfOpened_IsExpand") "Unity tests let Jetpack FLAT replace Half Opened"
$sessionBind = Get-Content (Join-Path $pkg "Editor\FoldableEditorSession.cs") -Raw
Assert-True ($sessionBind -notmatch "PresetName|FriendlyNameToPreset") "session keeps no write-only preset name"
Assert-True ($sessionBind -match "enum Source\s*\{\s*Simulator,\s*Posture,\s*Bridge,?\s*\}") "session sources are Simulator / Posture / Bridge (no unused None / Preview)"
Assert-True ($sessionBind -match "public static FoldableOrientation LandscapeNativeHalfOpenHingeForPose\(") "landscape-native Half Opened hinge follows the pose"
Assert-True ($sessionBind -match "nestedChangePending") "session re-raises a change published during Changed dispatch"
Assert-True ($sessionBind -match "(?s)static void InvokeChanged\(.*?catch \(Exception e\)") "session isolates Changed handler exceptions"
Assert-True ($sessionBind -match "public static bool BuildTargetOwnsProfile\(bool landscapeNativeInner\)") "panels and watcher bind only the build target's profiles"
Assert-True ($watcher -match "AllowSimulatedLayouts = FoldableEditorSession\.IsFoldableDeviceBound") "watcher opens the simulation gate on session changes"
Assert-True ((Get-Content (Join-Path $pkg "Editor\AssemblyInfo.cs") -Raw) -notmatch "iphoneduo") "Fold & Flip editor internals stay private to Fold & Flip / ResponsiveUI tests"
Assert-True ($sessionBind -match "PublishScreenSynced") "Cover to Main chrome sync notifies layout without a redundant session Changed"
Assert-True ($screenCommit -match "QueueDeferred") "Phone to Flip waits a tick then commits Cover to Main twice"
Assert-True ($screenCommit -match "CommitNowThenCatchup") "Fold panel product buttons share the same Cover to Main commit"
Assert-True ($screenCommit -match "UnityScreenMatchesProduct") "layout Apply waits until Unity Screen matches the session product"
Assert-True ($screenCommit -match "lastCommit") "last catch-up still notifies layout if Screen lags"
Assert-True ($screenCommit -match "PublishScreenSynced") "screen commit notifies layout after Unity Screen switch"
Assert-True ($watcher -match "QueueDeferred") "first Flip bind Apply waits until Simulator Cover to Main commits"
Assert-True ($sessionBind -match "Honor live Cover screen") "Fold defaults honor Cover/Normal when Simulator is on Cover"
Assert-True ($sessionBind -match "keepProduct") "applyDefaults does not stomp explicit HalfOpened or Closed"
Assert-True ($sessionBind -match "ApplyFlipOpenedInner") "Phone or Fold to Flip defaults Opened Main"
Assert-True ($sessionBind -match "Phone or Fold") "session comments Phone/Fold to Flip Opened"
Assert-True ($layoutTests -match "SetDeviceBinding_FirstBindFlipCoverScreen_IsOpened") "Unity tests Flip first bind Cover screen is Opened"
Assert-True ($layoutTests -match "SetDeviceBinding_FoldClosedToFlip_IsOpened") "Unity tests Fold Closed to Flip is Opened"
Assert-True ($layoutTests -match "SetDeviceBinding_FlipToFlip_KeepsClosed") "Unity tests Flip to Flip keeps Closed"
Assert-True ($sessionBind -match "ResetProductDefaults") "Phone/Pad unbind can reset Fold panel product"
Assert-True ($sessionBind -match "SessionState") "Play Mode domain reload persists Fold panel product"
Assert-True ($sessionBind -match "RestorePersisted") "Play Mode restores Fold panel product before first bind"
Assert-True ($sessionBind -match "DropStaleCatalogBinding") "session drops a persist bind whose catalog JSON is gone"
Assert-True ($sessionBind -match "CommitPlayLayout") "session commits PlayLayout for Play Mode enable"
Assert-True ($detector -match "ApplyPlayLayout") "Play Mode applies Fold panel snapshot on enable"
Assert-True ($watcher -match "ResolveSelection") "Phone/Pad unbind is delayed one tick during Simulator flicker"
Assert-True ($layoutTests -match "SimulateDomainReload_KeepsHalfOpened") "Unity tests keep Half Opened across Play Mode domain reload"
Assert-True ($layoutTests -match "PlayLayout.productState") "Unity tests restore PlayLayout HalfOpened"
$postureWin = Get-Content (Join-Path $pkg "Editor\FoldablePostureSimulatorWindow.cs") -Raw
Assert-True ($postureWin -match 'void OnEnable\(\)[\s\S]{0,200}never publish or switch[\s\S]{0,600}LoadBoundSelection\(\);') "Posture Simulator OnEnable only reads the bound session (keeps Normal / Seated)"
Assert-True ($postureWin -match "ApplyFold\(bool force = false\)") "Posture Simulator supports manual Apply"
Assert-True ($postureWin -match 'Btn\("Apply"') "Posture Simulator exposes an Apply button"
Assert-True ($postureWin -match "EditorApplication\.isPlaying[\s\S]*ApplySimulatedLayout[\s\S]*SyncSimulatedLayoutState") "Posture Simulator applies in Play Mode and syncs in Edit Mode"
Assert-True ($postureWin -match "Closed / Normal / Expand product-state authoring") "Posture Simulator directs full product authoring to Device Simulator"
Assert-True ($detector -match "ApplyFlatLayout") "detector ApplyFlatLayout"
Assert-True ($detector -match "previousProduct") "native commit keeps previous product across snapshot merge"
Assert-True ($detector -match "previousOrientation") "native commit keeps HalfOpened hinge axis across a missing FoldingFeature"
$simRuntime = Get-Content (Join-Path $pkg "Runtime\FoldableSimulator.cs") -Raw
Assert-True ($simRuntime -match "AllowSimulatedLayouts") "simulator respects gate"
Assert-True ($simRuntime -match "SyncOverlayFromDetector") "returning to Fold/Flip does not re-push serialized Expand"
Assert-True ($simRuntime -match "RequireComponent\(typeof\(FoldablePostureDetector\)\)") "simulator requires FoldablePostureDetector"
Assert-True ($simRuntime -notmatch "DEVELOPMENT_BUILD") "Android DEVELOPMENT_BUILD does not overlay Jetpack layout"
Assert-True ($simRuntime -notmatch "detector == null") "RequireComponent simulator does not repeatedly recover its detector"
Assert-True ($simRuntime -match "(?s)#endregion\s*#endif\s*#region Serialized") "simulator serialized fields are not Editor-only (same layout in players)"
Assert-True ($resolver -match "ShouldPreserveHalfOpened") "Enrich keeps HalfOpened across a missing FoldingFeature"
Assert-True ($layoutState -match "other.state != FoldableState.Undefined") "native merge keeps live HalfOpened hinge when FoldingFeature is missing"
Assert-True ($layoutState -match "state == FoldableState.Flat \|\| state == FoldableState.HalfOpened") "MergeNativeSnapshot clears stale Flat/HalfOpened when FoldingFeature is missing"
Assert-True ($layoutState -match "CopyExact") "layout exposes explicit exact-copy semantics"
Assert-True ($layoutState -match "MergeNativeSnapshot") "layout exposes explicit native-merge semantics"
Assert-True ($layoutTests -match "MergeNativeSnapshot_UndefinedNative_KeepsHalfOpenedHingeAndKind") "Unity tests keep hinge bounds across an empty native snapshot"
Assert-True ($layoutTests -match "MergeNativeSnapshot_UndefinedAfterExpand_ClearsStaleFlat") "Unity tests clear stale Flat when FoldingFeature is missing after Expand"
Assert-True ($layoutTests -match "NativeDetect_Fold_CoverAfterExpand_ViaMerge_IsClosedNormal") "Unity tests map Expand→Normal cover after merge"
Assert-True ($layoutTests -match "NativeDetect_Fold_CoverAfterExpand_NoPeak_ViaKindOpenSize_IsClosed") "Unity tests map cover via Device Kind when peak is wiped"
Assert-True ($layoutTests -match "ApplyCoverFlags_StaleOpenedWithCoverSize_BecomesClosed") "Unity tests override stale Opened with cover size"
Assert-True ($layoutTests -match "NativeDetect_RearDisplayActive_WhileFlat_KeepsClosedNormal") "Unity tests keep Closed when rear ACTIVE under FLAT"
Assert-True ($layoutTests -match "ApplyCoverFlags_LiveFlatWithCoverSizedWindow_StaysExpand") "Unity tests do not size-probe live FLAT into Closed"
Assert-True ($layoutTests -match "ApplyCoverFlags_LiveHalfOpened_DoesNotSizeProbeClosed") "Unity tests do not size-probe live HALF_OPENED into Closed"
Assert-True ($layoutTests -match "NativeDetect_Fold_CoverAfterHalfOpened_ViaMerge_ClearsStaleFlex") "Unity tests map Flex→Normal when FoldingFeature is missing"
Assert-True ($layoutTests -match "NativeDetect_Fold_HalfOpened_ForcesIsSeparating") "Unity tests force isSeparating on HALF_OPENED"
Assert-True ($layoutTests -match "ApplyCoverFlags_StaleHalfOpenedWithCoverSize_BecomesClosed") "Unity tests override stale HalfOpened with cover size"
Assert-True ($detector -match "RefreshNativeIfScreenChanged") "detector refreshes bridge when Unity Screen size changes"
Assert-True ($helper -match "public static void refresh") "Java bridge exposes refresh for Screen-size catch-up"
Assert-True ($helper -match "isStaleFoldingFeatureForCover") "Java drops stale FoldingFeature when metrics say cover"
Assert-True ($helper -match "featureSpansWindow") "Java stale feature also requires a hinge/window span mismatch"
Assert-True ($helper -notmatch "getPhysicalWidth") "largest display uses the active (logical) resolution"
Assert-True ($helper -match "Display\.DEFAULT_DISPLAY") "largest display reference is built-in only"
Assert-True ($helper -notmatch "resetSessionPeakMetrics\(\);\s*boundActivity") "Java keeps session peak across Activity rebind"
Assert-True ($resolver -match "Expand Half Opened") "fold half-open label"
Assert-True ($resolver -match "Half Opened") "flip half-open label"
Assert-True ($resolver -match "CoverAreaRatio|IsCurrentMuchSmallerThanReference|FoldableCoverHeuristics") "size-based cover heuristic"
Assert-True ($resolver -match "IsSmallerThanKindOpenMetrics|peakMaxMetrics|FoldableCoverHeuristics") "peak / kind open-size cover heuristic"
Assert-True (Test-Path (Join-Path $pkg "Runtime\FoldableCoverHeuristics.cs")) "cover heuristics module exists"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\src\main\java\com\futuredigitalplus\foldandflip\FoldableHelper.java") -Raw) -match "FoldableWindowAreaTracker") "Helper uses WindowArea tracker"
Assert-True (Test-Path (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\src\main\java\com\futuredigitalplus\foldandflip\FoldableWindowAreaTracker.java")) "WindowArea tracker exists"
Assert-True (Test-Path (Join-Path $pkg "..\Docs\STRUCTURE.md")) "package structure doc exists"
Assert-True ($resolver -match "DualScreen") "dual single-pane maps to Closed"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\FoldableProxy.cs") -Raw) -match "isCoverScreen = values\[4\] != 0") "proxy reads the WindowArea cover flag from the payload"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\Plugins\Android\Foldable.androidlib\src\main\java\com\futuredigitalplus\foldandflip\FoldableWindowAreaTracker.java") -Raw) -match "WindowAreaControllerCallbackAdapter") "WindowArea tracker uses CallbackAdapter"
Assert-True (Test-Path (Join-Path $pkg "Docs\DETECTION.md")) "detection reference doc exists"
Assert-True ((Get-Content (Join-Path $pkg "Docs\DETECTION.md") -Raw) -match "PLATFORMS") "DETECTION points at kit platforms / future adapter doc"
Assert-True ((Get-Content (Join-Path $pkg "Docs\DETECTION.md") -Raw) -match "ShouldPreserveHalfOpened|preserve Flex") "DETECTION keeps HalfOpened across a missing FoldingFeature unless Closed"
Assert-True ((Get-Content (Join-Path $pkg "Docs\DETECTION.md") -Raw) -match "starts \*\*Opened\*\*") "DETECTION Flip from Phone/Fold starts Opened"
Assert-True ((Get-Content (Join-Path $pkg "Docs\DETECTION.md") -Raw) -match "persistAcrossScenes") "DETECTION documents persistAcrossScenes DontDestroyOnLoad for HalfOpened"
Assert-True ((Get-Content (Join-Path $pkg "Docs\DETECTION.md") -Raw) -match "Boot through the scene[\s\S]{0,5}that hosts the detector") "DETECTION requires booting through the detector scene for Android HalfOpened"
Assert-True ((Get-Content (Join-Path $pkg "..\Docs\PRODUCTION.md") -Raw) -match "persistAcrossScenes") "PRODUCTION documents persistAcrossScenes DontDestroyOnLoad"
$migrationDoc = Get-Content (Join-Path $pkg "..\Docs\MIGRATION.md") -Raw
Assert-True ($migrationDoc -match "persistAcrossScenes") "MIGRATION documents the persistAcrossScenes host pattern"
Assert-True ($migrationDoc -match "cannot\s+automatically remap") "MIGRATION explains former manager GUID replacement"
Assert-True ((Get-Content (Join-Path $pkg "README.md") -Raw) -match "persistAcrossScenes") "FoldAndFlip README documents persistAcrossScenes"
Assert-True ((Get-Content (Join-Path $pkg "README.md") -Raw) -match "FoldablePostureDetector") "FoldAndFlip README documents FoldablePostureDetector"
$catalog = Get-Content (Join-Path $pkg "Editor\FoldableDeviceCatalog.cs") -Raw
Assert-True ($catalog -match "ProfilesRelativePath|Devices/Profiles") "catalog loads Profiles JSON"
Assert-True ($catalog -match "FoldPrefix|FlipPrefix|FoldableDeviceKind") "catalog Fold/Flip kinds"
Assert-True ($catalog -notmatch "SimulatorListTopPrefixLegacy|★ Foldable /") "catalog has current simulator prefixes only"
Assert-True ($catalog -notmatch 'normalized.Equals\("Generic"') "IsToolkitListName requires a live catalog device"
Assert-True ($catalog -notmatch "foldable-generic") "catalog does not special-case deleted Generic model"
Assert-True ($catalog -match "JsonUtility|Reload") "catalog has JSON Reload"
Assert-True ($catalog -notmatch 'Width\s*=\s*\d{3,}') "catalog C# has no hard-coded screen widths"
$profilesDir = Join-Path $pkg "Editor\Devices\Profiles"
Assert-True (Test-Path $profilesDir) "Profiles folder exists"
$profileFiles = @(Get-ChildItem $profilesDir -Filter "*.json")
Assert-True ($profileFiles.Count -ge 27) "at least 27 device profile JSON files ($($profileFiles.Count))"
$flipMarked = $false
foreach ($pf in $profileFiles) {
    $pj = Get-Content $pf.FullName -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True ($pj.sources -and $pj.sources.Count -ge 1) "profile $($pj.id) has sources"
    Assert-True ([string]$pj.sources[0].url -match '^https?://') "profile $($pj.id) source url"
    Assert-True ($pj.skins -and $pj.skins.deviceFile) "profile $($pj.id) has skins.deviceFile"
    $deviceAbs = Join-Path $pkg (($pj.skins.deviceFile -replace '/', [IO.Path]::DirectorySeparatorChar))
    Assert-True (Test-Path $deviceAbs) "profile $($pj.id) device file exists"
    Assert-True ($pj.screens -and $pj.screens.Count -ge 1) "profile $($pj.id) has screens"
    foreach ($ps in $pj.screens) {
        Assert-True ($ps.portraitWidth -gt 0 -and $ps.portraitHeight -gt 0) "profile $($pj.id)/$($ps.name) has portrait WxH"
        Assert-True ($ps.landscapeWidth -gt 0 -and $ps.landscapeHeight -gt 0) "profile $($pj.id)/$($ps.name) has landscape WxH"
        Assert-True ([int]$ps.landscapeWidth -eq [int]$ps.portraitHeight) "profile $($pj.id)/$($ps.name) landscapeWidth==portraitHeight"
        Assert-True ([int]$ps.landscapeHeight -eq [int]$ps.portraitWidth) "profile $($pj.id)/$($ps.name) landscapeHeight==portraitWidth"
    }
    $dj = Get-Content $deviceAbs -Raw -Encoding UTF8 | ConvertFrom-Json
    Assert-True ($dj.screens.Count -eq $pj.screens.Count) "profile $($pj.id) screen count vs .device ($($dj.screens.Count)/$($pj.screens.Count))"
    for ($si = 0; $si -lt $pj.screens.Count; $si++) {
        Assert-True ([int]$dj.screens[$si].width -eq [int]$pj.screens[$si].portraitWidth) "profile $($pj.id)[$si] width vs .device portrait"
        Assert-True ([int]$dj.screens[$si].height -eq [int]$pj.screens[$si].portraitHeight) "profile $($pj.id)[$si] height vs .device portrait"
        Assert-True ([string]$dj.screens[$si].presentation.name -eq [string]$pj.screens[$si].name) "profile $($pj.id)[$si] name vs .device"
    }
    if ($pj.skins.overlays) {
        foreach ($ov in $pj.skins.overlays) {
            $ovAbs = Join-Path $pkg (($ov.path -replace '/', [IO.Path]::DirectorySeparatorChar))
            Assert-True (Test-Path $ovAbs) "profile $($pj.id) overlay $($ov.screen)"
        }
    }
    if ($pj.kind -eq "FlipablePhone") { $flipMarked = $true }
}
Assert-True $flipMarked "profiles mark FlipablePhone devices"
Get-ChildItem (Join-Path $pkg "Editor\Devices\*.device") | ForEach-Object {
    $raw = [System.IO.File]::ReadAllText($_.FullName)
    $toolkit = $raw.Contains([char]0x2605) -and ($raw -match "Fold /|Flip /|Dual /")
    # Design Canvas refs: SOH + Flip / | Fold Cover / | iPhone Duo / | Fold / + ShortName
    # (ResponsiveUiSimulatorName.DesignRole* consts).
    $designRef = $raw -match '"friendlyName"\s*:\s*"\\u0001(Fold Cover / |iPhone Duo / |Fold / |Flip / )[^"]+"'
    Assert-True ($toolkit -or $designRef) "device highlight $($_.Name)"
}
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlipLike_Main_Overlay.png")) "skin Flip Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\PixelFoldLike_Inner_Overlay.png")) "skin Pixel Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold6Like_Inner_Overlay.png")) "skin Fold6 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\HuaweiMateX3Like_Inner_Overlay.png")) "skin Mate X3 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\HonorMagicV2Like_Inner_Overlay.png")) "skin Magic V2 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\OnePlusOpenLike_Inner_Overlay.png")) "skin OnePlus Open Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\XiaomiMixFold3Like_Inner_Overlay.png")) "skin Mix Fold3 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\MotorolaRazrPlusLike_Main_Overlay.png")) "skin Razr+ Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold7Like_Inner_Overlay.png")) "skin Fold7 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlip7Like_Main_Overlay.png")) "skin Flip7 Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\Pixel9ProFoldLike_Inner_Overlay.png")) "skin Pixel9 Pro Fold Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\OppoFindN5Like_Inner_Overlay.png")) "skin Find N5 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\HonorMagicV3Like_Inner_Overlay.png")) "skin Magic V3 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\MotorolaRazr50UltraLike_Main_Overlay.png")) "skin Razr50 Ultra Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\HuaweiMateXTLike_Inner_Overlay.png")) "skin Mate XT Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\VivoXFold3ProLike_Inner_Overlay.png")) "skin X Fold3 Pro Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\XiaomiMixFold4Like_Inner_Overlay.png")) "skin Mix Fold4 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold8UltraLike_Inner_Overlay.png")) "skin Fold8 Ultra Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold8Like_Inner_Overlay.png")) "skin Fold8 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlip8Like_Main_Overlay.png")) "skin Flip8 Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZTriFoldLike_Inner_Overlay.png")) "skin TriFold Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlip7FELike_Main_Overlay.png")) "skin Flip7 FE Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFoldSpecialEditionLike_Inner_Overlay.png")) "skin Fold SE Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlip6Like_Main_Overlay.png")) "skin Flip6 Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFlip5Like_Main_Overlay.png")) "skin Flip5 Main"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold5Like_Inner_Overlay.png")) "skin Fold5 Inner"
Assert-True (Test-Path (Join-Path $pkg "Editor\Devices\DeviceOverlays\GalaxyZFold2Like_Inner_Overlay.png")) "skin Fold2 Inner"
Assert-True ((Test-Path (Join-Path $profilesDir "galaxy-z-fold8-ultra-like.json")) -and
    (Test-Path (Join-Path $profilesDir "galaxy-z-trifold-like.json")) -and
    (Test-Path (Join-Path $profilesDir "galaxy-z-flip8-like.json"))) "profiles list Samsung Device Lab lineup"
$foldDevice = Get-Content (Join-Path $pkg "Editor\Devices\Galaxy Z Fold8-like.device") -Raw
Assert-True ($foldDevice -match "overlayPath") "Fold8-like device references overlay"

$asm = Get-Content (Join-Path $pkg "Editor\com.futuredigitalplus.foldandflip.editor.asmdef") -Raw
Assert-True ($asm -match "UnityEditor.DeviceSimulatorModule") "editor asmdef refs DeviceSimulatorModule"
Assert-True ($asm -match "com\.futuredigitalplus\.foldandflip") "editor asmdef refs runtime assembly"
Assert-True ($orientHelper -match "DescribeProjectOrientation") "orientation lock helpers"
Assert-True ($orientHelper -match "GetAllowedAxes") "GetAllowedAxes centralizes Player orientation axes"
Assert-True ($orientHelper -match "IsPortraitAllowed") "IsPortraitAllowed exists"
Assert-True ($orientHelper -match "IsLandscapeAllowed") "IsLandscapeAllowed exists"
Assert-True ($orientHelper -match "GetLockedHalfOpenHinge") "half-open hinge lock helper"
Assert-True ($orientHelper -match "(?s)LockedPoseLandscape\(.*?FoldableProductStateResolver\.IsLandscapeNativeHalfOpen\(product, innerLandscapeNative\)\)\s*return null") "iPhone Duo Seated is never forced by the Player lock (Seated allows only the hold's axis on device)"
Assert-True ($orientHelper -notmatch "IgnoresOrientationLock") "no IgnoresOrientationLock helper (Seated is never forced by the lock)"
Assert-True ($orientHelper -match "public static bool IsAutoRotation") "Seated unlock needs Default Orientation = Auto Rotation (a fixed one keeps the lock)"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "EnforceProjectOrientationLock") "plugin enforces Player Settings orientation lock"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "SetEnabled\(!lockedLandscape") "screen orientation disables only when project-locked"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "SetEnabled\(half && !lockedHinge") "half-open hinge disables when project-locked"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "FoldableSimulatorPanelUi\.StyleSelected\(") "preset buttons highlight selected product state"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "product == FoldableProductState.HalfOpened \|\|") "Half Opened preset enables isSeparating"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "sepToggle\.SetEnabled") "isSeparating toggle gated by product state"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableEditorSession.cs") -Raw) -match "Keep the panel toggle") "HalfOpened does not force isSeparating false"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableEditorSession.cs") -Raw) -match "product == FoldableProductState\.Closed[\s\S]{0,80}isSeparating = false") "Closed / Normal clears isSeparating"

# Galaxy Z Fold8 Cover is uniquely wide (~10:16). Landscape 1972x1248 aspect ~1.58 is the
# only toolkit Fold cover under default ResponsiveDevice padMax 1.6 — without product mapping
# Normal mis-detects as Pad. Specs match GSMArena / Wikipedia (Cover 1248x1972).
$fold8Profile = Get-Content (Join-Path $pkg "Editor\Devices\Profiles\galaxy-z-fold8-like.json") -Raw | ConvertFrom-Json
$fold8Cover = @($fold8Profile.screens | Where-Object { $_.coverOrOuter -eq $true })[0]
Assert-True ($null -ne $fold8Cover) "Fold8 profile has Cover screen"
Assert-True ($fold8Cover.portraitWidth -eq 1248 -and $fold8Cover.portraitHeight -eq 1972) "Fold8 Cover portrait 1248x1972"
Assert-True ($fold8Cover.landscapeWidth -eq 1972 -and $fold8Cover.landscapeHeight -eq 1248) "Fold8 Cover landscape 1972x1248"
$fold8Aspect = [math]::Max($fold8Cover.landscapeWidth, $fold8Cover.landscapeHeight) /
               [math]::Min($fold8Cover.landscapeWidth, $fold8Cover.landscapeHeight)
Assert-True ($fold8Aspect -lt 1.6) "Fold8 Cover landscape is under padMax 1.6 (Pad landmine)"
Assert-True ($fold8Profile.notes -match "1\.58|padMax|Pad|Fold Cover") "Fold8 profile notes document Cover Pad landmine"
$fold8DeviceRaw = Get-Content (Join-Path $pkg "Editor\Devices\Galaxy Z Fold8-like.device") -Raw
Assert-True ($fold8DeviceRaw -match '"friendlyName"\s*:\s*"\\u0001Fold Cover / Galaxy Z Fold8-like"') "Fold8 Design Canvas .device is Fold Cover / at Simulator top"
Assert-True ($catalog -match "IsDesignCanvasReference\(profileId\)") "Design Canvas refs use role prefixes + sort prefix"
$simulatorName = Get-Content (Join-Path $pkg "..\SharedEditor\ResponsiveUiSimulatorName.cs") -Raw
Assert-True ($simulatorName -match 'DesignRoleFoldCover = "Fold Cover / "') "Design Canvas Fold Cover role prefix is Fold Cover /"
Assert-True ($simulatorName -match 'DesignRoleIphoneDuo = "iPhone Duo / "') "Design Canvas iPhone Duo role prefix is iPhone Duo /"
Assert-True ($catalog -match "ResponsiveUiSimulatorName\.DesignRoleFoldCover") "Design Canvas Fold Cover refs use the Fold Cover / role prefix"
Assert-True ($catalog -match "ResponsiveUiSimulatorName\.DesignRoleIphoneDuo") "Design Canvas iPhone Duo refs use the iPhone Duo / role prefix"
Assert-True ($catalog -match "IsPassportFoldCover") "passport Cover detection for Fold Cover Simulator label"
Assert-True ((Get-Content (Join-Path $pkg "Runtime\FoldablePostureDetector.cs") -Raw) -match "SyncSimulatedLayoutState") "FoldablePostureDetector can sync layout without LayoutChanged"
Assert-True ((Get-Content (Join-Path $pkg "Editor\FoldableDeviceSimulatorPlugin.cs") -Raw) -match "SyncSimulatedLayoutState") "Edit Mode Apply syncs detector layout state"
Assert-True ((Get-Content (Join-Path $pkg "README.md") -Raw) -match "30.*\.device|30 profiles|light green") "README documents skins and preset highlight"

Write-Host "`n=== Namespace and setup contract ===" -ForegroundColor Cyan
$runtimeAsm = Get-Content (Join-Path $pkg "Runtime\com.futuredigitalplus.foldandflip.asmdef") -Raw | ConvertFrom-Json
$editorAsm = Get-Content (Join-Path $pkg "Editor\com.futuredigitalplus.foldandflip.editor.asmdef") -Raw | ConvertFrom-Json
$testAsm = Get-Content (Join-Path $pkg "Tests\Editor\com.futuredigitalplus.foldandflip.tests.editor.asmdef") -Raw | ConvertFrom-Json
Assert-True ($runtimeAsm.name -eq "com.futuredigitalplus.foldandflip" -and $runtimeAsm.rootNamespace -eq "FDPlus.Foldable") "runtime assembly namespace contract"
Assert-True ($editorAsm.name -eq "com.futuredigitalplus.foldandflip.editor" -and $editorAsm.rootNamespace -eq "FDPlus.Foldable.Editor") "editor assembly namespace contract"
Assert-True ($testAsm.name -eq "com.futuredigitalplus.foldandflip.tests.editor" -and $testAsm.rootNamespace -eq "FDPlus.Foldable.Tests") "test assembly namespace contract"
Assert-True ($setupMenu -match 'const string RootName = "FoldableSetup"') "setup hierarchy root matches documentation"
Assert-True ($setupMenu -match "FoldablePostureDetector") "setup creates FoldablePostureDetector"
Assert-True ($setupMenu -match "SetupResult") "setup returns SetupResult"
Assert-True ($setupMenu -match "\.Detector") "setup wires Detector on SetupResult"
Assert-True ($setupMenu -notmatch "BindSceneReferences|CreateTargets") "setup has no hinge camera/canvas wiring"
$ownedSources = @(Get-ChildItem $pkg -Recurse -File | Where-Object { $_.Extension -in @(".cs", ".java", ".gradle", ".xml", ".asmdef") })
$staleBranding = @($ownedSources | Select-String -Pattern "com\.samsung\.(gamedev|foldable)|Samsung\.Foldable" -List)
Assert-True ($staleBranding.Count -eq 0) "no stale Samsung package or C# namespaces"

Write-Host "`nNOTE: PASS means static package consistency only; run Unity EditMode tests and the physical-device matrix." -ForegroundColor Yellow

# Device files must be BOM-free JSON
Get-ChildItem (Join-Path $pkg "Editor\Devices\*.device") | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $bom = ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF)
    Assert-True (-not $bom) "no UTF-8 BOM $($_.Name)"
    try {
        $null = Get-Content $_.FullName -Raw | ConvertFrom-Json
        Assert-True $true "json $($_.Name)"
    } catch {
        Assert-True $false "json $($_.Name)"
    }
}

$projManifestPath = Join-Path $projRoot "Packages\manifest.json"
if (Test-Path $projManifestPath) {
    Assert-True (Test-Path (Join-Path $projRoot "Assets\ResponsiveUI\FoldAndFlip")) "project has Assets/ResponsiveUI/FoldAndFlip"
}

Write-Host "`n=== Layout posture math (mirrored) ===" -ForegroundColor Cyan

function Resolve-Posture($state, $orientation, $isSeparating) {
    if ($isSeparating -and $state -eq "Flat") {
        if ($orientation -eq "Vertical") { return "Book" }
        return "DualPane"
    }
    if ($state -ne "HalfOpened") { return "Flat" }
    if ($orientation -eq "Vertical") { return "Book" }
    return "Tabletop"
}

function New-Simulated($state, $orientation, $width, $height, $hinge, $thickness) {
    if ($state -eq "Undefined") { $state = "Flat" }
    if ($orientation -eq "Undefined") { $orientation = "Horizontal" }
    $width = [Math]::Max(1, $width)
    $height = [Math]::Max(1, $height)
    $hinge = [Math]::Max(0.0, [Math]::Min(1.0, $hinge))
    $thickness = [Math]::Max(1, [Math]::Min($thickness, [Math]::Min($width, $height)))
    if ($orientation -eq "Vertical") {
        $x = [Math]::Max(0, [Math]::Min([int][Math]::Round($width * $hinge) - [int]($thickness / 2), $width - $thickness))
        return @{ state=$state; orientation=$orientation; xMin=$x; xMax=$x+$thickness; yMin=0; yMax=$height; w=$width; h=$height }
    } else {
        $y = [Math]::Max(0, [Math]::Min([int][Math]::Round($height * $hinge) - [int]($thickness / 2), $height - $thickness))
        return @{ state=$state; orientation=$orientation; xMin=0; xMax=$width; yMin=$y; yMax=$y+$thickness; w=$width; h=$height }
    }
}

Assert-True ((Resolve-Posture "Flat" "Horizontal" $false) -eq "Flat") "Flat -> Flat"
Assert-True ((Resolve-Posture "HalfOpened" "Horizontal" $false) -eq "Tabletop") "HalfOpened+H -> Tabletop"
Assert-True ((Resolve-Posture "HalfOpened" "Vertical" $false) -eq "Book") "HalfOpened+V -> Book"
Assert-True ((Resolve-Posture "Flat" "Vertical" $true) -eq "Book") "Flat+sep+V -> Book"
Assert-True ((Resolve-Posture "Flat" "Horizontal" $true) -eq "DualPane") "Flat+sep+H -> DualPane"

$t = New-Simulated "HalfOpened" "Horizontal" 1080 2000 0.5 20
Assert-True ($t.yMin -ge 900 -and $t.yMax -le 1100) "tabletop hinge mid-ish ($($t.yMin)-$($t.yMax))"
# Samsung Flex: cam = Rect(0, upperY, 1, upperY) — for mid hinge upperY≈0.5, yMax≈1
$upperY = $t.yMax / [double]$t.h
Assert-True (($upperY + $upperY) -le 1.05) "Samsung Flex camera band mid ($upperY + $upperY)"
Assert-True ($upperY -gt 0.4 -and $upperY -lt 0.6) "Samsung Flex upperY mid-ish"

$b = New-Simulated "HalfOpened" "Vertical" 2000 1080 0.5 20
Assert-True ($b.xMin -ge 900 -and $b.xMax -le 1100) "book hinge mid-ish ($($b.xMin)-$($b.xMax))"
$rightX = $b.xMax / [double]$b.w
$camW = [Math]::Max(0.0, 1.0 - $rightX)
Assert-True (($rightX + $camW) -le 1.0001) "book camera rect <= 1"

$c = New-Simulated "Undefined" "Undefined" 0 -5 2 9999
Assert-True ($c.w -eq 1 -and $c.h -eq 1) "invalid metrics clamped to 1x1"

# Child scripts call `exit`; invoke them in a separate process so this gate can
# continue (and so stdout is not shared through a pipe that can deadlock).
function Invoke-NestedGate([string]$scriptPath) {
    $outFile = Join-Path ([IO.Path]::GetTempPath()) ("fold-gate-{0}-out.log" -f [guid]::NewGuid().ToString("n"))
    $errFile = Join-Path ([IO.Path]::GetTempPath()) ("fold-gate-{0}-err.log" -f [guid]::NewGuid().ToString("n"))
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

Write-Host "`n=== Resolution matrix (delegated) ===" -ForegroundColor Cyan
$matrixScript = Join-Path $PSScriptRoot "test-resolutions.ps1"
Assert-True (Test-Path $matrixScript) "exists test-resolutions.ps1"
if (Test-Path $matrixScript) {
    Assert-True ((Invoke-NestedGate $matrixScript) -eq 0) "resolution matrix suite"
}

Write-Host "`n=== Full device resolution audit (delegated) ===" -ForegroundColor Cyan
$auditScript = Join-Path $PSScriptRoot "audit-all-device-resolutions.ps1"
Assert-True (Test-Path $auditScript) "exists audit-all-device-resolutions.ps1"
if (Test-Path $auditScript) {
    Assert-True ((Invoke-NestedGate $auditScript) -eq 0) "device resolution audit suite"
}

# Real-device tabletop hinge mid on Fold Inner 1812×2176
$foldHalf = New-Simulated "HalfOpened" "Horizontal" 1812 2176 0.5 24
Assert-True ($foldHalf.yMin -ge 1000 -and $foldHalf.yMax -le 1170) "ZFold Inner tabletop hinge mid ($($foldHalf.yMin)-$($foldHalf.yMax))"
$flipHalf = New-Simulated "HalfOpened" "Horizontal" 1080 2640 0.5 24
Assert-True ($flipHalf.yMin -ge 1240 -and $flipHalf.yMax -le 1400) "ZFlip6 Main tabletop hinge mid ($($flipHalf.yMin)-$($flipHalf.yMax))"

Write-Host "`n=== Summary ===" -ForegroundColor Cyan
Write-Host "Passed: $passed"
Write-Host "Failed: $failed"
Write-Host "Skipped: $skipped (host-project assets)"
if ($failed -gt 0) { exit 1 } else { exit 0 }
