// iPhone Duo hinge bridge (UIKit UIHingeInteraction, iOS 27.1+).
// Classes and selectors are resolved at runtime so this file builds with any Xcode.
// FDPIphoneDuo_Start returns 0 only when UIHingeInteraction cannot be resolved (iOS < 27.1, or a
// renamed class / initializer). Non-foldable devices are filtered in C# by the model list
// (IphoneDuoHinge.IsSupportedDevice), so Unity keeps the hardware Phone / Pad path there.
// Status codes match IphoneDuoHingeStatus.

#import <UIKit/UIKit.h>
#import <objc/runtime.h>
#import <objc/message.h>
#import <sys/sysctl.h>
#import <QuartzCore/QuartzCore.h>

extern "C" UIViewController* UnityGetGLViewController(void);

enum
{
    FDPHingeNone = -1,
    FDPHingeUnknown = 0,
    FDPHingeClosed = 1,
    FDPHingePartiallyOpen = 2,
    FDPHingeFullyOpen = 3,
};

// Angle fallback thresholds (radians). A status is entered at one angle and left only past a
// wider one, so sensor noise near the edge (e.g. resting almost flat) does not flip it.
static const double FDPClosedEnterAngle = 10.0 * M_PI / 180.0;
static const double FDPClosedExitAngle = 20.0 * M_PI / 180.0;
static const double FDPFullyOpenEnterAngle = 170.0 * M_PI / 180.0;
static const double FDPFullyOpenExitAngle = 160.0 * M_PI / 180.0;

@protocol FDPHingeInteractionInit
- (instancetype)initWithUpdateHandler:(void (^)(id interaction, id update))handler;
@end

static id<UIInteraction> s_Interaction;
static __weak UIView* s_View;
static int s_Status = FDPHingeNone;
static int s_Version;

static NSNumber* ReadNumber(id target, NSString* key)
{
    if (![target respondsToSelector:NSSelectorFromString(key)])
        return nil;
    id value = [target valueForKey:key];
    return [value isKindOfClass:NSNumber.class] ? value : nil;
}

static int StatusFromAngle(double angle, int previous)
{
    if (angle < 0.0)
        return FDPHingeUnknown;

    double closedMax = previous == FDPHingeClosed ? FDPClosedExitAngle : FDPClosedEnterAngle;
    if (angle <= closedMax)
        return FDPHingeClosed;

    double fullyOpenMin = previous == FDPHingeFullyOpen ? FDPFullyOpenExitAngle : FDPFullyOpenEnterAngle;
    if (angle >= fullyOpenMin)
        return FDPHingeFullyOpen;
    return FDPHingePartiallyOpen;
}

static int MapStatus(NSNumber* raw, double angle)
{
    // UIHingeStatus values are ABI-stable and equal FDPHinge* (Unknown 0, Closed 1, PartiallyOpen 2,
    // FullyOpen 3), so the system status is honoured whatever SDK built this file.
    if (raw != nil)
    {
        switch (raw.integerValue)
        {
            case FDPHingeClosed: return FDPHingeClosed;
            case FDPHingePartiallyOpen: return FDPHingePartiallyOpen;
            case FDPHingeFullyOpen: return FDPHingeFullyOpen;
            default: break;
        }
    }
    // No status, or the system reports Unknown: derive it from the angle (radians, 0 closed,
    // pi flat) with hysteresis against the current status.
    return StatusFromAngle(angle, s_Status);
}

static void ApplyUpdate(id update)
{
    id hinge = [update respondsToSelector:NSSelectorFromString(@"hinge")]
        ? [update valueForKey:@"hinge"]
        : nil;

    // A nil hinge means the view left a hierarchy that provides hinge updates (for example under
    // a full-screen modal such as an ad). That is not a posture: keep the last status.
    if (hinge == nil)
        return;

    NSNumber* angleValue = ReadNumber(hinge, @"angle");
    double angle = angleValue != nil ? angleValue.doubleValue : -1.0;
    int status = MapStatus(ReadNumber(hinge, @"status"), angle);

    // C# only maps the status, so angle-only changes do not bump the version.
    if (status == s_Status)
        return;

    s_Status = status;
    s_Version++;
}

/// UIHingeInteraction when this iOS provides it (27.1+), else Nil.
static Class HingeInteractionClass(void)
{
    Class cls = NSClassFromString(@"UIHingeInteraction");
    if (cls == Nil ||
        ![cls conformsToProtocol:@protocol(UIInteraction)] ||
        ![cls instancesRespondToSelector:@selector(initWithUpdateHandler:)])
        return Nil;
    return cls;
}

