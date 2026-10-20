using System.Collections.Generic;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    #region Types

    /// <summary>
    /// Where the camera looks while focusing a world object.
    /// Does not write <see cref="Camera.rect"/> — slot Viewport still owns that.
    /// Auto uses this slot's Focus Bounds values when set, otherwise the whole camera.
    /// Values are serialized ints: keep them stable (3–6 are retired).
    /// </summary>
    public enum ResponsiveCameraFocusScreen
    {
        None = 0,
        Auto = 1,
        Full = 2,
        Bounds = 7,
    }

    /// <summary>
    /// How tightly the camera frames the slot <c>focusTarget</c>.
    /// </summary>
    public enum ResponsiveCameraFocusFit
    {
        Center = 0,
        Cover = 1,
        Fit = 2,
    }

    #endregion

    /// <summary>
    /// Frames a world object in a camera viewport or a slot Focus Bounds UI rect.
    /// Center slides the camera; Cover fills the pane (may crop); Fit shows the whole object.
    /// </summary>
    public static class ResponsiveCameraFocus
    {
        #region Buffers

        // Focus runs on Unity's main thread. Reuse buffers so bounds tracking and
        // repeated responsive applies do not allocate arrays.
        static readonly Vector3[] BoundsCorners = new Vector3[4];
        static readonly Vector3[] RootCorners = new Vector3[4];
        // One-shot lookups (GetTargetWorldBounds) rescan the hierarchy on every call.
        static readonly TargetBoundsCache UncachedBounds = new TargetBoundsCache();

        /// <summary>
        /// Per-camera-row component membership cache. Renderer/collider bounds remain
        /// live every frame; only hierarchy discovery is throttled.
        /// </summary>
        internal sealed class TargetBoundsCache
        {
            const int HierarchyRefreshFrames = 10;

            readonly List<Renderer> _renderers = new List<Renderer>(16);
            readonly List<Collider> _colliders = new List<Collider>(8);
            readonly List<Collider2D> _colliders2D = new List<Collider2D>(8);
            Transform _target;
            int _directChildCount;
            int _nextHierarchyRefreshFrame;

            public bool GetBounds(
                Transform target,
                SpriteRenderer ignoreBackground,
                out Bounds bounds) =>
                GetBounds(target, ignoreBackground, refreshHierarchy: false, out bounds);

            /// <param name="refreshHierarchy">
            /// True rescans the Target hierarchy now instead of on the throttled schedule.
            /// </param>
            internal bool GetBounds(
                Transform target,
                SpriteRenderer ignoreBackground,
                bool refreshHierarchy,
                out Bounds bounds)
            {
                if (refreshHierarchy ||
                    _target != target ||
                    target != null &&
                    target.childCount != _directChildCount ||
                    Time.frameCount >= _nextHierarchyRefreshFrame ||
                    ContainsDestroyedComponent())
                {
                    Refresh(target);
                }

                bounds = default;
                bool any = false;
                for (int i = 0; i < _renderers.Count; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null ||
                        !renderer.gameObject.activeInHierarchy ||
                        !BelongsToTarget(renderer.transform) ||
                        renderer == ignoreBackground)
                        continue;
                    Encapsulate(renderer.bounds, ref bounds, ref any);
                }

                if (any)
                    return bounds.extents.sqrMagnitude > 1e-12f;

                for (int i = 0; i < _colliders.Count; i++)
                {
                    Collider collider = _colliders[i];
                    if (collider == null ||
                        !collider.gameObject.activeInHierarchy ||
                        !BelongsToTarget(collider.transform))
                        continue;
                    Encapsulate(collider.bounds, ref bounds, ref any);
                }

                if (any)
                    return bounds.extents.sqrMagnitude > 1e-12f;

                for (int i = 0; i < _colliders2D.Count; i++)
                {
                    Collider2D collider = _colliders2D[i];
                    if (collider == null ||
                        !collider.gameObject.activeInHierarchy ||
                        !BelongsToTarget(collider.transform))
                        continue;
                    Encapsulate(collider.bounds, ref bounds, ref any);
                }

                return any && bounds.extents.sqrMagnitude > 1e-12f;
            }

            void Refresh(Transform target)
            {
                _target = target;
                _directChildCount = target != null ? target.childCount : 0;
                _renderers.Clear();
                _colliders.Clear();
                _colliders2D.Clear();
                if (target != null)
                {
                    target.GetComponentsInChildren(true, _renderers);
                    target.GetComponentsInChildren(true, _colliders);
                    target.GetComponentsInChildren(true, _colliders2D);
                }

                _nextHierarchyRefreshFrame =
                    Time.frameCount + HierarchyRefreshFrames;
            }

            bool BelongsToTarget(Transform componentTransform) =>
                componentTransform == _target ||
                componentTransform.IsChildOf(_target);

            bool ContainsDestroyedComponent()
            {
                for (int i = 0; i < _renderers.Count; i++)
                {
                    if (_renderers[i] == null)
                        return true;
                }
                for (int i = 0; i < _colliders.Count; i++)
                {
                    if (_colliders[i] == null)
                        return true;
                }
                for (int i = 0; i < _colliders2D.Count; i++)
                {
                    if (_colliders2D[i] == null)
                        return true;
                }
                return false;
            }

            static void Encapsulate(
                Bounds current,
                ref Bounds bounds,
                ref bool any)
            {
                if (!any)
                {
                    bounds = current;
                    any = true;
                    return;
                }

                bounds.Encapsulate(current);
            }
        }

        #endregion

        #region Apply

        /// <param name="ignoreBackground">Camera background sprite left out of the Target bounds.</param>
        /// <param name="bounds">Live Focus Bounds HUD rect (Auto / Bounds).</param>
        /// <param name="boundsRect">Stored normalized window rect; wins over <paramref name="bounds"/>.</param>
        /// <param name="settleCanvases">
        /// When true and a live Focus Bounds HUD frames this slot, runs
        /// <see cref="Canvas.ForceUpdateCanvases"/> before reading it. Pass false when
        /// the HUD is known settled (runtime target-motion re-frame).
        /// </param>
        /// <remarks>Focus never writes <see cref="Camera.rect"/> — slot Viewport owns that.</remarks>
        public static void Apply(
            Camera camera,
            Transform target,
            ResponsiveCameraFocusScreen screen,
            ResponsiveCameraFocusFit fit,
            float padding,
            SpriteRenderer ignoreBackground = null,
            RectTransform bounds = null,
            Rect boundsRect = default,
            bool settleCanvases = true)
        {
            if (camera == null || target == null)
                return;
            if (screen == ResponsiveCameraFocusScreen.None)
                return;

            // Full ignores Focus Bounds, so only settle when the HUD is actually read.
            bool readsLiveBounds =
                bounds != null &&
                !HasBoundsRect(boundsRect) &&
                (screen == ResponsiveCameraFocusScreen.Auto ||
                 screen == ResponsiveCameraFocusScreen.Bounds);
            if (settleCanvases && readsLiveBounds)
                Canvas.ForceUpdateCanvases();

            camera.ResetAspect();
            Rect pane = GetCameraFocusViewport(camera, screen, bounds, boundsRect);
            float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(camera, target);
            // One hierarchy scan per Apply: projection fit and centering share the bounds.
            bool hasBounds = GetTargetWorldBounds(target, ignoreBackground, out Bounds targetBounds);
            if (fit != ResponsiveCameraFocusFit.Center && hasBounds)
                ApplyProjectionFit(camera, targetBounds, depth, fit, padding, pane);

            CenterOnTarget(
                camera,
                hasBounds ? targetBounds.center : target.position,
                depth,
                pane.center);
        }

        public static Rect GetCameraFocusViewport(
            Camera camera,
            ResponsiveCameraFocusScreen screen,
            RectTransform bounds = null,
            Rect boundsRect = default)
        {
            if (screen != ResponsiveCameraFocusScreen.Auto &&
                screen != ResponsiveCameraFocusScreen.Bounds)
                return ResponsiveCameraState.FullViewport;

            if (camera != null && HasBoundsRect(boundsRect))
                return MapWindowRectToCameraViewport(
                    camera.rect, ClampBoundsRect(boundsRect));

            if (GetUiBoundsViewport(camera, bounds, out Rect pane))
                return pane;

            return ResponsiveCameraState.FullViewport;
        }

        public static bool HasBoundsRect(Rect rect) =>
            rect.width >= 0.05f && rect.height >= 0.05f;

        public static Rect ClampBoundsRect(Rect rect)
        {
            float x = Mathf.Clamp01(rect.x);
            float y = Mathf.Clamp01(rect.y);
            float w = Mathf.Clamp(rect.width, 0f, 1f - x);
            float h = Mathf.Clamp(rect.height, 0f, 1f - y);
            return new Rect(x, y, w, h);
        }

        /// <summary>
        /// Converts a UI rect to the Target camera's normalized viewport.
        /// Overlay uses the rect's fraction of the root canvas (Device Simulator
        /// Screen pixels do not match <see cref="Camera.pixelRect"/>). Falls back to
        /// <see cref="RectTransformUtility.WorldToScreenPoint"/> then
        /// <see cref="Camera.ScreenToViewportPoint"/>. Camera / world canvases use
        /// <see cref="Camera.WorldToViewportPoint"/>. Does not write <see cref="Camera.rect"/>.
        /// </summary>
        public static bool GetUiBoundsViewport(
            Camera camera,
            RectTransform bounds,
            out Rect viewport)
        {
            viewport = ResponsiveCameraState.FullViewport;
            if (camera == null || bounds == null || !bounds.gameObject.activeInHierarchy)
                return false;

            Canvas canvas = bounds.GetComponentInParent<Canvas>();
            if (canvas == null)
                return false;

            RectTransform root = (RectTransform)canvas.rootCanvas.transform;
            // Canvas fractions are reliable for screen-space canvases, including
            // Device Simulator where Screen pixels and Camera.pixelRect can differ.
            // A World Space canvas must be projected through the target camera.
            if (canvas.renderMode != RenderMode.WorldSpace &&
                GetNormalizedInRoot(bounds, root, out Rect window))
            {
                viewport = MapWindowRectToCameraViewport(camera.rect, window);
                if (viewport.width >= 0.05f && viewport.height >= 0.05f)
                    return true;
            }

            bounds.GetWorldCorners(BoundsCorners);
            bool overlay = canvas.renderMode == RenderMode.ScreenSpaceOverlay;
            return camera.pixelWidth >= 2 && camera.pixelHeight >= 2 &&
                   GetViewportFromCorners(camera, BoundsCorners, overlay, out viewport);
        }

        static bool GetViewportFromCorners(
            Camera camera,
            Vector3[] corners,
            bool overlay,
            out Rect viewport)
        {
            viewport = ResponsiveCameraState.FullViewport;
            float xMin = float.PositiveInfinity;
            float yMin = float.PositiveInfinity;
            float xMax = float.NegativeInfinity;
            float yMax = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector3 vp;
                if (overlay)
                {
                    // Overlay world XY is already screen pixels. Null is required:
                    // RectTransformUtility does not fall back to Camera.main — it
                    // returns (x, y). Passing `camera` would project those pixels
                    // through the 3D frustum and miss the HUD rect.
                    Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[i]);
                    vp = camera.ScreenToViewportPoint(screen);
                }
                else
                    vp = camera.WorldToViewportPoint(corners[i]);

                if (vp.x < xMin) xMin = vp.x;
                if (vp.y < yMin) yMin = vp.y;
                if (vp.x > xMax) xMax = vp.x;
                if (vp.y > yMax) yMax = vp.y;
            }

            viewport = Rect.MinMaxRect(
                Mathf.Clamp01(xMin),
                Mathf.Clamp01(yMin),
                Mathf.Clamp01(xMax),
                Mathf.Clamp01(yMax));
            return viewport.width >= 0.05f && viewport.height >= 0.05f;
        }

        static bool GetNormalizedInRoot(
            RectTransform bounds,
            RectTransform root,
            out Rect window)
        {
            window = default;
            bounds.GetWorldCorners(BoundsCorners);
            root.GetWorldCorners(RootCorners);
            float rw = RootCorners[2].x - RootCorners[0].x;
            float rh = RootCorners[2].y - RootCorners[0].y;
            if (Mathf.Abs(rw) >= 1e-4f && Mathf.Abs(rh) >= 1e-4f)
            {
                float xMin = float.PositiveInfinity;
                float yMin = float.PositiveInfinity;
                float xMax = float.NegativeInfinity;
                float yMax = float.NegativeInfinity;
                for (int i = 0; i < 4; i++)
                {
                    float nx = (BoundsCorners[i].x - RootCorners[0].x) / rw;
                    float ny = (BoundsCorners[i].y - RootCorners[0].y) / rh;
                    if (nx < xMin) xMin = nx;
                    if (ny < yMin) yMin = ny;
                    if (nx > xMax) xMax = nx;
                    if (ny > yMax) yMax = ny;
                }

                window = Rect.MinMaxRect(
                    Mathf.Clamp01(xMin),
                    Mathf.Clamp01(yMin),
                    Mathf.Clamp01(xMax),
                    Mathf.Clamp01(yMax));
                return window.width >= 0.01f && window.height >= 0.01f;
            }

            Rect rootRect = root.rect;
            if (rootRect.width < 1e-4f || rootRect.height < 1e-4f)
                return false;

            Rect boundsRect = bounds.rect;
            Vector3 bl = root.InverseTransformPoint(
                bounds.TransformPoint(new Vector3(boundsRect.xMin, boundsRect.yMin, 0f)));
            Vector3 tr = root.InverseTransformPoint(
                bounds.TransformPoint(new Vector3(boundsRect.xMax, boundsRect.yMax, 0f)));
            window = Rect.MinMaxRect(
                Mathf.Clamp01((Mathf.Min(bl.x, tr.x) - rootRect.xMin) / rootRect.width),
                Mathf.Clamp01((Mathf.Min(bl.y, tr.y) - rootRect.yMin) / rootRect.height),
                Mathf.Clamp01((Mathf.Max(bl.x, tr.x) - rootRect.xMin) / rootRect.width),
                Mathf.Clamp01((Mathf.Max(bl.y, tr.y) - rootRect.yMin) / rootRect.height));
            return window.width >= 0.01f && window.height >= 0.01f;
        }

        public static Rect MapWindowRectToCameraViewport(Rect cameraRect, Rect windowPane)
        {
            float cw = Mathf.Max(1e-5f, cameraRect.width);
            float ch = Mathf.Max(1e-5f, cameraRect.height);
            float xMin = Mathf.Clamp01((windowPane.xMin - cameraRect.x) / cw);
            float xMax = Mathf.Clamp01((windowPane.xMax - cameraRect.x) / cw);
            float yMin = Mathf.Clamp01((windowPane.yMin - cameraRect.y) / ch);
            float yMax = Mathf.Clamp01((windowPane.yMax - cameraRect.y) / ch);
            return new Rect(xMin, yMin, Mathf.Max(0f, xMax - xMin), Mathf.Max(0f, yMax - yMin));
        }

        #endregion

        #region Framing

        /// <summary>
        /// Slides the camera (no rotation) so <paramref name="focusPoint"/> sits at
        /// <paramref name="viewportCenter"/>.
        /// </summary>
        static void CenterOnTarget(
            Camera camera,
            Vector3 focusPoint,
            float depth,
            Vector2 viewportCenter)
        {
            float d = Mathf.Max(0.01f, Mathf.Abs(depth));
            Vector3 viewCenter = camera.ViewportToWorldPoint(
                new Vector3(viewportCenter.x, viewportCenter.y, d));
            Vector3 delta = focusPoint - viewCenter;
            delta -= Vector3.Dot(delta, camera.transform.forward) * camera.transform.forward;
            if (delta.sqrMagnitude < 1e-12f)
                return;
            camera.transform.position += delta;
        }

        public static Vector2 MeasureTargetSize(
            Transform target,
            Camera camera,
            SpriteRenderer ignoreBackground = null)
        {
            if (target == null || camera == null)
                return Vector2.zero;

            return GetTargetWorldBounds(target, ignoreBackground, out Bounds bounds)
                ? MeasureBoundsSize(bounds, camera)
                : Vector2.zero;
        }

        /// <summary>World size of <paramref name="bounds"/> along the camera right / up axes.</summary>
        static Vector2 MeasureBoundsSize(in Bounds bounds, Camera camera)
        {
            Vector3 extents = bounds.extents;
            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;
            float minR = float.MaxValue;
            float maxR = float.MinValue;
            float minU = float.MaxValue;
            float maxU = float.MinValue;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 d = new Vector3(
                            extents.x * x, extents.y * y, extents.z * z);
                        float r = Vector3.Dot(d, right);
                        float u = Vector3.Dot(d, up);
                        if (r < minR) minR = r;
                        if (r > maxR) maxR = r;
                        if (u < minU) minU = u;
                        if (u > maxU) maxU = u;
                    }
                }
            }

            return new Vector2(Mathf.Max(0f, maxR - minR), Mathf.Max(0f, maxU - minU));
        }

        static void ApplyProjectionFit(
            Camera camera,
            in Bounds targetBounds,
            float depth,
            ResponsiveCameraFocusFit fit,
            float padding,
            Rect cameraViewport)
        {
            Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(
                camera, depth, cameraViewport);
            Vector2 size = MeasureBoundsSize(targetBounds, camera);
            if (view.x < 1e-5f || view.y < 1e-5f || size.x < 1e-5f || size.y < 1e-5f)
                return;

            float pad = Mathf.Max(1f, padding);
            float sx = (size.x * pad) / view.x;
            float sy = (size.y * pad) / view.y;
            // Object/view ratios. Smaller ortho zooms in. Cover fills the pane
            // (min → more zoom, may crop). Fit shows the whole object (max → less zoom).
            float scale = fit == ResponsiveCameraFocusFit.Cover
                ? Mathf.Min(sx, sy)
                : Mathf.Max(sx, sy);
            if (Mathf.Abs(scale - 1f) < 1e-4f)
                return;

            if (camera.orthographic)
                camera.orthographicSize = Mathf.Max(0.01f, camera.orthographicSize * scale);
            else
            {
                float tanHalf = Mathf.Tan(0.5f * camera.fieldOfView * Mathf.Deg2Rad);
                camera.fieldOfView = 2f * Mathf.Atan(tanHalf * scale) * Mathf.Rad2Deg;
            }
        }

        /// <summary>
        /// Combined active Renderer bounds, or combined Collider / Collider2D bounds.
        /// Same rules as the per-camera <see cref="TargetBoundsCache"/> the runtime watcher
        /// uses, with a fresh hierarchy scan on every call.
        /// </summary>
        public static bool GetTargetWorldBounds(
            Transform target,
            SpriteRenderer ignoreBackground,
            out Bounds bounds) =>
            UncachedBounds.GetBounds(
                target, ignoreBackground, refreshHierarchy: true, out bounds);

        #endregion
    }
}
