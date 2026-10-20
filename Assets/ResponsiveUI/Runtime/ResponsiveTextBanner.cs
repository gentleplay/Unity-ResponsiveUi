using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// A reusable single-line TMP banner whose width follows its rendered text.
    /// Height comes from the active ResponsiveUI widget slot. Alignment writes the
    /// banner RectTransform pivot; fitted width then changes size only, not position.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)]
    public sealed class ResponsiveTextBanner : MonoBehaviour, IResponsiveUiClient
    {
        public enum Align
        {
            LeftUp,
            CenterUp,
            RightUp,
            LeftMiddle,
            CenterMiddle,
            RightMiddle,
            LeftDown,
            CenterDown,
            RightDown,
        }

        #region Serialized

        [Header("Targets")]
        [SerializeField] RectTransform banner;
        [SerializeField] TextMeshProUGUI text;

        [Header("Sizing")]
        [Min(0f)]
        [SerializeField] float textPadding = 20f;
        [Min(0f)]
        [SerializeField] float boxPadding = 100f;
        [Tooltip(
            "Sets the banner RectTransform pivot (Left Up, Right Middle, …). " +
            "After that, text width changes size only — anchoredPosition stays put.")]
        [SerializeField] Align alignment = Align.LeftMiddle;

        [Header("Display")]
        [Min(0f)]
        [SerializeField] float displayDuration = 2.5f;
        [SerializeField] bool clearTextOnHide = true;

        [Header("ResponsiveUI")]
        [Tooltip("Optional. Auto-resolves from the target's Canvas when empty.")]
        [SerializeField] ResponsiveUI responsiveUi;

        #endregion

        #region Runtime state

        ResponsiveUI _hub;
        // Unscaled auto-hide deadline (LateUpdate poll, no per-Show allocation).
        // Unscaled like the two-side tween so a paused timeScale still hides.
        float _hideAtUnscaledTime;
        bool _hideScheduled;
        Vector2 _slotPosition;
        Vector2 _slotSize;
        Vector2 _slotPivot;
        Vector2 _textSlotSize;
        float _fittedWidth;
        string _visibleText = string.Empty;
        bool _hasSlot;
        bool _visible;
        bool _previewLocked;
        bool _pendingHubCapture;

        // What this banner itself last wrote (Alignment pivot / position, fitted width).
        // Restore undoes only these writes, and only while the rect still holds them —
        // anything written since (the owning controller after a device / fold change,
        // Inspector LivePush) wins and becomes the slot instead of being reverted.
        bool _hasWrite;
        Vector2 _writtenPivot;
        Vector2 _writtenPosition;
        Vector2 _writtenSize;
        bool _hasTextWrite;
        Vector2 _writtenTextSize;

#if UNITY_EDITOR
        /// <summary>
        /// Editor widget sync assigns this so fitted preview writes are not captured
        /// into the owning <see cref="ResponsiveUiController"/> slot.
        /// </summary>
        public static Action<RectTransform> EditorIgnoreSlotCapture;
#endif

        public ResponsiveUI Hub =>
            ResponsiveUiHub.Resolve(responsiveUi, ResolveFrom);

        Component ResolveFrom => banner != null ? banner : transform;

        public bool IsVisible => _visible;
        public bool IsPreviewLocked => _previewLocked;
        public string VisibleText => _visibleText;
        public Align Alignment => alignment;

        /// <summary>
        /// Sets Left Middle / Right Middle / … alignment, writes the matching pivot, and re-fits when visible.
        /// </summary>
        public void SetAlignment(Align value)
        {
            alignment = value;
            RefreshLayout();
        }

        #endregion

        #region Lifecycle

        /// <summary>Assigns targets from code and captures their current slot geometry.</summary>
        public void SetTargets(
            RectTransform bannerTarget,
            TextMeshProUGUI textTarget,
            bool captureSlot = true)
        {
            banner = bannerTarget;
            text = textTarget;
            if (captureSlot)
                CaptureSlot();
        }

        void Reset()
        {
            banner = GetComponent<RectTransform>();
            text = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        void Awake()
        {
            ResolveTargets();
            CaptureSlot();
        }

        void OnEnable()
        {
            ResolveTargets();
            BindHub(true);
            if (!_visible)
            {
                SetBannerDisplayed(false);
                if (!RectStillOwned())
                    CaptureSlot();
            }
        }

        void OnDisable()
        {
            StopHide();
            // Undoes this banner's own writes; a rect the controller rewrote is kept.
            RestoreSlot();
            _visible = false;
            _previewLocked = false;
            _visibleText = string.Empty;
            _pendingHubCapture = false;
            BindHub(false);
        }

        void OnTransformParentChanged()
        {
            BindHub(false);
            if (!isActiveAndEnabled)
                return;
            BindHub(true);
            if (_hub != null)
                ApplyFromHub(_hub, _hub.CurrentLayout, layoutOnly: false);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying)
                return;

            if (_pendingHubCapture)
                CaptureAfterHub();

            if (_hideScheduled && Time.unscaledTime >= _hideAtUnscaledTime)
            {
                _hideScheduled = false;
                Hide();
            }
        }

        /// <summary>Shows text and optionally hides it after the configured duration.</summary>
        public void Show(string value, bool autoHide = true)
        {
            if (_previewLocked && autoHide)
                return;

            ResolveTargets();
            if (banner == null || text == null || string.IsNullOrEmpty(value))
                return;

            StopHide();

            // Same message re-shown while already fitted: only extend the timer —
            // skip the TMP re-measure (gameplay can repeat a message every event).
            bool unchanged =
                _visible &&
                !_pendingHubCapture &&
                _fittedWidth > 0f &&
                value == _visibleText &&
                RectStillOwned();

            if (!unchanged)
            {
                if (!_pendingHubCapture && !_visible)
                    RecaptureLiveSlot();

                _visibleText = value;
                _visible = true;
                text.SetText(value);
            }

            SetBannerDisplayed(true);

            if (!unchanged && !_pendingHubCapture)
                ApplyTextFit();

            if (autoHide && Application.isPlaying && displayDuration > 0f && isActiveAndEnabled)
            {
                _hideAtUnscaledTime = Time.unscaledTime + displayDuration;
                _hideScheduled = true;
            }
        }

        /// <summary>
        /// Hides the banner and undoes its own fitting / Alignment writes. If the owning
        /// controller rewrote the rect since (device or fold change), that layout is kept.
        /// </summary>
        public void Hide()
        {
            StopHide();
            RestoreSlot();
            SetBannerDisplayed(false);
            if (clearTextOnHide && text != null)
                text.text = string.Empty;

            _visible = false;
            _visibleText = string.Empty;
        }

        /// <summary>
        /// Writes the Alignment pivot. When visible, re-measures text and changes
        /// width only — anchoredPosition is not rewritten if the pivot already matches.
        /// </summary>
        public void RefreshLayout()
        {
            if (_pendingHubCapture)
            {
                CaptureAfterHub();
                return;
            }

            ApplyAlignmentPivot();
            if (_visible)
                ApplyTextFit();
        }

        /// <summary>
        /// Captures the current target rect as the slot original. Call only after the
        /// owning ResponsiveUiController has applied its active widget slot.
        /// </summary>
        public void CaptureSlot()
        {
            if (_pendingHubCapture)
                return;

            ResolveTargets();
            SnapshotSlot();
        }

        void SnapshotSlot()
        {
            if (banner == null)
                return;

            _slotPosition = banner.anchoredPosition;
            _slotSize = banner.sizeDelta;
            _slotPivot = banner.pivot;
            if (text != null)
                _textSlotSize = text.rectTransform.sizeDelta;
            _hasSlot = true;
        }

        /// <summary>Blocks normal timed shows while an Editor preview owns the banner.</summary>
        public void SetPreviewLock(bool locked)
        {
            if (_previewLocked == locked)
                return;

            _previewLocked = locked;
            if (!locked)
                Hide();
        }

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            if (!isActiveAndEnabled || !hub.enabled)
                return;
            if (!ResponsiveUiHub.BeginApplyFromHub(
                    this, ResolveFrom, hub, ref responsiveUi, ref _hub, layoutOnly))
                return;

            _pendingHubCapture = true;
        }

        /// <summary>
        /// Play Mode same-form size ticks recapture only while shown.
        /// Simulator canvas-ready still notifies this client.
        /// </summary>
        public bool SizeChangeRequiresReapply() => _visible;

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget() => false;
#endif

        #endregion

        #region Layout math

        /// <summary>Returns banner width from measured TMP width and configured padding.</summary>
        public static float CalculateWidth(
            float preferredTextWidth,
            float textPadding,
            float boxPadding)
        {
            return Mathf.Max(0f, preferredTextWidth) +
                   Mathf.Max(0f, textPadding) +
                   Mathf.Max(0f, boxPadding);
        }

        /// <summary>
        /// Normalized 0–1 pivot of an <see cref="Align"/> value (left/bottom = 0).
        /// </summary>
        public static Vector2 AlignmentAnchor(Align alignment)
        {
            int index = (int)alignment;
            return new Vector2((index % 3) * 0.5f, 1f - (index / 3) * 0.5f);
        }

        /// <summary>
        /// Anchored position of the widget slot with the Alignment pivot applied.
        /// Independent of fitted width — once this pivot is written, size changes
        /// must not write a new anchoredPosition.
        /// </summary>
        public static Vector2 CalculateAlignedPosition(
            Vector2 slotPosition,
            Vector2 slotSize,
            Vector2 slotPivot,
            Align alignment)
        {
            return ConvertAnchoredPositionForPivot(
                slotPosition,
                slotSize,
                slotPivot,
                AlignmentAnchor(alignment));
        }

        /// <summary>
        /// Returns the anchoredPosition that keeps the same parent-space rect when pivot changes.
        /// </summary>
        public static Vector2 ConvertAnchoredPositionForPivot(
            Vector2 position,
            Vector2 size,
            Vector2 fromPivot,
            Vector2 toPivot)
        {
            return new Vector2(
                position.x - size.x * fromPivot.x + size.x * toPivot.x,
                position.y - size.y * fromPivot.y + size.y * toPivot.y);
        }

        #endregion

        #region Helpers

        void ResolveTargets()
        {
            if (banner == null)
                banner = GetComponent<RectTransform>();
            if (text == null && banner != null)
                text = banner.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        void ApplyTextFit()
        {
            if (_pendingHubCapture)
                return;

            ResolveTargets();
            if (!_hasSlot || banner == null || text == null ||
                string.IsNullOrEmpty(_visibleText))
                return;

            TextWrappingModes previousWrapping = text.textWrappingMode;
            TextOverflowModes previousOverflow = text.overflowMode;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.SetText(_visibleText);
            text.ForceMeshUpdate(ignoreActiveState: true);

            float preferredWidth = text.preferredWidth;
            if (preferredWidth <= 0f)
                preferredWidth = text.GetPreferredValues(
                    _visibleText,
                    99999f,
                    99999f).x;

            text.textWrappingMode = previousWrapping;
            text.overflowMode = previousOverflow;

            float textWidth = CalculateWidth(preferredWidth, textPadding, 0f);
            float bannerWidth = CalculateWidth(
                preferredWidth,
                textPadding,
                boxPadding);

            RectTransform textRect = text.rectTransform;
            textRect.sizeDelta = new Vector2(textWidth, _textSlotSize.y);
            _writtenTextSize = textRect.sizeDelta;
            _hasTextWrite = true;
            if (!PivotMatchesAlignment())
                ApplyAlignmentPivot();
            IgnoreEditorSlotCapture();
            banner.sizeDelta = new Vector2(bannerWidth, _slotSize.y);
            _fittedWidth = bannerWidth;
            RememberBannerWrite();

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(textRect);
                UnityEditor.EditorUtility.SetDirty(banner);
            }
#endif
        }

        void BindHub(bool bind) =>
            ResponsiveUiHub.BindClient(this, ResolveFrom, ref responsiveUi, ref _hub, bind);

        void CaptureAfterHub()
        {
            _pendingHubCapture = false;
            // Rect still holds this banner's own write: nothing rewrote it, so the
            // stored slot is still the original. Otherwise the hub-written rect is the slot.
            if (!RectStillOwned())
                AdoptLiveSlot();

            if (!_visible)
                return;
            ApplyAlignmentPivot();
            ApplyTextFit();
        }

        void RecaptureLiveSlot()
        {
            ResolveTargets();
            if (banner == null)
                return;

            // Undo this banner's own fitted / aligned writes (or adopt a rect the
            // controller rewrote), then sample that as the slot. A fitted width is never
            // stored as the slot.
            RestoreSlot();
            CaptureSlot();
        }

        /// <summary>
        /// True while the rect still holds exactly what this banner last wrote — i.e.
        /// nothing else (controller Apply, Inspector LivePush) changed it since.
        /// </summary>
        bool RectStillOwned()
        {
            return _hasWrite &&
                   banner != null &&
                   Near(banner.pivot, _writtenPivot, 1e-4f) &&
                   Near(banner.anchoredPosition, _writtenPosition, 0.01f) &&
                   Near(banner.sizeDelta, _writtenSize, 0.01f);
        }

        static bool Near(Vector2 a, Vector2 b, float tolerance) =>
            Mathf.Abs(a.x - b.x) <= tolerance &&
            Mathf.Abs(a.y - b.y) <= tolerance;

        void RememberBannerWrite()
        {
            if (banner == null)
                return;
            _writtenPivot = banner.pivot;
            _writtenPosition = banner.anchoredPosition;
            _writtenSize = banner.sizeDelta;
            _hasWrite = true;
        }

        /// <summary>
        /// Someone else rewrote the banner rect after this banner's last write: keep their
        /// layout and make it the slot. Only the fitted TMP rect (ours) is put back first so
        /// its fitted width is not sampled as the text slot.
        /// </summary>
        void AdoptLiveSlot()
        {
            RestoreTextRect();
            _hasWrite = false;
            _fittedWidth = 0f;
            SnapshotSlot();
        }

        void RestoreTextRect()
        {
            if (_hasTextWrite &&
                text != null &&
                Near(text.rectTransform.sizeDelta, _writtenTextSize, 0.01f))
                text.rectTransform.sizeDelta = _textSlotSize;
            _hasTextWrite = false;
        }

        bool PivotMatchesAlignment()
        {
            if (banner == null)
                return false;
            Vector2 alignPivot = AlignmentAnchor(alignment);
            return Mathf.Abs(banner.pivot.x - alignPivot.x) <= 1e-4f &&
                   Mathf.Abs(banner.pivot.y - alignPivot.y) <= 1e-4f;
        }

        void ApplyAlignmentPivot()
        {
            ResolveTargets();
            if (banner == null)
                return;
            if (!_hasSlot)
                CaptureSlot();
            if (!_hasSlot)
                return;

            Vector2 alignPivot = AlignmentAnchor(alignment);
            IgnoreEditorSlotCapture();
            banner.pivot = alignPivot;
            banner.anchoredPosition = CalculateAlignedPosition(
                _slotPosition,
                _slotSize,
                _slotPivot,
                alignment);
            RememberBannerWrite();

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(banner);
#endif
        }

        /// <summary>
        /// Undoes this banner's own writes. If the rect no longer holds them (the owning
        /// controller applied a new device / fold layout, or LivePush wrote it), that
        /// layout is kept and adopted as the slot — restoring the older snapshot would
        /// snap the banner back to the previous device state.
        /// </summary>
        void RestoreSlot()
        {
            if (banner == null)
            {
                _hasWrite = false;
                _hasTextWrite = false;
                _fittedWidth = 0f;
                return;
            }

            if (!_hasSlot || !RectStillOwned())
            {
                AdoptLiveSlot();
                return;
            }

            IgnoreEditorSlotCapture();
            banner.pivot = _slotPivot;
            banner.anchoredPosition = _slotPosition;
            banner.sizeDelta = _slotSize;
            RestoreTextRect();
            _hasWrite = false;
            _fittedWidth = 0f;
        }

        void StopHide()
        {
            _hideScheduled = false;
        }

        void SetBannerDisplayed(bool displayed)
        {
            if (banner == null)
                return;

            // The banner RectTransform is often this GameObject (gameplay message).
            // Deactivating it would disable this client, clear preview lock, and
            // recapture a fitted rect on the next Show — preview Next/Prev then jumps.
            if (banner.gameObject == gameObject)
            {
                if (displayed && !gameObject.activeSelf)
                    gameObject.SetActive(true);

                Graphic graphic = banner.GetComponent<Graphic>();
                if (graphic != null)
                    graphic.enabled = displayed;
                if (text != null)
                    text.enabled = displayed;
                return;
            }

            banner.gameObject.SetActive(displayed);
        }

        void IgnoreEditorSlotCapture()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                EditorIgnoreSlotCapture?.Invoke(banner);
#endif
        }

        #endregion
    }
}