extern "C" int FDPIphoneDuo_Start(void)
{
    if (s_Interaction != nil)
        return 1;

    Class cls = HingeInteractionClass();
    if (cls == Nil)
        return 0;

    // Handler runs on the main thread with the initial state, then on every hinge change.
    // Updates from an interaction that was already removed (Stop) are ignored.
    id<UIInteraction> interaction = (id<UIInteraction>)[(id<FDPHingeInteractionInit>)[cls alloc]
        initWithUpdateHandler:^(id sender, id update) {
            if (sender == s_Interaction)
                ApplyUpdate(update);
        }];

    // Set before addInteraction: so an initial update delivered synchronously is accepted.
    UIView* view = UnityGetGLViewController().view;
    s_Interaction = interaction;
    s_View = view;
    [view addInteraction:interaction];
    return 1;
}

extern "C" void FDPIphoneDuo_Stop(void)
{
    if (s_Interaction == nil)
        return;

    [s_View removeInteraction:s_Interaction];
    s_Interaction = nil;
    s_View = nil;
    s_Status = FDPHingeNone;
    s_Version++;
}

/// While disabled UIKit neither calls nor queues hinge updates; re-enabling calls the handler
/// with the current hinge state, so a fold made while the app was paused is picked up.
extern "C" void FDPIphoneDuo_SetEnabled(int enabled)
{
    id interaction = s_Interaction;
    if (interaction == nil || ![interaction respondsToSelector:@selector(setEnabled:)])
        return;
    [interaction setValue:@(enabled != 0) forKey:@"enabled"];
}

extern "C" int FDPIphoneDuo_GetStatus(void) { return s_Status; }

extern "C" int FDPIphoneDuo_GetVersion(void) { return s_Version; }

/// Unity view size in points (independent of Unity's render-resolution scaling).
extern "C" float FDPIphoneDuo_GetViewPointWidth(void)
{
    return (float)UnityGetGLViewController().view.bounds.size.width;
}

extern "C" float FDPIphoneDuo_GetViewPointHeight(void)
{
    return (float)UnityGetGLViewController().view.bounds.size.height;
}

// Flat (Opened) inner display: Android-style landscape lock. While locked, iOS keeps the current
// interface orientation when the device turns, so a held-tall landscape game stays full screen
// (sideways) instead of being letterboxed. The 180° flip runs unlocked
// (FDPIphoneDuo_RequestInterfaceOrientation): a rotation requested while locked is reported, not shown.

static BOOL s_InterfaceOrientationLocked;

static BOOL FDPPrefersInterfaceOrientationLocked(id self, SEL _cmd) { return s_InterfaceOrientationLocked; }

static UIWindowScene* FDPUnityWindowScene(void)
{
    return UnityGetGLViewController().view.window.windowScene;
}

extern "C" void FDPIphoneDuo_SetInterfaceOrientationLocked(int locked)
{
    if (@available(iOS 26.0, *))
    {
        s_InterfaceOrientationLocked = locked != 0;
        UIViewController* controller = UnityGetGLViewController();
        if (controller == nil)
            return;

        // Unity's view controller does not override prefersInterfaceOrientationLocked: add the
        // override to its class (or replace it when it is already the class's own).
        Class cls = object_getClass(controller);
        SEL sel = @selector(prefersInterfaceOrientationLocked);
        if (!class_addMethod(cls, sel, (IMP)FDPPrefersInterfaceOrientationLocked, "B@:"))
            method_setImplementation(class_getInstanceMethod(cls, sel), (IMP)FDPPrefersInterfaceOrientationLocked);
        [controller setNeedsUpdateOfPrefersInterfaceOrientationLocked];
    }
}

/// True while the Unity scene's interface orientation is locked (iOS 26+).
extern "C" int FDPIphoneDuo_IsInterfaceOrientationLocked(void)
{
    if (@available(iOS 26.0, *))
    {
        UIWindowScene* scene = FDPUnityWindowScene();
        return scene != nil && scene.effectiveGeometry.isInterfaceOrientationLocked ? 1 : 0;
    }
    return 0;
}

/// UIInterfaceOrientation of the Unity scene (0 when unknown).
extern "C" int FDPIphoneDuo_GetInterfaceOrientation(void)
{
    UIWindowScene* scene = FDPUnityWindowScene();
    return scene != nil ? (int)scene.interfaceOrientation : 0;
}

/// Rotates the Unity scene to a UIInterfaceOrientation (1-4), also while locked.
extern "C" void FDPIphoneDuo_RequestInterfaceOrientation(int orientation)
{
    if (@available(iOS 16.0, *))
    {
        UIWindowScene* scene = FDPUnityWindowScene();
        if (scene == nil || orientation < 1 || orientation > 4)
            return;
        UIWindowSceneGeometryPreferencesIOS* preferences = [[UIWindowSceneGeometryPreferencesIOS alloc]
            initWithInterfaceOrientations:(UIInterfaceOrientationMask)(1 << orientation)];
        [scene requestGeometryUpdateWithPreferences:preferences errorHandler:^(NSError* error) {
            NSLog(@"iPhone Duo: interface orientation %d request failed: %@", orientation, error);
        }];
    }
}

