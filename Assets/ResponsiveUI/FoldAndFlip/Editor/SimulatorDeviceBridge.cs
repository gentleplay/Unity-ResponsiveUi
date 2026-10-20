using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Best-effort reflection helpers to read/drive Unity Device Simulator selection.
    /// </summary>
    [InitializeOnLoad]
    public static class SimulatorDeviceBridge
    {
        /// <summary>True when a Device Simulator rotation turns the chrome landscape (90° / 270°).</summary>
        public static bool IsLandscapeRotation(int rotationDegrees) =>
            Mathf.Abs(rotationDegrees / 90) % 2 == 1;

        #region Types

        public struct Selection
        {
            public string FriendlyName;
            public string DeviceModel;
            public int DeviceIndex;
            public int ScreenIndex;
            public int ScreenWidth;
            public int ScreenHeight;
            public int RotationDegrees;
            public bool HasRotation;
            public bool IsValid;

            /// <summary>
            /// Catalog panel WxH oriented to Simulator rotation (90/270 swap).
            /// Not the Game View widget size.
            /// </summary>
            public Vector2 OrientedScreenSize
            {
                get
                {
                    if (ScreenWidth <= 0 || ScreenHeight <= 0)
                        return Vector2.zero;

                    if (HasRotation && IsLandscapeRotation(RotationDegrees))
                        return new Vector2(ScreenHeight, ScreenWidth);

                    return new Vector2(ScreenWidth, ScreenHeight);
                }
            }
        }

        sealed class SimulatorEventBinding
        {
            public object Source;
            public EventInfo Event;
            public Delegate Handler;
        }

        #endregion

        #region Runtime state

        /// <summary>
        /// Fired when the active Simulator device/screen/size fingerprint changes
        /// (plugin <c>deviceChanged</c>, session watcher, or an explicit notify).
        /// Notifications are coalesced to one editor delayCall to avoid inspector thrash.
        /// </summary>
        public static event Action SelectionChanged;

        /// <summary>
        /// True while a Unity <c>deviceChanged</c> publish is waiting for a committed
        /// Simulator selection identity. Fold controls hide until this is false.
        /// </summary>
        public static bool IsSelectionPending { get; private set; }

        /// <summary>Last published selection identity (model|friendly|screen|rotation).</summary>
        static string lastPublishedIdentity = "";

        const int SelectionSettleRetries = 2;

        static bool selectionNotifyQueued;
        static int selectionPublishGeneration;
        static object[] cachedSimulatorWindows;
        static double cachedSimulatorWindowsUntil;
        static IDeviceSimulatorSelectionAdapter selectionAdapter =
            new UnityDeviceSimulatorSelectionAdapter();
        static readonly List<SimulatorEventBinding> simulatorEventBindings =
            new List<SimulatorEventBinding>();

        #endregion

        #region Lifecycle

        static SimulatorDeviceBridge()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += RefreshSelectionEventBindings;
        }

        static void OnBeforeAssemblyReload()
        {
            UnbindSelectionEventBindings();
            selectionPublishGeneration++;
            IsSelectionPending = false;
            selectionNotifyQueued = false;
        }

        #endregion

        #region Selection events

        public static void NotifySelectionChanged()
        {
            if (selectionNotifyQueued)
                return;
            selectionNotifyQueued = true;
            EditorApplication.delayCall += () =>
            {
                selectionNotifyQueued = false;
                RaiseSelectionChanged();
            };
        }

        /// <summary>Isolated: one throwing listener must not stop the others.</summary>
        static void RaiseSelectionChanged()
        {
            Action handlers = SelectionChanged;
            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action)subscribers[i]).Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, subscribers[i].Target as UnityEngine.Object);
                }
            }
        }

        /// <summary>
        /// Unity <c>deviceChanged</c> entry. Waits until the reflected selection identity
        /// changes (one coalesced retry chain, not idle polling) then publishes
        /// <see cref="SelectionChanged"/>.
        /// </summary>
        public static void RequestSelectionPublish()
        {
            IsSelectionPending = true;
            InvalidateSimulatorWindowCache();
            int generation = ++selectionPublishGeneration;
            QueueStablePublish(generation, SelectionSettleRetries);
        }

        static void InvalidateSimulatorWindowCache() =>
            cachedSimulatorWindowsUntil = 0;

        public static string BuildSelectionIdentity(in Selection selection)
        {
            if (!selection.IsValid)
                return "";

            return (selection.DeviceModel ?? "") + "|" +
                   (selection.FriendlyName ?? "") + "|" +
                   selection.ScreenIndex + "|" +
                   selection.RotationDegrees + "|" +
                   selection.HasRotation;
        }

        static void QueueStablePublish(int generation, int remainingRetries)
        {
            EditorApplication.delayCall += () =>
            {
                if (generation != selectionPublishGeneration)
                    return;

                // Do not reschedule through domain reload / compile (hang risk).
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    IsSelectionPending = false;
                    return;
                }

                string identity = "";
                if (GetActiveSelection(out Selection selection) && selection.IsValid)
                    identity = BuildSelectionIdentity(selection);

                bool changed = identity.Length > 0 && identity != lastPublishedIdentity;
                if (!changed && remainingRetries > 0)
                {
                    QueueStablePublish(generation, remainingRetries - 1);
                    return;
                }

                if (identity.Length > 0)
                    lastPublishedIdentity = identity;
                IsSelectionPending = false;
                // A device switch rebuilds ScreenSimulation: drop the old event source, bind the new.
                RefreshSelectionEventBindings();
                NotifySelectionChanged();
            };
        }

        public static void EnsureSelectionEventBindings()
        {
            if (simulatorEventBindings.Count > 0)
                return;
            RefreshSelectionEventBindings();
        }

        static void RefreshSelectionEventBindings()
        {
            UnbindSelectionEventBindings();

            EnsureResolved();
            if (simulatorWindowType == null)
                return;

            InvalidateSimulatorWindowCache();
            foreach (object window in GetSimulatorWindows())
            {
                object main = GetMain(window);
                object screenSimulation = GetScreenSimulation(main);
                if (screenSimulation != null)
                    BindSimulatorEvent(screenSimulation, "OnOrientationChanged");

                // A chrome rotation the Player orientation rules out (portrait chrome in a
                // landscape-only project) changes no screen orientation; the device view's shape
                // still changes.
                object deviceView = GetDeviceView(main);
                if (deviceView != null)
                    BindSimulatorEvent(deviceView, "OnBoundingBoxShapeChange");
            }
        }

        static void UnbindSelectionEventBindings()
        {
            for (int i = 0; i < simulatorEventBindings.Count; i++)
            {
                SimulatorEventBinding binding = simulatorEventBindings[i];
                try
                {
                    binding.Event.RemoveEventHandler(binding.Source, binding.Handler);
                }
                catch (Exception ex)
                {
                    ReportOnce("unbind-simulator-events", "Could not detach Simulator state events", ex);
                }
            }
            simulatorEventBindings.Clear();
        }

        static void BindSimulatorEvent(object source, string eventName)
        {
            EventInfo eventInfo = source.GetType().GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (eventInfo == null)
                return;

            try
            {
                // Unity ScreenSimulation events vary by version: Action, Action<Vector2>,
                // Action<ScreenOrientation>, etc. Build a matching ignore-args wrapper.
                Delegate handler = CreateNotifyHandler(eventInfo.EventHandlerType);
                eventInfo.AddEventHandler(source, handler);
                simulatorEventBindings.Add(new SimulatorEventBinding
                {
                    Source = source,
                    Event = eventInfo,
                    Handler = handler,
                });
            }
            catch (Exception ex)
            {
                ReportOnce(
                    "bind-simulator-event-" + eventName,
                    "Could not subscribe to Simulator " + eventName,
                    ex);
            }
        }

        /// <summary>
        /// Compiles a delegate of <paramref name="handlerType"/> that discards any
        /// event args and calls <see cref="NotifySelectionChanged"/>.
        /// </summary>
        static Delegate CreateNotifyHandler(Type handlerType)
        {
            if (handlerType == null)
                throw new ArgumentNullException(nameof(handlerType));

            MethodInfo invoke = handlerType.GetMethod("Invoke");
            if (invoke == null)
                throw new InvalidOperationException(handlerType.FullName + " has no Invoke");

            ParameterInfo[] parameters = invoke.GetParameters();
            ParameterExpression[] args = new ParameterExpression[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                args[i] = Expression.Parameter(parameters[i].ParameterType, "arg" + i);

            MethodInfo notify = typeof(SimulatorDeviceBridge).GetMethod(
                nameof(NotifySelectionChanged),
                BindingFlags.Public | BindingFlags.Static);
            Expression body = Expression.Call(notify);
            return Expression.Lambda(handlerType, body, args).Compile();
        }

        #endregion

        #region Editor

        /// <summary>Editor diagnostic: Foldable → Diagnose Simulator Bridge</summary>
        public static string Diagnose()
        {
            resolvedTypes = false;
            EnsureResolved();
            var sb = new System.Text.StringBuilder();
            sb.AppendLine(resolveLog);
            if (simulatorWindowType == null)
                return sb.ToString();

            var windows = Resources.FindObjectsOfTypeAll(simulatorWindowType);
            sb.AppendLine($"SimulatorWindow instances: {windows.Length}");
            foreach (var window in windows)
            {
                var main = GetMain(window);
                sb.AppendLine($"  main={(main != null ? main.GetType().Name : "null")}");
                if (main == null)
                    continue;

                int screenIndex = ReadInt(main, main.GetType(), "m_ScreenIndex", "screenIndex");
                sb.AppendLine($"  m_ScreenIndex={screenIndex}");
                var screenSim = GetScreenSimulation(main);
                sb.AppendLine($"  ScreenSimulation={(screenSim != null ? screenSim.GetType().Name : "null")}");
                if (screenSim != null)
                {
                    AppendEventSignature(sb, screenSim.GetType(), "OnOrientationChanged");
                    AppendEventSignature(sb, screenSim.GetType(), "OnResolutionChanged");
                }
                var init = main.GetType().GetMethod("InitSimulation",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                sb.AppendLine($"  InitSimulation={(init != null)}");
            }

            return sb.ToString();
        }

        static void AppendEventSignature(System.Text.StringBuilder sb, Type type, string eventName)
        {
            EventInfo eventInfo = type.GetEvent(
                eventName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (eventInfo == null)
            {
                sb.AppendLine($"  {eventName}=missing");
                return;
            }

            Type handlerType = eventInfo.EventHandlerType;
            MethodInfo invoke = handlerType?.GetMethod("Invoke");
            if (invoke == null)
            {
                sb.AppendLine($"  {eventName}={handlerType?.Name ?? "null"}");
                return;
            }

            ParameterInfo[] parameters = invoke.GetParameters();
            if (parameters.Length == 0)
            {
                sb.AppendLine($"  {eventName}=Action");
                return;
            }

            var names = new string[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
                names[i] = parameters[i].ParameterType.Name;
            sb.AppendLine($"  {eventName}=Action<{string.Join(", ", names)}>");
        }

        #endregion

        #region Simulator

        /// <summary>
        /// Force open Device Simulator windows to rebuild their device dropdown.
        /// Needed after deleting <c>.device</c> assets — import hooks mark the list dirty,
        /// but deletes alone do not.
        /// </summary>
        public static void ForceReloadDeviceLists()
        {
            if (!ResponsiveUiSimulatorList.Refresh(out string error))
                ReportOnce("reload-devices", "Could not refresh Device Simulator device list: " + error);
        }

        static Type FindTypeByName(System.Reflection.Assembly asm, string typeName)
        {
            if (asm == null || string.IsNullOrEmpty(typeName))
                return null;

            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null)
                return null;

            foreach (var type in types)
            {
                if (type != null && type.Name == typeName)
                    return type;
            }

            return null;
        }

        public static bool GetActiveSelection(out Selection selection) =>
            selectionAdapter.GetActiveSelection(out selection);

        internal static void SetSelectionAdapter(
            IDeviceSimulatorSelectionAdapter adapter)
        {
            selectionAdapter = adapter ?? new UnityDeviceSimulatorSelectionAdapter();
            InvalidateSimulatorWindowCache();
        }

        /// <summary>
        /// Prefers the focused Simulator window, then a visible window, then first valid.
        /// </summary>
        internal static bool ReadActiveSelection(out Selection selection)
        {
            selection = default;
            try
            {
                EnsureResolved();
                if (simulatorWindowType == null)
                    return false;

                EditorWindow focused = EditorWindow.focusedWindow;
                if (focused != null && simulatorWindowType.IsInstanceOfType(focused))
                {
                    object focusedMain = GetMain(focused);
                    if (focusedMain != null && ReadSelection(focusedMain, out selection))
                        return true;
                }

                Selection firstValid = default;
                bool hasFirst = false;
                foreach (object window in GetSimulatorWindows())
                {
                    object main = GetMain(window);
                    if (main == null)
                        continue;
                    if (!ReadSelection(main, out Selection current))
                        continue;

                    if (!hasFirst)
                    {
                        firstValid = current;
                        hasFirst = true;
                    }

                    if (window is EditorWindow editorWindow && editorWindow != null)
                    {
                        Rect pos = editorWindow.position;
                        if (pos.width >= 32f && pos.height >= 32f)
                        {
                            selection = current;
                            return true;
                        }
                    }
                }

                if (hasFirst)
                {
                    selection = firstValid;
                    return true;
                }
            }
            catch (Exception ex)
            {
                ReportOnce("read-selection", "Could not read the active Simulator device", ex);
            }

            return false;
        }

        /// <summary>
        /// Device Simulator windows currently in the Editor. Does not read
        /// <see cref="EditorWindow.position"/>.
        /// </summary>
        public static object[] GetSimulatorWindowObjects()
        {
            try
            {
                EnsureResolved();
                if (simulatorWindowType == null)
                    return Array.Empty<object>();
                return GetSimulatorWindows() ?? Array.Empty<object>();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        static object[] GetSimulatorWindows()
        {
            double now = EditorApplication.timeSinceStartup;
            if (cachedSimulatorWindows != null && now < cachedSimulatorWindowsUntil)
                return cachedSimulatorWindows;

            cachedSimulatorWindows = Resources.FindObjectsOfTypeAll(simulatorWindowType);
            cachedSimulatorWindowsUntil = now + 1.0d;
            return cachedSimulatorWindows ?? Array.Empty<object>();
        }

        static bool ReadSelection(object main, out Selection selection)
        {
            selection = default;
            var mainType = main.GetType();

            int deviceIndex = ReadInt(main, mainType, "deviceIndex", "m_DeviceIndex");
            int screenIndex = ReadInt(main, mainType, "screenIndex", "m_ScreenIndex");

            object device = GetMemberValue(main, mainType, "device")
                            ?? GetMemberValue(main, mainType, "currentDevice")
                            ?? GetMemberValue(main, mainType, "m_Device")
                            ?? GetMemberValue(main, mainType, "deviceInfo")
                            ?? GetMemberValue(main, mainType, "m_DeviceInfo");

            if (device == null)
                device = GetListItem(GetDeviceList(main), deviceIndex);

            if (device == null)
                return false;

            string friendly = ReadFriendlyName(device);
            string model = ReadDeviceModel(device);

            int width = 0;
            int height = 0;
            ReadScreenSize(device, screenIndex, ref width, ref height);
            bool hasRotation = ReadRotation(main, out int rotationDegrees);

            selection = new Selection
            {
                FriendlyName = friendly ?? "",
                DeviceModel = model ?? "",
                DeviceIndex = deviceIndex,
                ScreenIndex = Mathf.Max(0, screenIndex),
                ScreenWidth = width,
                ScreenHeight = height,
                RotationDegrees = rotationDegrees,
                HasRotation = hasRotation,
                IsValid = !string.IsNullOrEmpty(friendly) || !string.IsNullOrEmpty(model)
            };
            return selection.IsValid;
        }

        static bool ReadRotation(object main, out int rotationDegrees)
        {
            rotationDegrees = 0;
            object ui = GetUserInterface(main);
            if (ui == null)
                return false;

            object value = GetMemberValue(ui, ui.GetType(), "Rotation")
                           ?? GetMemberValue(ui, ui.GetType(), "m_Rotation")
                           ?? GetMemberValue(ui, ui.GetType(), "rotation");
            if (value == null)
                return false;

            try
            {
                rotationDegrees = NormalizeRotationDegrees(Convert.ToInt32(value));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void ReadScreenSize(object device, int screenIndex, ref int width, ref int height)
        {
            var screens = GetMemberValue(device, device.GetType(), "screens")
                          ?? GetMemberValue(device, device.GetType(), "Screens")
                          ?? GetMemberValue(device, device.GetType(), "m_Screens");

            // Nested DeviceInfoAsset / DeviceInfo
            var info = GetMemberValue(device, device.GetType(), "deviceInfo")
                       ?? GetMemberValue(device, device.GetType(), "m_DeviceInfo")
                       ?? GetMemberValue(device, device.GetType(), "info");
            if (screens == null && info != null)
            {
                screens = GetMemberValue(info, info.GetType(), "screens")
                          ?? GetMemberValue(info, info.GetType(), "Screens");
            }

            var screen = GetListItem(screens, screenIndex) ?? GetListItem(screens, 0);
            if (screen == null)
                return;

            int w = ReadInt(screen, screen.GetType(), "width", "Width", "m_Width");
            int h = ReadInt(screen, screen.GetType(), "height", "Height", "m_Height");
            if (w > 0 && h > 0)
            {
                width = w;
                height = h;
            }
        }

        static string ReadFriendlyName(object device)
        {
            var type = device.GetType();
            var name = GetMemberValue(device, type, "friendlyName") as string
                       ?? GetMemberValue(device, type, "FriendlyName") as string
                       ?? GetMemberValue(device, type, "name") as string
                       ?? GetMemberValue(device, type, "Name") as string;
            if (!string.IsNullOrEmpty(name))
                return name;

            var info = GetMemberValue(device, type, "deviceInfo")
                       ?? GetMemberValue(device, type, "m_DeviceInfo")
                       ?? GetMemberValue(device, type, "info");
            if (info != null && !ReferenceEquals(info, device))
                return ReadFriendlyName(info);

            return null;
        }

        static string ReadDeviceModel(object device)
        {
            var type = device.GetType();
            var model = GetMemberValue(device, type, "deviceModel") as string
                        ?? GetMemberValue(device, type, "DeviceModel") as string;
            if (!string.IsNullOrEmpty(model))
                return model;

            var systemInfo = GetMemberValue(device, type, "systemInfo")
                             ?? GetMemberValue(device, type, "SystemInfo")
                             ?? GetMemberValue(device, type, "m_SystemInfo");
            if (systemInfo != null)
            {
                model = GetMemberValue(systemInfo, systemInfo.GetType(), "deviceModel") as string
                        ?? GetMemberValue(systemInfo, systemInfo.GetType(), "DeviceModel") as string;
                if (!string.IsNullOrEmpty(model))
                    return model;
            }

            var info = GetMemberValue(device, type, "deviceInfo")
                       ?? GetMemberValue(device, type, "m_DeviceInfo")
                       ?? GetMemberValue(device, type, "info");
            if (info != null && !ReferenceEquals(info, device))
                return ReadDeviceModel(info);

            return null;
        }

        static bool SetScreenIndex(int screenIndex)
        {
            EnsureResolved();
            if (simulatorWindowType == null)
                return false;

            bool any = false;
            foreach (var window in GetSimulatorWindows())
            {
                var main = GetMain(window);
                if (main == null)
                    continue;

                int currentScreenIndex = ReadInt(
                    main, main.GetType(), "screenIndex", "m_ScreenIndex");
                if (currentScreenIndex == screenIndex)
                {
                    any = true;
                    continue;
                }

                try
                {
                    if (ChangeScreen(main, screenIndex))
                        any = true;
                }
                catch (Exception ex)
                {
                    ReportOnce("set-screen", "Could not set Simulator screen", ex);
                }
            }

            return any;
        }

        /// <summary>
        /// Force Device Simulator chrome rotation (0 = portrait, ±90 = landscape).
        /// </summary>
        static bool SetRotationDegrees(int degrees)
        {
            EnsureResolved();
            if (simulatorWindowType == null)
                return false;

            degrees = NormalizeRotationDegrees(degrees);

            bool any = false;
            foreach (var window in GetSimulatorWindows())
            {
                object main = GetMain(window);
                var ui = GetUserInterface(main);
                if (ui == null)
                    continue;

                if (EnsureIntMember(ui, "Rotation", degrees) || EnsureIntMember(ui, "m_Rotation", degrees))
                    any = true;

                // The toolbar rotation and the device picture are separate. Setting only the
                // toolbar value leaves the preview portrait, so the camera and UI stay portrait.
                object view = GetDeviceView(main);
                if (view != null && EnsureIntMember(view, "Rotation", degrees))
                    any = true;
            }

            return any;
        }

        static int NormalizeRotationDegrees(int degrees)
        {
            degrees = ((degrees % 360) + 360) % 360;
            return degrees == 270 ? -90 : degrees;
        }

        /// <summary>
        /// Fixed Default Orientation switched to Auto Rotation on the simulator shim by
        /// <see cref="SyncRenderedOrientationToChrome"/>; restored once chrome is back on a
        /// project axis.
        /// </summary>
        static bool renderOrientationOverridden;

        /// <summary>
        /// Match the simulator render to the device chrome. Chrome on an axis the project does
        /// not allow (portrait chrome in a landscape-only project, or landscape chrome in a
        /// portrait-only one) otherwise keeps the other framebuffer and rotates it inside the
        /// frame, so the camera looks sideways. This writes the simulator shim only — not Player
        /// Settings: chrome off the project axis allows only the chrome axis (see
        /// <see cref="ResolveRenderAxes"/>), chrome on a project axis keeps the project flags, and
        /// a fixed Default Orientation becomes Auto Rotation while the chrome needs it. Nothing is written (and ApplyChanges is skipped) when no value changes.
        /// Call after <see cref="SetRotationDegrees"/> so device orientation is current.
        /// </summary>
        static void SyncRenderedOrientationToChrome(bool chromeLandscape)
        {
            EnsureResolved();
            if (simulatorWindowType == null)
                return;

            FoldablePlayerOrientation.GetAllowedAxes(out bool projectPortrait, out bool projectLandscape);
            ResolveRenderAxes(
                chromeLandscape, projectPortrait, projectLandscape,
                out bool portrait, out bool landscape);
            bool forcePortrait = portrait && !projectPortrait;
            bool forceLandscape = landscape && !projectLandscape;
            bool overrideAxis = forcePortrait || forceLandscape;

            // The chrome axis itself must render upright: portrait chrome needs the Portrait flag
            // even when the project only allows Portrait Upside Down. An axis the render must not
            // take stays off.
            bool portraitFlag = portrait &&
                                (PlayerSettings.allowedAutorotateToPortrait || !chromeLandscape);
            bool landscapeLeftFlag = landscape &&
                                     (PlayerSettings.allowedAutorotateToLandscapeLeft || chromeLandscape);
            bool landscapeRightFlag = landscape &&
                                      (PlayerSettings.allowedAutorotateToLandscapeRight || chromeLandscape);

            // Autorotate flags do nothing under a fixed Default Orientation.
            var defaultOrientation = FoldablePlayerOrientation.GetDefaultOrientation();
            bool fixedDefault = defaultOrientation != UIOrientation.AutoRotation;
            ScreenOrientation? orientationTarget = null;
            if (fixedDefault && overrideAxis)
                orientationTarget = ScreenOrientation.AutoRotation;
            else if (fixedDefault && renderOrientationOverridden)
                orientationTarget = FoldablePlayerOrientation.ToScreenOrientation(defaultOrientation);

            foreach (var window in GetSimulatorWindows())
            {
                var screenSim = GetScreenSimulation(GetMain(window));
                if (screenSim == null)
                    continue;

                var simType = screenSim.GetType();
                try
                {
                    bool changed = SetShimBool(screenSim, simType, "autorotateToPortrait", portraitFlag);
                    changed |= SetShimBool(screenSim, simType, "autorotateToLandscapeLeft", landscapeLeftFlag);
                    changed |= SetShimBool(screenSim, simType, "autorotateToLandscapeRight", landscapeRightFlag);
                    if (orientationTarget.HasValue)
                        changed |= SetShimOrientation(screenSim, simType, orientationTarget.Value);

                    // Landscape-only already has the landscape flags, so a half-open tabletop
                    // must still push the device angle or Screen, the camera, and the UI stay
                    // portrait. Portrait-only pushes 0, the opposite pose.
                    int targetRotation = chromeLandscape ? 90 : 0;
                    changed |= SetIntMember(screenSim, "DeviceRotation", targetRotation);
                    bool renderMatches = RenderedOrientationMatches(screenSim, simType, chromeLandscape);
                    if (!changed && renderMatches)
                        continue;

                    var apply = simType.GetMethod(
                        "ApplyChanges",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);
                    apply?.Invoke(screenSim, null);
                }
                catch (Exception ex)
                {
                    ReportOnce(
                        "sync-render-orientation",
                        "Could not match Simulator render orientation to device chrome",
                        ex);
                }
            }

            renderOrientationOverridden = fixedDefault && overrideAxis;
        }

        /// <summary>
        /// Simulator render axes for a device chrome. The chrome axis is always allowed so the
        /// framebuffer matches the frame (portrait chrome in a landscape-only project, or
        /// landscape chrome in a portrait-only one, would otherwise render sideways). Chrome off
        /// the project axis allows only the chrome axis: with both allowed, a chrome turn
        /// auto-rotates the render before the session (e.g. the Fold half-open hold) turns the
        /// chrome back. Chrome on a project axis keeps the project axes.
        /// </summary>
        internal static void ResolveRenderAxes(
            bool chromeLandscape,
            bool projectPortrait,
            bool projectLandscape,
            out bool portrait,
            out bool landscape)
        {
            bool chromeAxisAllowed = chromeLandscape ? projectLandscape : projectPortrait;
            if (!chromeAxisAllowed)
            {
                portrait = !chromeLandscape;
                landscape = chromeLandscape;
                return;
            }

            portrait = projectPortrait;
            landscape = projectLandscape;
        }

        static bool RenderedOrientationMatches(object screenSim, Type simType, bool chromeLandscape)
        {
            var prop = simType.GetProperty(
                "orientation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanRead || prop.PropertyType != typeof(ScreenOrientation))
                return true;

            var orientation = (ScreenOrientation)prop.GetValue(screenSim);
            bool renderedLandscape =
                orientation == ScreenOrientation.LandscapeLeft ||
                orientation == ScreenOrientation.LandscapeRight;
            return renderedLandscape == chromeLandscape;
        }

        /// <summary>True when <paramref name="name"/> exists and now equals <paramref name="value"/>.</summary>
        static bool EnsureIntMember(object target, string name, int value)
        {
            if (target == null || !ReadIntMember(target, name, out int current))
                return false;
            if (NormalizeRotationDegrees(current) == NormalizeRotationDegrees(value))
                return true;
            return SetIntMember(target, name, value);
        }

        static bool ReadIntMember(object target, string name, out int value)
        {
            value = 0;
            var type = target.GetType();
            var prop = type.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanRead && prop.PropertyType == typeof(int))
            {
                value = Convert.ToInt32(prop.GetValue(target));
                return true;
            }

            var field = type.GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType != typeof(int))
                return false;
            value = Convert.ToInt32(field.GetValue(target));
            return true;
        }

        /// <summary>Writes an int property or field when it differs. True when written.</summary>
        static bool SetIntMember(object target, string name, int value)
        {
            if (target == null)
                return false;

            var type = target.GetType();
            try
            {
                var prop = type.GetProperty(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null && prop.CanRead && prop.CanWrite && prop.PropertyType == typeof(int))
                {
                    int current = NormalizeRotationDegrees(Convert.ToInt32(prop.GetValue(target)));
                    int next = NormalizeRotationDegrees(value);
                    if (current == next)
                        return false;
                    prop.SetValue(target, value);
                    return true;
                }

                var field = type.GetField(
                    name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null || field.FieldType != typeof(int))
                    return false;

                int currentField = NormalizeRotationDegrees(Convert.ToInt32(field.GetValue(target)));
                if (currentField == NormalizeRotationDegrees(value))
                    return false;
                field.SetValue(target, value);
                return true;
            }
            catch (Exception ex)
            {
                ReportOnce("set-int-" + name, "Could not set Simulator " + name, ex);
                return false;
            }
        }

        /// <summary>Writes a simulator shim bool when it differs. True when written.</summary>
        static bool SetShimBool(object screenSim, Type simType, string name, bool value)
        {
            var prop = simType.GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanRead || !prop.CanWrite || prop.PropertyType != typeof(bool))
                return false;
            if (Convert.ToBoolean(prop.GetValue(screenSim)) == value)
                return false;

            prop.SetValue(screenSim, value);
            return true;
        }

        /// <summary>Writes the simulator shim <c>orientation</c> when it differs. True when written.</summary>
        static bool SetShimOrientation(object screenSim, Type simType, ScreenOrientation value)
        {
            var prop = simType.GetProperty(
                "orientation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanRead || !prop.CanWrite || prop.PropertyType != typeof(ScreenOrientation))
                return false;
            if ((ScreenOrientation)prop.GetValue(screenSim) == value)
                return false;

            prop.SetValue(screenSim, value);
            return true;
        }

        /// <summary>
        /// Switch Simulator Cover/Inner (or Single/Spanned) and rotation for the
        /// current product state. Chrome follows the session pose.
        /// </summary>
        public static void SyncScreenForFoldablePosture(bool preferInner)
        {
            var device = FoldableEditorSession.FindDeviceByFriendlyName(FoldableEditorSession.BoundFriendlyName);
            int screenIndex = FoldableEditorSession.PreferredScreenIndex(device, preferInner);
            SetScreenIndex(screenIndex);

            bool chromeLandscape = FoldableEditorSession.IsLandscape;
            SetRotationDegrees(chromeLandscape ? 90 : 0);
            SyncRenderedOrientationToChrome(chromeLandscape);
        }

        /// <summary>
        /// Full sync from session product state: screen index + rotation.
        /// Returns catalog metrics for the active screen (swapped when landscape).
        /// </summary>
        public static bool SyncFromSession(out int metricsWidth, out int metricsHeight, out string screenName)
        {
            metricsWidth = FoldableEditorSession.MetricsWidth;
            metricsHeight = FoldableEditorSession.MetricsHeight;
            screenName = FoldableEditorSession.BoundScreenName;

            if (!FoldableEditorSession.ResolveScreenForProduct(
                    FoldableEditorSession.BoundFriendlyName,
                    FoldableEditorSession.ProductState,
                    out var screen,
                    out int screenIndex))
            {
                return false;
            }

            bool switched = SetScreenIndex(screenIndex);

            bool chromeLandscape = FoldableEditorSession.IsLandscape;

            bool rotated = SetRotationDegrees(chromeLandscape ? 90 : 0);
            SyncRenderedOrientationToChrome(chromeLandscape);

            screen.GetOrientedSize(chromeLandscape, out metricsWidth, out metricsHeight);
            screenName = screen.Name ?? screenName;

            if (!switched)
            {
                ReportOnce(
                    "sync-screen",
                    $"Could not switch Simulator to screen {screenIndex} ({screenName} {metricsWidth}×{metricsHeight})");
                // Still return true so session metrics update; chrome may stay stale.
            }
            else if (!rotated && chromeLandscape)
            {
                ReportOnce("sync-rotation", "Could not rotate Simulator to landscape");
            }

            return true;
        }

        public static bool SelectToolkitDevice(string friendlyName, int preferredScreenIndex)
            => SelectDeviceInternal(friendlyName, preferredScreenIndex, allowNormalizedName: true);

        public static bool SelectDevice(string friendlyName, int preferredScreenIndex)
            => SelectDeviceInternal(friendlyName, preferredScreenIndex, allowNormalizedName: false);

        static bool SelectDeviceInternal(
            string friendlyName,
            int preferredScreenIndex,
            bool allowNormalizedName)
        {
            EnsureResolved();
            if (simulatorWindowType == null || string.IsNullOrEmpty(friendlyName))
                return false;

            bool any = false;
            foreach (var window in GetSimulatorWindows())
            {
                var main = GetMain(window);
                if (main == null)
                    continue;

                if (ReadSelection(main, out var current) &&
                    current.IsValid &&
                    (string.Equals(
                         current.FriendlyName,
                         friendlyName,
                         StringComparison.OrdinalIgnoreCase) ||
                     (allowNormalizedName &&
                      FoldableDeviceCatalog.NamesMatch(
                          current.FriendlyName,
                          friendlyName))))
                {
                    try
                    {
                        if (current.ScreenIndex == preferredScreenIndex ||
                            ChangeScreen(main, preferredScreenIndex))
                        {
                            any = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        ReportOnce("select-device-screen", "Could not select Simulator screen", ex);
                    }
                    continue;
                }

                bool selected;
                try
                {
                    selected = SelectOnMain(
                        main,
                        friendlyName,
                        preferredScreenIndex,
                        allowNormalizedName);
                }
                catch (Exception ex)
                {
                    ReportOnce("select-device", "Could not select Simulator device", ex);
                    selected = false;
                }
                if (selected)
                    any = true;
            }

            return any;
        }

        public static bool GetDeviceNamesInDisplayOrder(List<string> names)
        {
            names.Clear();
            EnsureResolved();
            if (simulatorWindowType == null)
                return false;

            foreach (var window in GetSimulatorWindows())
            {
                // Arrays are IList too.
                if (GetDeviceList(GetMain(window)) is IList devices)
                {
                    for (int i = 0; i < devices.Count; i++)
                        AddDeviceName(names, devices[i]);
                }

                if (names.Count > 0)
                    return true;
            }

            return false;
        }

        static bool SelectOnMain(
            object main,
            string friendlyName,
            int preferredScreenIndex,
            bool allowNormalizedName)
        {
            var mainType = main.GetType();

            int deviceIndex = FindDeviceIndex(main, friendlyName, allowNormalizedName);
            if (deviceIndex >= 0 && SetDeviceIndex(main, deviceIndex))
            {
                ChangeScreen(main, preferredScreenIndex);
                return true;
            }

            var loadByName = mainType.GetMethod("LoadDevice",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            if (loadByName != null)
            {
                try
                {
                    loadByName.Invoke(main, new object[] { friendlyName });
                    ChangeScreen(main, preferredScreenIndex);
                    return true;
                }
                catch (Exception ex)
                {
                    ReportOnce("load-device", "LoadDevice(string) failed; trying indexed selection", ex);
                }
            }

            return false;
        }

        static int FindDeviceIndex(
            object main,
            string friendlyName,
            bool allowNormalizedName)
        {
            // Arrays are IList too.
            if (GetDeviceList(main) is IList devices)
            {
                for (int i = 0; i < devices.Count; i++)
                {
                    if (DeviceMatches(
                            devices[i],
                            friendlyName,
                            allowNormalizedName))
                        return i;
                }
            }

            return -1;
        }

        static object GetDeviceList(object main)
        {
            if (main == null)
                return null;

            Type mainType = main.GetType();
            return GetMemberValue(main, mainType, "devices")
                   ?? GetMemberValue(main, mainType, "m_Devices")
                   ?? GetMemberValue(main, mainType, "deviceList")
                   ?? GetMemberValue(main, mainType, "m_DeviceList");
        }

        static void AddDeviceName(List<string> names, object device)
        {
            string friendlyName = ReadFriendlyName(device);
            if (string.IsNullOrEmpty(friendlyName))
                return;

            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(
                        names[i],
                        friendlyName,
                        StringComparison.OrdinalIgnoreCase))
                    return;
            }

            names.Add(friendlyName);
        }

        static bool DeviceMatches(
            object device,
            string friendlyName,
            bool allowNormalizedName)
        {
            if (device == null)
                return false;

            var name = ReadFriendlyName(device);
            return string.Equals(
                       name,
                       friendlyName,
                       StringComparison.OrdinalIgnoreCase) ||
                   (allowNormalizedName &&
                    FoldableDeviceCatalog.NamesMatch(name, friendlyName));
        }

        static bool SetDeviceIndex(object main, int deviceIndex)
        {
            var mainType = main.GetType();

            var changeDevice = mainType.GetMethod("ChangeDevice",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);
            if (changeDevice != null)
            {
                changeDevice.Invoke(main, new object[] { deviceIndex });
                return true;
            }

            var setDevice = mainType.GetMethod("SetDevice",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(int) }, null);
            if (setDevice != null)
            {
                setDevice.Invoke(main, new object[] { deviceIndex });
                return true;
            }

            var prop = mainType.GetProperty("deviceIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(main, deviceIndex);
                return true;
            }

            var field = mainType.GetField("m_DeviceIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                field.SetValue(main, deviceIndex);
                return true;
            }

            return false;
        }

        static bool ChangeScreen(object main, int screenIndex)
        {
            if (main == null)
                return false;

            if (ChangeScreenOnDeviceSimulatorMain(main, screenIndex))
                return true;

            // Fallback nested targets
            var nested = GetMemberValue(main, main.GetType(), "m_DeviceSimulatorMain")
                         ?? GetMemberValue(main, main.GetType(), "deviceSimulatorMain");
            return nested != null && ChangeScreenOnDeviceSimulatorMain(nested, screenIndex);
        }

        /// <summary>
        /// Matches Unity DeviceSimulatorMain screen switching:
        /// set m_ScreenIndex, then ScreenSimulation.ChangeScreen + InitScreenUI,
        /// or InitSimulation() rebuild.
        /// </summary>
        static bool ChangeScreenOnDeviceSimulatorMain(object main, int screenIndex)
        {
            var mainType = main.GetType();
            screenIndex = Mathf.Max(0, screenIndex);

            // Clamp to device screen count when available.
            var currentDevice = GetMemberValue(main, mainType, "currentDevice");
            if (currentDevice != null)
            {
                var info = GetMemberValue(currentDevice, currentDevice.GetType(), "deviceInfo")
                           ?? GetMemberValue(currentDevice, currentDevice.GetType(), "m_DeviceInfo");
                var screens = info != null
                    ? (GetMemberValue(info, info.GetType(), "screens")
                       ?? GetMemberValue(info, info.GetType(), "Screens"))
                    : null;
                int count = screens is IList list ? list.Count : -1;
                if (count > 0)
                    screenIndex = Mathf.Clamp(screenIndex, 0, count - 1);
            }

            // 1) Write private m_ScreenIndex (no public setter in UnityCsReference).
            var indexField = mainType.GetField("m_ScreenIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (indexField != null)
                indexField.SetValue(main, screenIndex);

            // 2) Preferred path: ScreenSimulation.ChangeScreen + InitScreenUI (HandleScreenChange).
            var screenSim = GetScreenSimulation(main);
            if (screenSim != null)
            {
                var change = screenSim.GetType().GetMethod("ChangeScreen",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(int) }, null);
                if (change != null)
                {
                    try
                    {
                        change.Invoke(screenSim, new object[] { screenIndex });
                        var initUi = mainType.GetMethod("InitScreenUI",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        initUi?.Invoke(main, null);

                        // Keep UI toggle label in sync when possible.
                        var ui = GetUserInterface(main);
                        if (ui != null)
                        {
                            var setName = ui.GetType().GetMethod("SetScreenToggleName",
                                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                                null, new[] { typeof(string) }, null);
                            setName?.Invoke(ui, new object[] { $"Screen {screenIndex + 1}" });
                        }

                        return true;
                    }
                    catch (Exception ex)
                    {
                        ReportOnce("change-screen", "ScreenSimulation.ChangeScreen failed", ex);
                    }
                }
            }

            // 3) Rebuild simulation from the new m_ScreenIndex.
            var init = mainType.GetMethod("InitSimulation",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (init != null)
            {
                try
                {
                    init.Invoke(main, null);
                    return true;
                }
                catch (Exception ex)
                {
                    ReportOnce("init-simulation", "InitSimulation failed", ex);
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        static Type simulatorWindowType;
        static PropertyInfo mainProperty;
        static FieldInfo mainField;
        static bool resolvedTypes;
        static string resolveLog = "";
        static readonly HashSet<string> reportedDiagnostics = new HashSet<string>();

        static void EnsureResolved()
        {
            if (resolvedTypes)
                return;
            resolvedTypes = true;

            var asm = typeof(DeviceSimulatorPlugin).Assembly;
            simulatorWindowType = asm.GetType("UnityEditor.DeviceSimulation.SimulatorWindow")
                                  ?? FindTypeByName(asm, "SimulatorWindow");

            if (simulatorWindowType == null)
            {
                resolveLog = "SimulatorWindow type not found";
                ReportOnce("resolve-window", resolveLog);
                return;
            }

            // UnityCsReference: public DeviceSimulatorMain main => m_Main;
            mainProperty = simulatorWindowType.GetProperty("main",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            mainField = simulatorWindowType.GetField("m_Main",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (mainProperty == null && mainField == null)
            {
                resolveLog = "SimulatorWindow.main / m_Main not found";
                ReportOnce("resolve-main", resolveLog);
            }
            else
                resolveLog = $"OK window={simulatorWindowType.Name} mainProp={mainProperty != null} mainField={mainField != null}";
        }

        static object GetMain(object window)
        {
            if (window == null)
                return null;
            try
            {
                if (mainProperty != null)
                    return mainProperty.GetValue(window);
                if (mainField != null)
                    return mainField.GetValue(window);
                return GetMemberValue(window, window.GetType(), "main")
                       ?? GetMemberValue(window, window.GetType(), "m_Main");
            }
            catch (Exception ex)
            {
                ReportOnce("get-main", "Could not read Device Simulator state", ex);
                return null;
            }
        }

        static void ReportOnce(string key, string message, Exception exception = null)
        {
            if (!reportedDiagnostics.Add(key))
                return;
            Debug.LogWarning(
                "[Fold & Flip] Simulator bridge disabled for this operation: " + message +
                (exception == null ? "" : " (" + exception.GetType().Name + ": " + exception.Message + ")") +
                "\nUse Responsive UI → Debug → Diagnose Simulator Bridge for details.");
        }

        static object GetListItem(object list, int index)
        {
            if (list == null || index < 0)
                return null;

            // Arrays are IList too.
            if (list is IList iList && index < iList.Count)
                return iList[index];

            return null;
        }

        static object GetScreenSimulation(object main) =>
            main == null
                ? null
                : GetMemberValue(main, main.GetType(), "ScreenSimulation")
                  ?? GetMemberValue(main, main.GetType(), "m_ScreenSimulation");

        static object GetDeviceView(object main)
        {
            object ui = GetUserInterface(main);
            return ui == null
                ? null
                : GetMemberValue(ui, ui.GetType(), "DeviceView")
                  ?? GetMemberValue(ui, ui.GetType(), "m_DeviceView");
        }

        static object GetUserInterface(object main) =>
            main == null
                ? null
                : GetMemberValue(main, main.GetType(), "userInterface")
                  ?? GetMemberValue(main, main.GetType(), "m_UserInterface")
                  ?? GetMemberValue(main, main.GetType(), "UserInterface");

        static int ReadInt(object target, Type type, params string[] names)
        {
            foreach (var name in names)
            {
                var value = GetMemberValue(target, type, name);
                if (value == null)
                    continue;
                try
                {
                    return Convert.ToInt32(value);
                }
                catch
                {
                    // continue
                }
            }

            return -1;
        }

        static object GetMemberValue(object target, Type type, string name)
        {
            try
            {
                var prop = type.GetProperty(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop != null)
                    return prop.GetValue(target);

                var field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return field?.GetValue(target);
            }
            catch (Exception ex)
            {
                ReportOnce("member-" + type.FullName + "." + name, "Could not read " + type.Name + "." + name, ex);
                return null;
            }
        }

        #endregion
    }
}
