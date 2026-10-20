using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Play Mode form-switch cover on the Canvas
    /// <see cref="ResponsiveUI"/> hub. Instant <see cref="blackOverlay"/> masks the
    /// mid-Apply reflow; optional loading <see cref="ResponsiveUiController"/> draws on
    /// top. Both use <see cref="CanvasGroup"/> (<c>ignoreParentGroups</c>) so parent fades
    /// cannot punch through. Cover stays up through NotifyClients, then hides after
    /// end-of-frame + min visible. Only form changes after the first Apply into a checked
    /// <see cref="showOnSlots"/> destination show it (defaults: Flip Cover off).
    /// </summary>
    [Serializable]
    public sealed class ResponsiveFormTransitionLoading
    {
        #region Serialized

        [Tooltip("When on, Play Mode form switches can show the black overlay / loading controller.")]
        [SerializeField] bool enabled;

        [Tooltip(
            "Full-screen Image hard cover. Play Mode drives a CanvasGroup alpha 1 for the whole " +
            "form-switch Apply (under the loading controller when both are set), then back to 0. " +
            "Keep Image color alpha at the desired cover opacity (usually 1). Stretch-full " +
            "RectTransform. Edit Mode leaves authored alpha alone.")]
        [SerializeField] Image blackOverlay;

        [Tooltip(
            "Optional loading panel controller (cover + text widgets per slot). Keep its root as a " +
            "late sibling under the Canvas (draws on top of the black overlay) and outside other " +
            "Active Objects. Play Mode: stays active while Enabled so Apply keeps it laid out; " +
            "CanvasGroup toggles visibility. Edit Mode: activate the GameObject to author — alpha " +
            "is not forced off. First Apply does not show it.")]
        [SerializeField] ResponsiveUiController controller;

        [Tooltip("Minimum time the cover stays visible after Apply finishes (realtime).")]
        [SerializeField][Min(0f)] float minVisibleSeconds = 0.25f;

        [Tooltip(
            "Show the cover when a form switch lands on a checked slot (same form × orientation " +
            "grid as Active Objects). Unchecked = no cover for that destination. Default: all on " +
            "except Flip Cover.")]
        [SerializeField] ResponsiveActiveObjectSlots showOnSlots =
            ResponsiveActiveObjectSlots.FormTransitionDefaults();

        #endregion

        #region Runtime

        // Shared only when two hubs point at the same controller — last Begin owns hide.
        static ResponsiveUiController s_shownController;
        static int s_showGeneration;

        // Enter Play Mode without domain reload keeps statics; drop the previous
        // session's (destroyed) controller reference.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_shownController = null;
            s_showGeneration = 0;
        }

        bool _visible;
        int _holdCount;
        int _showGeneration;
        float _shownAt;
        Coroutine _hideRoutine;
        MonoBehaviour _host;

        CanvasGroup _blackCanvasGroup;
        int _blackOverlayId;
        CanvasGroup _controllerCanvasGroup;
        int _controllerId;

        #endregion

        #region Access

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                if (value)
                    EnsureReady();
                else
                    HideNow();
            }
        }

        public Image BlackOverlay
        {
            get => blackOverlay;
            set
            {
                if (blackOverlay == value)
                    return;
                blackOverlay = value;
                InvalidateBlackCache();
            }
        }

        public ResponsiveUiController Controller
        {
            get => controller;
            set
            {
                if (controller == value)
                    return;
                controller = value;
                InvalidateControllerCache();
            }
        }

        public float MinVisibleSeconds
        {
            get => minVisibleSeconds;
            set => minVisibleSeconds = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Destination slots that may show the cover. Defaults to
        /// <see cref="ResponsiveActiveObjectSlots.FormTransitionDefaults"/>.
        /// </summary>
        public ResponsiveActiveObjectSlots ShowOnSlots
        {
            get => showOnSlots;
            set => showOnSlots = value;
        }

        public bool IsVisible => _visible;

        #endregion

        #region Gates

        /// <summary>True when Apply form factor changed.</summary>
        public static bool IsFormChange(
            ResponsiveDevice.FormFactor previous,
            ResponsiveDevice.FormFactor next) =>
            previous != next;

        /// <summary>
        /// Form changed and the destination slot allows the cover
        /// (<paramref name="showOnNext"/> from <see cref="ShowOnSlots"/>).
        /// </summary>
        public static bool ShouldCoverFormChange(
            ResponsiveDevice.FormFactor previous,
            ResponsiveDevice.FormFactor next,
            bool showOnNext) =>
            IsFormChange(previous, next) && showOnNext;

        /// <summary>
        /// Pure gate: feature on, Play Mode, black overlay and/or controller assigned,
        /// after first Apply, form changed into a checked destination slot.
        /// </summary>
        public static bool ShouldShow(
            bool featureEnabled,
            bool isPlaying,
            bool hasBlackOverlay,
            bool hasController,
            bool hasAppliedBefore,
            ResponsiveDevice.FormFactor previous,
            ResponsiveDevice.FormFactor next,
            bool showOnNext) =>
            featureEnabled &&
            isPlaying &&
            (hasBlackOverlay || hasController) &&
            hasAppliedBefore &&
            ShouldCoverFormChange(previous, next, showOnNext);

        #endregion

        #region Apply hooks

        /// <summary>
        /// Show when this Apply is a form transition into a checked slot. Call under hub
        /// <c>ApplyInFlight</c> before NotifyClients. Returns true when this Apply holds
        /// the cover.
        /// </summary>
        public bool Begin(
            MonoBehaviour host,
            in ResponsiveDevice.LayoutKey previous,
            in ResponsiveDevice.LayoutKey next,
            bool hasAppliedBefore)
        {
            _host = host;
            bool showOnNext = showOnSlots.IsActive(next);
            if (!ShouldShow(
                    enabled,
                    Application.isPlaying,
                    blackOverlay != null,
                    controller != null,
                    hasAppliedBefore,
                    previous.Form,
                    next.Form,
                    showOnNext))
            {
                // Destination slot off (e.g. Flip Closed): drop any leftover in-flight cover.
                if (!showOnNext && IsFormChange(previous.Form, next.Form))
                    DismissInFlightCoverIfAny();
                return false;
            }

            CancelHide();
            // Mark visible before Prepare so force-hide branches do not write alpha 0 then 1.
            _visible = true;
            _shownAt = Time.realtimeSinceStartup;
            // Hard cover first, loading chrome on top — mid-Apply reflow never flashes.
            SetBlackOverlayVisible(true);
            SetControllerVisible(true, claimOwnership: true);
            _holdCount++;
            return true;
        }

        /// <summary>Re-assert cover after Active Objects may have toggled hierarchy.</summary>
        public void KeepVisible()
        {
            if (!enabled || !_visible)
                return;
            SetBlackOverlayVisible(true);
            SetControllerVisible(true, claimOwnership: false);
        }

        /// <summary>Release one Apply hold; hide after end-of-frame + min visible time.</summary>
        public void End()
        {
            if (_holdCount <= 0)
                return;

            _holdCount--;
            if (_holdCount > 0)
                return;

            CancelHide();
            if (!enabled || !_visible || _host == null || !_host.isActiveAndEnabled)
            {
                HideNow();
                return;
            }

            _hideRoutine = _host.StartCoroutine(HideAfterVisibleRoutine());
        }

        /// <summary>Cancel deferred hide and hide the cover immediately.</summary>
        public void HideNow()
        {
            CancelHide();
            bool ownsController = OwnsShownController();
            if (!ownsController)
            {
                // Shared controller claimed by another hub — still drop our black overlay.
                if (Application.isPlaying)
                    SetBlackOverlayVisible(false);
                _visible = false;
                _holdCount = 0;
                return;
            }

            // Play Mode only drives cover alpha. Edit Mode leaves authored visibility alone.
            if (Application.isPlaying)
            {
                SetControllerVisible(false, claimOwnership: false);
                SetBlackOverlayVisible(false);
            }

            if (s_shownController == controller)
                s_shownController = null;
            _visible = false;
            _holdCount = 0;

            // Feature off: release the controller so it stops receiving hub Applies. Play Mode
            // only — the ExecuteAlways hub hides on disable in Edit Mode too, and must not
            // deactivate the authored controller there.
            if (Application.isPlaying &&
                !enabled &&
                controller != null &&
                controller.gameObject.activeSelf)
                controller.gameObject.SetActive(false);
        }

        /// <summary>
        /// Play Mode: warm CanvasGroups and keep covers alpha-hidden so the first form
        /// switch does not AddComponent / activate mid-Apply. Edit Mode leaves hierarchy /
        /// alpha alone so authors can toggle GameObjects and see scale.
        /// </summary>
        public void EnsureReady()
        {
            if (!enabled || !Application.isPlaying || _visible)
                return;

            // SetVisible prepares once — avoid Prepare + SetVisible double work.
            SetBlackOverlayVisible(false);
            SetControllerVisible(false, claimOwnership: false);
        }

        #endregion

        #region Internals

        bool OwnsShownController() =>
            controller == null ||
            s_shownController == null ||
            s_shownController != controller ||
            _showGeneration == s_showGeneration;

        void DismissInFlightCoverIfAny()
        {
            if (!Application.isPlaying)
                return;
            if (!_visible && _holdCount <= 0 && _hideRoutine == null)
                return;
            HideNow();
        }

        void InvalidateBlackCache()
        {
            _blackCanvasGroup = null;
            _blackOverlayId = 0;
        }

        void InvalidateControllerCache()
        {
            _controllerCanvasGroup = null;
            _controllerId = 0;
        }

        void PrepareBlackOverlay()
        {
            if (blackOverlay == null)
            {
                InvalidateBlackCache();
                return;
            }

            int id = blackOverlay.GetInstanceID();
            if (_blackCanvasGroup == null || _blackOverlayId != id)
            {
                _blackOverlayId = id;
                _blackCanvasGroup = blackOverlay.GetComponent<CanvasGroup>();
                if (_blackCanvasGroup == null)
                    _blackCanvasGroup = blackOverlay.gameObject.AddComponent<CanvasGroup>();
            }

            if (!blackOverlay.gameObject.activeSelf)
                blackOverlay.gameObject.SetActive(true);

            if (!_blackCanvasGroup.ignoreParentGroups)
                _blackCanvasGroup.ignoreParentGroups = true;
        }

        void PrepareController()
        {
            if (controller == null)
            {
                InvalidateControllerCache();
                return;
            }

            int id = controller.GetInstanceID();
            if (_controllerCanvasGroup == null || _controllerId != id)
            {
                _controllerId = id;
                _controllerCanvasGroup = controller.GetComponent<CanvasGroup>();
                if (_controllerCanvasGroup == null)
                    _controllerCanvasGroup = controller.gameObject.AddComponent<CanvasGroup>();
            }

            if (!controller.gameObject.activeSelf)
                controller.gameObject.SetActive(true);
            if (!controller.enabled)
                controller.enabled = true;

            // Survive parent CanvasGroup fades. Only force-hide in Play Mode — Edit Mode
            // must keep authored alpha so the Scene view shows the panel when active.
            if (!_controllerCanvasGroup.ignoreParentGroups)
                _controllerCanvasGroup.ignoreParentGroups = true;
            if (Application.isPlaying && !_visible)
                ApplyCanvasGroupHidden(_controllerCanvasGroup);
        }

        void SetBlackOverlayVisible(bool visible)
        {
            if (blackOverlay == null)
                return;

            PrepareBlackOverlay();
            if (visible)
                BringToFront(blackOverlay.transform);
            ApplyCanvasGroup(_blackCanvasGroup, visible);
        }

        void SetControllerVisible(bool visible, bool claimOwnership)
        {
            if (controller == null)
                return;

            PrepareController();
            if (visible)
                BringToFront(controller.transform);

            if (visible && claimOwnership)
            {
                s_shownController = controller;
                _showGeneration = ++s_showGeneration;
            }

            ApplyCanvasGroup(_controllerCanvasGroup, visible);
        }

        static void BringToFront(Transform t)
        {
            Transform parent = t.parent;
            if (parent == null)
                return;
            int last = parent.childCount - 1;
            if (t.GetSiblingIndex() != last)
                t.SetAsLastSibling();
        }

        static void ApplyCanvasGroup(CanvasGroup group, bool visible)
        {
            if (group == null)
                return;

            float a = visible ? 1f : 0f;
            if (group.alpha != a)
                group.alpha = a;
            if (group.blocksRaycasts != visible)
                group.blocksRaycasts = visible;
            if (group.interactable != visible)
                group.interactable = visible;
        }

        static void ApplyCanvasGroupHidden(CanvasGroup group)
        {
            if (group == null)
                return;
            if (group.alpha != 0f)
                group.alpha = 0f;
            if (group.blocksRaycasts)
                group.blocksRaycasts = false;
            if (group.interactable)
                group.interactable = false;
        }

        void CancelHide()
        {
            if (_hideRoutine == null || _host == null)
                return;
            _host.StopCoroutine(_hideRoutine);
            _hideRoutine = null;
        }

        IEnumerator HideAfterVisibleRoutine()
        {
            // One frame so the applied layout paints under the cover.
            yield return null;

            float min = minVisibleSeconds;
            if (min > 0f)
            {
                // Allocation-free wait (no per-hide WaitHandle / yield instruction alloc).
                float deadline = _shownAt + min;
                while (Time.realtimeSinceStartup < deadline)
                    yield return null;
            }

            _hideRoutine = null;
            HideNow();
        }

        #endregion
    }
}