// Sideways mode (a one-axis game held across its lock on the inner display, from Seated, a cold
// start, an unfold or the launch hold): an interface across the hold is letterboxed by iOS, lock or
// not. iOS keeps the interface of the hold's axis instead and the Unity view is turned a quarter with
// swapped bounds, so the game renders its own size and fills the display sideways (Android Fold
// style; a landscape game held tall or a portrait game held wide). Touches follow the view
// transform. 0 restores the plain view. Idempotent: called every frame while turned, in case UIKit
// lays the root view out again.
extern "C" void FDPIphoneDuo_SetContentQuarterTurns(int turns)
{
    UIView* view = UnityGetGLViewController().view;
    UIView* parent = view.superview;
    if (view == nil || parent == nil)
        return;

    CGSize size = parent.bounds.size;
    CGSize bounds = (turns & 1) != 0 ? CGSizeMake(size.height, size.width) : size;
    CGAffineTransform transform = CGAffineTransformMakeRotation((CGFloat)(turns * M_PI_2));
    if (CGAffineTransformEqualToTransform(view.transform, transform) &&
        CGSizeEqualToSize(view.bounds.size, bounds))
        return;

    view.transform = transform;
    view.bounds = CGRectMake(0, 0, bounds.width, bounds.height);
    view.center = CGPointMake(size.width / 2, size.height / 2);
}

// Launch hold: the Unity splash and the first scenes run before C# reads the posture. Held across a
// one-axis Player lock on the inner display (a landscape game held tall), iOS would letterbox them.
// iOS builds write the posture rules to Info.plist (IphoneDuoBuildPlist), and from launch this
// applies the same rule C# applies later (IphoneDuoHoldOrientation): Sideways allows the upright
// orientation of the hold and turns the view, Rotate allows it without the turn, System Default
// leaves the Player lock. The posture comes from the hinge, started here early. C# adopts the hold
// on its first posture frame (FDPIphoneDuo_GetLaunchHoldMask / GetLaunchHoldTurns /
// ReleaseLaunchHold); turning the device to the game's axis before that ends it here (while Seated
// with the Locked Seated rotation, or flat with a Locked Flat that keeps a hold of its own, only when
// the kept hold is on it), and so does
// FDPLaunchHoldDeadline when C# never takes it over (no posture detector).

// IphoneDuoAcrossHold values.
enum { FDPRuleSideways = 0, FDPRuleRotate = 1, FDPRuleSystemDefault = 2 };
// IphoneDuoSeatedRotation and IphoneDuoSeatedPose values.
enum { FDPSeatedLocked = 0, FDPSeatedFollowsHold = 1 };
enum { FDPSeatedPoseAuto = 0, FDPSeatedPosePortrait = 1, FDPSeatedPoseLandscape = 2 };
// IphoneDuoFlatRotation and IphoneDuoFlatPose values.
enum { FDPFlatFollowDevice = 0, FDPFlatLocked = 1 };
enum { FDPFlatPoseAuto = 0, FDPFlatPoseWide = 1, FDPFlatPoseTall = 2 };

// Inner display in landscape points and the cover / inner area split (C# IphoneDuoHinge
// InnerLongPoints / InnerShortPoints, FoldableCoverHeuristics.CoverAreaRatio).
static const double FDPInnerLongPoints = 951.0;
static const double FDPInnerShortPoints = 669.0;
static const double FDPCoverAreaRatio = 0.7;

// Seconds after launch the hold ends by itself when C# never requested its release.
static const CFTimeInterval FDPLaunchHoldDeadline = 20.0;

static BOOL s_LaunchArmed;                           // Info.plist enables it on this model
static BOOL s_TraceLog;                              // development builds with the rules' trace log
static int s_FlatRule = FDPRuleSystemDefault;
static int s_SeatedRule = FDPRuleSystemDefault;
static int s_SeatedRotation = FDPSeatedLocked;
static int s_SeatedLockedPose = FDPSeatedPoseAuto;
static UIDeviceOrientation s_SeatedDevice;           // the hold a Locked Seated keeps (Unknown: none yet)
static int s_FlatRotation = FDPFlatFollowDevice;
static int s_FlatLockedPose = FDPFlatPoseAuto;
static UIDeviceOrientation s_FlatDevice;             // the hold a Locked Flat keeps (Unknown: none yet)
static int s_SidewaysTurns;                          // IphoneDuoRules.SidewaysQuarterTurns
static UIInterfaceOrientationMask s_LaunchHoldMask;   // 0: no launch hold
static int s_LaunchHoldTurns;                        // quarter turns the launch hold gave the view
static BOOL s_LaunchHoldDone;                        // C# took over, or the hold does not apply
static IMP s_UnitySupportedOrientations;
static NSTimer* s_LaunchTimer;
static BOOL s_ReleaseRequested;
static CFTimeInterval s_ReleaseRequestedAt;
static UIInterfaceOrientationMask s_UnityMaskAtRelease;
static CFTimeInterval s_LaunchedAt;
static id s_LaunchObserver;                          // UIApplicationDidFinishLaunchingNotification
static id s_DeviceObserver;                          // UIDeviceOrientationDidChangeNotification
static BOOL s_GeneratingDeviceOrientation;

static BOOL FDPOneAxis(UIInterfaceOrientationMask mask)
{
    BOOL portrait = (mask & (UIInterfaceOrientationMaskPortrait | UIInterfaceOrientationMaskPortraitUpsideDown)) != 0;
    BOOL landscape = (mask & UIInterfaceOrientationMaskLandscape) != 0;
    return portrait != landscape;
}

static UIInterfaceOrientationMask FDPAxisMask(UIInterfaceOrientation orientation)
{
    return UIInterfaceOrientationIsPortrait(orientation)
        ? UIInterfaceOrientationMaskPortrait | UIInterfaceOrientationMaskPortraitUpsideDown
        : UIInterfaceOrientationMaskLandscape;
}

static BOOL FDPInnerSize(CGSize size)
{
    return size.width * size.height >= FDPInnerLongPoints * FDPInnerShortPoints * FDPCoverAreaRatio;
}

// The app's window scene, also before Unity's window exists.
static UIWindowScene* FDPAppWindowScene(void)
{
    for (UIScene* scene in UIApplication.sharedApplication.connectedScenes)
    {
        if ([scene isKindOfClass:[UIWindowScene class]])
            return (UIWindowScene*)scene;
    }
    return nil;
}

static NSUInteger FDPSupportedInterfaceOrientations(id self, SEL _cmd)
{
    NSUInteger unity = ((NSUInteger (*)(id, SEL))s_UnitySupportedOrientations)(self, _cmd);
    if (s_LaunchHoldMask != 0)
        return s_LaunchHoldMask;

    // Launched on the inner display in an orientation the Player lock rules out (the launch screen
    // lists the other axis), before the hold knows the posture: keep its axis allowed. iOS sizes
    // Unity's window for the scene only on a real rotation, so a window created landscape-only would
    // stay landscape-sized inside a portrait scene. The whole axis, because iOS gives an iPhone app no
    // Portrait Upside Down window: launched in it, the window takes Portrait.
    if (s_LaunchArmed && !s_LaunchHoldDone && FDPOneAxis(unity))
    {
        UIWindowScene* scene = FDPAppWindowScene();
        UIInterfaceOrientation current = scene != nil ? scene.interfaceOrientation : UIInterfaceOrientationUnknown;
        if (current != UIInterfaceOrientationUnknown && (unity & (1 << current)) == 0 &&
            FDPInnerSize(scene.coordinateSpace.bounds.size))
        {
            static BOOL s_Logged;
            if (s_TraceLog && !s_Logged)
            {
                s_Logged = YES;
                NSLog(@"iPhone Duo: launch keeps interface %ld (Unity mask %lu)", (long)current, (unsigned long)unity);
            }
            return unity | FDPAxisMask(current);
        }
    }
    return unity;
}

// Replaces supportedInterfaceOrientations on Unity's view controller class only (added when the
// class inherits it), keeping Unity's implementation for FDPUnityMask.
static void FDPInstallOrientationOverride(Class cls)
{
    if (s_UnitySupportedOrientations != NULL || cls == Nil)
        return;
    SEL selector = @selector(supportedInterfaceOrientations);
    Method method = class_getInstanceMethod(cls, selector);
    if (method == NULL)
        return;
    s_UnitySupportedOrientations = method_getImplementation(method);
    if (!class_addMethod(cls, selector, (IMP)FDPSupportedInterfaceOrientations, method_getTypeEncoding(method)))
        method_setImplementation(method, (IMP)FDPSupportedInterfaceOrientations);
}

static NSString* FDPModelIdentifier(void)
{
    NSString* simulated = NSProcessInfo.processInfo.environment[@"SIMULATOR_MODEL_IDENTIFIER"];
    if (simulated.length > 0)
        return simulated;
    char machine[64] = {0};
    size_t size = sizeof(machine) - 1;
    return sysctlbyname("hw.machine", machine, &size, NULL, 0) == 0 ? @(machine) : @"";
}

// Unity's own orientation mask (Player Settings / Screen.autorotateTo*), without the launch hold.
static UIInterfaceOrientationMask FDPUnityMask(UIViewController* controller)
{
    return s_UnitySupportedOrientations != NULL
        ? (UIInterfaceOrientationMask)((NSUInteger (*)(id, SEL))s_UnitySupportedOrientations)(controller, @selector(supportedInterfaceOrientations))
        : controller.supportedInterfaceOrientations;
}

// Inner display: device orientation axes are turned 90° from the panel (C# IphoneDuoHoldOrientation).
static UIInterfaceOrientationMask FDPInnerHoldMask(UIDeviceOrientation device)
{
    switch (device)
    {
        case UIDeviceOrientationLandscapeRight:
        case UIDeviceOrientationLandscapeLeft:
            return UIInterfaceOrientationMaskPortrait | UIInterfaceOrientationMaskPortraitUpsideDown;
        case UIDeviceOrientationPortrait:
        case UIDeviceOrientationPortraitUpsideDown:
            return UIInterfaceOrientationMaskLandscape;
        default:
            return 0;
    }
}

// Interface orientation upright for the hold (C# IphoneDuoFlatOrientation.UprightInterface).
static UIInterfaceOrientation FDPInnerUpright(UIDeviceOrientation device)
{
    switch (device)
    {
        case UIDeviceOrientationLandscapeLeft: return UIInterfaceOrientationPortrait;
        case UIDeviceOrientationLandscapeRight: return UIInterfaceOrientationPortraitUpsideDown;
        case UIDeviceOrientationPortrait: return UIInterfaceOrientationLandscapeLeft;
        case UIDeviceOrientationPortraitUpsideDown: return UIInterfaceOrientationLandscapeRight;
        default: return UIInterfaceOrientationUnknown;
    }
}

static UIInterfaceOrientation FDPOpposite(UIInterfaceOrientation orientation)
{
    switch (orientation)
    {
        case UIInterfaceOrientationPortrait: return UIInterfaceOrientationPortraitUpsideDown;
        case UIInterfaceOrientationPortraitUpsideDown: return UIInterfaceOrientationPortrait;
        case UIInterfaceOrientationLandscapeLeft: return UIInterfaceOrientationLandscapeRight;
        case UIInterfaceOrientationLandscapeRight: return UIInterfaceOrientationLandscapeLeft;
        default: return UIInterfaceOrientationUnknown;
    }
}

// Seated: the device orientation of the hold the rules keep (C# IphoneDuoRules.SeatedHoldTall and
// IphoneDuoHoldOrientation.SeatedHoldDevice). Locked keeps the pose's hold while the device turns to
// the other one; the device's own side of that hold still counts, so the game stays upright. The
// launch hold applies to one-axis games only, so Auto never needs the entry hold of a two-axis game.
static UIDeviceOrientation FDPSeatedHoldDevice(UIDeviceOrientation device, UIInterfaceOrientationMask unity)
{
    UIInterfaceOrientationMask axis = FDPInnerHoldMask(device);
    if (axis == 0 || s_SeatedRotation != FDPSeatedLocked)
        return device;
    BOOL holdTall = (axis & UIInterfaceOrientationMaskPortrait) != 0;
    BOOL tall = holdTall;
    if (s_SeatedLockedPose == FDPSeatedPosePortrait)
        tall = YES;
    else if (s_SeatedLockedPose == FDPSeatedPoseLandscape)
        tall = NO;
    else if (FDPOneAxis(unity))
        tall = (unity & UIInterfaceOrientationMaskLandscape) != 0;
    if (holdTall != tall)
    {
        UIInterfaceOrientationMask kept = FDPInnerHoldMask(s_SeatedDevice);
        device = kept != 0 && ((kept & UIInterfaceOrientationMaskPortrait) != 0) == tall
            ? s_SeatedDevice
            : tall ? UIDeviceOrientationLandscapeLeft : UIDeviceOrientationPortrait;
    }
    s_SeatedDevice = device;
    return device;
}

// Flat: the hold a Locked Flat keeps (C# IphoneDuoRules.FlatHoldWide), true for wide. The launch hold
// applies to one-axis games only, so Auto never needs the entry hold of a two-axis game.
static BOOL FDPFlatLockedWide(BOOL holdWide, UIInterfaceOrientationMask unity)
{
    if (s_FlatLockedPose == FDPFlatPoseWide)
        return YES;
    if (s_FlatLockedPose == FDPFlatPoseTall)
        return NO;
    return FDPOneAxis(unity) ? (unity & UIInterfaceOrientationMaskLandscape) != 0 : holdWide;
}

// A Locked Flat on a one-axis game's own axis keeps the real hold (C# IphoneDuoRules.FlatKeepsHold).
static BOOL FDPFlatOnGameAxis(UIInterfaceOrientationMask unity)
{
    return s_FlatRotation == FDPFlatLocked && FDPOneAxis(unity) &&
        FDPFlatLockedWide(NO, unity) == ((unity & UIInterfaceOrientationMaskLandscape) != 0);
}

// Flat: the device orientation of the hold the rules keep (C# IphoneDuoHoldOrientation.FlatHoldDevice),
// like FDPSeatedHoldDevice. Locked keeps the pose's hold while the device turns to the other one; the
// device's own side of that hold still counts, so the game stays upright. On the game's own axis the
// real hold stays (held across it, FDPFlatRule turns the game sideways).
static UIDeviceOrientation FDPFlatHoldDevice(UIDeviceOrientation device, UIInterfaceOrientationMask unity)
{
    UIInterfaceOrientationMask axis = FDPInnerHoldMask(device);
    if (axis == 0 || s_FlatRotation != FDPFlatLocked || FDPFlatOnGameAxis(unity))
        return device;
    BOOL holdWide = (axis & UIInterfaceOrientationMaskLandscape) != 0;
    BOOL wide = FDPFlatLockedWide(holdWide, unity);
    if (holdWide != wide)
    {
        UIInterfaceOrientationMask kept = FDPInnerHoldMask(s_FlatDevice);
        device = kept != 0 && ((kept & UIInterfaceOrientationMaskLandscape) != 0) == wide
            ? s_FlatDevice
            : wide ? UIDeviceOrientationPortrait : UIDeviceOrientationLandscapeLeft;
    }
    s_FlatDevice = device;
    return device;
}

// The flat across-lock rule (C# IphoneDuoRules.FlatAcrossLockFor): a Locked Flat on the game's own
// axis never rotates, so Rotate keeps the game sideways.
static int FDPFlatRule(UIInterfaceOrientationMask unity)
{
    return s_FlatRule == FDPRuleRotate && FDPFlatOnGameAxis(unity) ? FDPRuleSideways : s_FlatRule;
}

static void FDPRefreshOrientations(UIViewController* controller)
{
    if (@available(iOS 16.0, *))
        [controller setNeedsUpdateOfSupportedInterfaceOrientations];
}

static UIInterfaceOrientation FDPUnityInterfaceOrientation(void)
{
    id app = UIApplication.sharedApplication.delegate;
    SEL getter = NSSelectorFromString(@"interfaceOrientation");
    return [app respondsToSelector:getter]
        ? ((UIInterfaceOrientation (*)(id, SEL))objc_msgSend)(app, getter)
        : UIInterfaceOrientationUnknown;
}

// Unity tracks the interface orientation itself (UnityAppController.interfaceOrientation). A rotation
// iOS makes on request may not reach it: hand Unity the orientation iOS shows.
static void FDPSyncUnityOrientation(UIInterfaceOrientation orientation)
{
    id app = UIApplication.sharedApplication.delegate;
    SEL update = NSSelectorFromString(@"updateAppOrientation:");
    if (orientation == UIInterfaceOrientationUnknown || ![app respondsToSelector:update])
        return;
    if (FDPUnityInterfaceOrientation() != orientation)
        ((void (*)(id, SEL, UIInterfaceOrientation))objc_msgSend)(app, update, orientation);
}

static void FDPEndLaunchHold(BOOL keepTurn)
{
    [s_LaunchTimer invalidate];
    s_LaunchTimer = nil;
    s_LaunchHoldDone = YES;
    // The hold no longer follows the device: stop the launch observers and orientation updates.
    NSNotificationCenter* center = NSNotificationCenter.defaultCenter;
    if (s_LaunchObserver != nil)
    {
        [center removeObserver:s_LaunchObserver];
        s_LaunchObserver = nil;
    }
    if (s_DeviceObserver != nil)
    {
        [center removeObserver:s_DeviceObserver];
        s_DeviceObserver = nil;
    }
    if (s_GeneratingDeviceOrientation)
    {
        s_GeneratingDeviceOrientation = NO;
        [UIDevice.currentDevice endGeneratingDeviceOrientationNotifications];
    }
    UIViewController* controller = UnityGetGLViewController();
    if (s_LaunchHoldMask != 0)
    {
        s_LaunchHoldMask = 0;
        if (!keepTurn && s_LaunchHoldTurns != 0)
            FDPIphoneDuo_SetContentQuarterTurns(0);
    }
    s_LaunchHoldTurns = 0;
    if (controller != nil)
        FDPRefreshOrientations(controller);
}

static void FDPUpdateLaunchHold(void)
{
    if (s_LaunchHoldDone || s_ReleaseRequested)
        return;
    UIViewController* controller = UnityGetGLViewController();
    if (controller == nil || controller.view.window == nil)
        return;
    FDPInstallOrientationOverride(object_getClass(controller));
    // The posture: partly open is Seated, otherwise flat (the hinge reports shortly after start).
    FDPIphoneDuo_Start();

    // Cover-size view: the cover is an iPhone, nothing to hold.
    if (!FDPInnerSize(controller.view.window.windowScene.coordinateSpace.bounds.size))
    {
        FDPEndLaunchHold(NO);
        return;
    }

    UIInterfaceOrientationMask unity = FDPUnityMask(controller);
    UIDeviceOrientation device = UIDevice.currentDevice.orientation;
    if (s_Status == FDPHingePartiallyOpen)
    {
        device = FDPSeatedHoldDevice(device, unity);
        s_FlatDevice = UIDeviceOrientationUnknown;
    }
    else
    {
        s_SeatedDevice = UIDeviceOrientationUnknown;
        device = FDPFlatHoldDevice(device, unity);
    }
    UIInterfaceOrientationMask axis = FDPInnerHoldMask(device);
    if (axis == 0)
        return;   // face up / down / unknown: keep what is shown
    if (!FDPOneAxis(unity) || (axis & unity) != 0)
    {
        // Held on the game's axis: no hold (iOS rotates to it full screen).
        if (s_LaunchHoldMask != 0)
            FDPEndLaunchHold(NO);
        return;
    }

    int rule = s_Status == FDPHingePartiallyOpen ? s_SeatedRule : FDPFlatRule(unity);
    if (rule == FDPRuleSystemDefault)
    {
        FDPEndLaunchHold(NO);
        return;
    }

    UIWindowScene* scene = controller.view.window.windowScene;
    UIInterfaceOrientation current = scene.interfaceOrientation;
    // The upright interface, except that a tall hold always gets Portrait: iOS gives an iPhone app no
    // Portrait Upside Down window (C# IphoneDuoHoldOrientation.HoldFlags). The view turn makes up for it.
    UIInterfaceOrientation upright = FDPInnerUpright(device);
    UIInterfaceOrientationMask hold = UIInterfaceOrientationIsPortrait(upright)
        ? UIInterfaceOrientationMaskPortrait
        : (UIInterfaceOrientationMask)(1 << upright);
    if (s_LaunchHoldMask != hold)
    {
        if (s_LaunchHoldMask == 0)
            NSLog(@"iPhone Duo: launch hold (mask %lu, device %ld, rule %d)", (unsigned long)hold, (long)device, rule);
        s_LaunchHoldMask = hold;
        FDPRefreshOrientations(controller);
        if (@available(iOS 16.0, *))
        {
            UIWindowSceneGeometryPreferencesIOS* preferences =
                [[UIWindowSceneGeometryPreferencesIOS alloc] initWithInterfaceOrientations:hold];
            [scene requestGeometryUpdateWithPreferences:preferences errorHandler:nil];
        }
    }

    // The view turn so the game reads upright (C# IphoneDuoHoldOrientation.ViewTurns). Sideways: the
    // configured turn from the upright orientation, the opposite turn from the upside-down one.
    // Rotate: 180° from the upside-down one.
    BOOL shown = current == upright || current == FDPOpposite(upright);
    int turns = 0;
    if (shown)
    {
        BOOL upsideDown = current != upright;
        if (rule == FDPRuleSideways)
            turns = upsideDown ? -s_SidewaysTurns : s_SidewaysTurns;
        else if (upsideDown)
            turns = 2;
        FDPSyncUnityOrientation(current);
    }
    if (turns != 0 || s_LaunchHoldTurns != 0)
        FDPIphoneDuo_SetContentQuarterTurns(turns);
    s_LaunchHoldTurns = turns;
    // Development builds: one line per change, for simulator / hardware QA.
    if (!s_TraceLog)
        return;
    static NSString* s_LastTrace;
    CGSize window = controller.view.window.bounds.size;
    CGSize sceneSize = scene.coordinateSpace.bounds.size;
    NSString* trace = [NSString stringWithFormat:@"device %ld interface %ld status %d rule %d turns %d window %.0fx%.0f scene %.0fx%.0f unity %ld",
        (long)device, (long)current, s_Status, rule, turns, window.width, window.height, sceneSize.width, sceneSize.height,
        (long)FDPUnityInterfaceOrientation()];
    if (![trace isEqualToString:s_LastTrace])
    {
        s_LastTrace = trace;
        NSLog(@"iPhone Duo: launch hold %@", trace);
    }
}

@interface FDPIphoneDuoLaunchHold : NSObject
@end

@implementation FDPIphoneDuoLaunchHold

+ (void)load
{
    NSDictionary* info = NSBundle.mainBundle.infoDictionary;
    if (![info[@"FDPIphoneDuoLaunchHold"] boolValue])
        return;
    NSArray* models = info[@"FDPIphoneDuoModels"];
    if (![models isKindOfClass:[NSArray class]] || ![models containsObject:FDPModelIdentifier()])
        return;
    s_FlatRule = [info[@"FDPIphoneDuoFlatRule"] intValue];
    s_SeatedRule = [info[@"FDPIphoneDuoSeatedRule"] intValue];
    s_SeatedRotation = [info[@"FDPIphoneDuoSeatedRotation"] intValue];
    s_SeatedLockedPose = [info[@"FDPIphoneDuoSeatedLockedPose"] intValue];
    s_FlatRotation = [info[@"FDPIphoneDuoFlatRotation"] intValue];
    s_FlatLockedPose = [info[@"FDPIphoneDuoFlatLockedPose"] intValue];
    s_SidewaysTurns = [info[@"FDPIphoneDuoSidewaysQuarterTurns"] intValue];
    s_TraceLog = [info[@"FDPIphoneDuoTraceLog"] boolValue];
    if (s_SidewaysTurns == 0)
        s_SidewaysTurns = 1;
    s_LaunchArmed = YES;

    // Before Unity creates its window, so a portrait launch gets a portrait-sized window.
    FDPInstallOrientationOverride(NSClassFromString(@"UnityDefaultViewController"));

    NSNotificationCenter* center = NSNotificationCenter.defaultCenter;
    s_LaunchObserver = [center addObserverForName:UIApplicationDidFinishLaunchingNotification object:nil queue:NSOperationQueue.mainQueue usingBlock:^(NSNotification* note) {
        [NSNotificationCenter.defaultCenter removeObserver:s_LaunchObserver];
        s_LaunchObserver = nil;
        if (s_LaunchHoldDone)
            return;
        s_LaunchedAt = CACurrentMediaTime();
        s_GeneratingDeviceOrientation = YES;
        [UIDevice.currentDevice beginGeneratingDeviceOrientationNotifications];
        FDPUpdateLaunchHold();
        if (s_LaunchHoldDone)
            return;
        // The splash renders before the first device orientation arrives: poll briefly until C# takes over.
        s_LaunchTimer = [NSTimer scheduledTimerWithTimeInterval:0.1 repeats:YES block:^(NSTimer* timer) {
            if (!s_ReleaseRequested)
            {
                // No posture detector took the hold over: end it, straightening the view, so iOS
                // shows the Player orientation (a turned view would stay turned with no one to undo it).
                if (CACurrentMediaTime() - s_LaunchedAt > FDPLaunchHoldDeadline)
                {
                    NSLog(@"iPhone Duo: launch hold not taken over by C# after %.0f s; ending it", FDPLaunchHoldDeadline);
                    FDPEndLaunchHold(NO);
                    return;
                }
                FDPUpdateLaunchHold();
                return;
            }
            // C# owns the hold: end it once Unity applied C#'s Screen flags (or after a second).
            UIViewController* controller = UnityGetGLViewController();
            if (controller == nil || FDPUnityMask(controller) != s_UnityMaskAtRelease ||
                CACurrentMediaTime() - s_ReleaseRequestedAt > 1.0)
                FDPEndLaunchHold(YES);
        }];
    }];
    s_DeviceObserver = [center addObserverForName:UIDeviceOrientationDidChangeNotification object:nil queue:NSOperationQueue.mainQueue usingBlock:^(NSNotification* note) {
        FDPUpdateLaunchHold();
    }];
}

@end

/// UIInterfaceOrientationMask the launch hold allows (0 when none). C# writes it as Screen flags,
/// then calls FDPIphoneDuo_ReleaseLaunchHold.
extern "C" int FDPIphoneDuo_GetLaunchHoldMask(void) { return (int)s_LaunchHoldMask; }

/// Quarter turns the launch hold gave the view (odd: Sideways rule), 0 when straight.
extern "C" int FDPIphoneDuo_GetLaunchHoldTurns(void) { return s_LaunchHoldTurns; }

/// Hands the launch hold to C#, keeping the view as it is. It ends once Unity applied the Screen
/// flags C# wrote (so iOS never sees the Player lock alone in between), or at once when there is none.
extern "C" void FDPIphoneDuo_ReleaseLaunchHold(void)
{
    UIViewController* controller = UnityGetGLViewController();
    if (s_LaunchHoldMask == 0 || controller == nil)
    {
        FDPEndLaunchHold(YES);
        return;
    }
    s_ReleaseRequested = YES;
    s_ReleaseRequestedAt = CACurrentMediaTime();
    s_UnityMaskAtRelease = FDPUnityMask(controller);
}
