using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    #region Types

    /// <summary>
    /// How a <see cref="SpriteRenderer"/> fills the camera's visible world size.
    /// </summary>
    public enum ResponsiveCameraSpriteFitMode
    {
        /// <summary>Match camera width and height independently (may change aspect).</summary>
        Stretch = 0,
        /// <summary>Uniform scale using max(width, height) ratio — fill, may crop.</summary>
        Cover = 1,
        /// <summary>Uniform scale using min(width, height) ratio — fit inside, may letterbox.</summary>
        Fit = 2,
    }

    #endregion

    /// <summary>
    /// Scales a <see cref="SpriteRenderer"/> to fill the referenced camera's visible view area,
    /// then centers it on that view. View size comes from <see cref="Camera.ViewportToWorldPoint"/>
    /// (honors rect / aspect / gate fit). A Simple background uses localScale; Sliced / Tiled uses
    /// <see cref="SpriteRenderer.size"/>.
    /// </summary>
    public static class ResponsiveCameraSpriteFit
    {
        #region Visible size

        public static Vector2 GetVisibleWorldSize(Camera camera, float perspectiveDepth) =>
            GetVisibleWorldSize(camera, perspectiveDepth, new Rect(0f, 0f, 1f, 1f));

        public static Vector2 GetVisibleWorldSize(
            Camera camera,
            float perspectiveDepth,
            Rect cameraViewport)
        {
            if (camera == null)
                return Vector2.zero;

            float depth = Mathf.Max(0.01f, Mathf.Abs(perspectiveDepth));
            if (cameraViewport.width < 1e-4f || cameraViewport.height < 1e-4f)
                cameraViewport = new Rect(0f, 0f, 1f, 1f);

            Vector3 bl = camera.ViewportToWorldPoint(
                new Vector3(cameraViewport.xMin, cameraViewport.yMin, depth));
            Vector3 br = camera.ViewportToWorldPoint(
                new Vector3(cameraViewport.xMax, cameraViewport.yMin, depth));
            Vector3 tl = camera.ViewportToWorldPoint(
                new Vector3(cameraViewport.xMin, cameraViewport.yMax, depth));
            float width = Vector3.Distance(bl, br);
            float height = Vector3.Distance(bl, tl);
            if (width < 1e-5f || height < 1e-5f)
                return GetVisibleWorldSizeFallback(camera, depth);

            return new Vector2(width, height);
        }

        public static Vector2 GetVisibleWorldSizeFallback(Camera camera, float perspectiveDepth)
        {
            if (camera == null)
                return Vector2.zero;

            float aspect = ResolveAspect(camera);
            if (camera.orthographic)
            {
                float height = Mathf.Max(0.01f, camera.orthographicSize) * 2f;
                return new Vector2(height * aspect, height);
            }

            float depth = Mathf.Max(0.01f, Mathf.Abs(perspectiveDepth));
            float halfHeight = depth * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float heightPersp = halfHeight * 2f;
            return new Vector2(heightPersp * aspect, heightPersp);
        }

        public static float ResolveAspect(Camera camera)
        {
            if (camera == null)
                return 1f;

            int pw = camera.pixelWidth;
            int ph = camera.pixelHeight;
            if (pw > 0 && ph > 0)
                return pw / (float)ph;

            return Mathf.Max(0.01f, camera.aspect);
        }

        public static float PerspectiveDepth(Camera camera, Transform target)
        {
            if (camera == null || target == null)
                return 1f;

            Vector3 toTarget = target.position - camera.transform.position;
            float depth = Vector3.Dot(toTarget, camera.transform.forward);
            return Mathf.Abs(depth) < 0.01f ? 1f : depth;
        }

        #endregion

        #region Scale

        public static Vector2 ComputeScale(
            Vector2 spriteLocalSize,
            Vector2 worldSize,
            ResponsiveCameraSpriteFitMode mode)
        {
            float sw = Mathf.Max(1e-5f, Mathf.Abs(spriteLocalSize.x));
            float sh = Mathf.Max(1e-5f, Mathf.Abs(spriteLocalSize.y));
            float sx = worldSize.x / sw;
            float sy = worldSize.y / sh;

            switch (mode)
            {
                case ResponsiveCameraSpriteFitMode.Cover:
                    {
                        float s = Mathf.Max(sx, sy);
                        return new Vector2(s, s);
                    }
                case ResponsiveCameraSpriteFitMode.Fit:
                    {
                        float s = Mathf.Min(sx, sy);
                        return new Vector2(s, s);
                    }
                default:
                    return new Vector2(sx, sy);
            }
        }

        #endregion

        #region Apply

        /// <summary>
        /// Fits the single camera background and moves its render plane behind all
        /// normal camera content. Depth handles meshes; minimum sorting order handles
        /// other transparent SpriteRenderers.
        /// </summary>
        public static void ApplyBackground(
            SpriteRenderer background,
            Camera camera,
            ResponsiveCameraSpriteFitMode mode)
        {
            if (background == null || camera == null || background.sprite == null)
                return;

            PlaceBehindCameraContent(background, camera);
            Apply(background, camera, mode);
        }

        public static void Apply(
            SpriteRenderer sprite,
            Camera camera,
            ResponsiveCameraSpriteFitMode mode)
        {
            if (sprite == null || camera == null || sprite.sprite == null)
                return;

            float depth = PerspectiveDepth(camera, sprite.transform);
            Vector2 view = GetVisibleWorldSize(camera, depth);
            if (view.x <= 0f || view.y <= 0f)
                return;

            if (sprite.drawMode != SpriteDrawMode.Simple)
            {
                ApplyDrawMode(sprite, camera, view, mode);
                AlignToCameraView(sprite, camera, depth);
                return;
            }

            ApplySimple(sprite, camera, view, mode);
            AlignToCameraView(sprite, camera, depth);
        }

        public static void PlaceBehindCameraContent(SpriteRenderer background, Camera camera)
        {
            float near = camera.nearClipPlane;
            float far = Mathf.Max(near + 0.02f, camera.farClipPlane);
            float desiredDepth = far - Mathf.Max(0.01f, (far - near) * 0.01f);
            float currentDepth = Vector3.Dot(
                background.bounds.center - camera.transform.position,
                camera.transform.forward);
            background.transform.position +=
                camera.transform.forward * (desiredDepth - currentDepth);
            int backLayerId = GetBackSortingLayerId();
            if (background.sortingLayerID != backLayerId)
                background.sortingLayerID = backLayerId;
            if (background.sortingOrder != short.MinValue)
                background.sortingOrder = short.MinValue;
        }

        // SortingLayer.layers allocates a new array per call. Layers are fixed in a
        // player, so Play Mode caches the back-most id (reset per Play session).
        static int s_backSortingLayerId;
        static bool s_hasBackSortingLayerId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_backSortingLayerId = 0;
            s_hasBackSortingLayerId = false;
        }

        /// <summary>Id of the first (back-most) sorting layer.</summary>
        public static int GetBackSortingLayerId()
        {
#if UNITY_EDITOR
            // Tags & Layers can be edited outside Play Mode — always read there.
            if (!Application.isPlaying)
                return ReadBackSortingLayerId();
#endif
            if (!s_hasBackSortingLayerId)
            {
                s_backSortingLayerId = ReadBackSortingLayerId();
                s_hasBackSortingLayerId = true;
            }

            return s_backSortingLayerId;
        }

        static int ReadBackSortingLayerId()
        {
            SortingLayer[] layers = SortingLayer.layers;
            return layers != null && layers.Length > 0 ? layers[0].id : 0;
        }

        #endregion

        #region Align

        /// <summary>
        /// Move the sprite so its bounds center sits on the camera view center (keeps depth).
        /// Required after scale so offset tabletop cameras do not leave clear-color gaps.
        /// </summary>
        public static void AlignToCameraView(SpriteRenderer sprite, Camera camera, float depth)
        {
            if (sprite == null || camera == null)
                return;

            float d = Mathf.Max(0.01f, Mathf.Abs(depth));
            Vector3 viewCenter = camera.ViewportToWorldPoint(
                new Vector3(0.5f, 0.5f, d));
            Vector3 spriteCenter = sprite.bounds.center;
            Vector3 delta = viewCenter - spriteCenter;
            delta -= Vector3.Dot(delta, camera.transform.forward) * camera.transform.forward;
            if (delta.sqrMagnitude < 1e-12f)
                return;

            sprite.transform.position += delta;
        }

        #endregion

        #region Measure

        /// <summary>
        /// Axis-aligned size of the sprite quad projected onto the camera right/up plane.
        /// </summary>
        public static Vector2 MeasureProjectedSize(SpriteRenderer sprite, Camera camera)
        {
            if (sprite == null || camera == null || sprite.sprite == null)
                return Vector2.zero;

            Vector3 c;
            Vector3 e;
            if (sprite.drawMode == SpriteDrawMode.Simple)
            {
                Bounds local = sprite.sprite.bounds;
                c = local.center;
                e = local.extents;
            }
            else
            {
                Vector2 size = sprite.size;
                c = Vector3.zero;
                e = new Vector3(size.x * 0.5f, size.y * 0.5f, 0f);
            }

            Vector3 right = camera.transform.right;
            Vector3 up = camera.transform.up;

            float minR = float.PositiveInfinity;
            float maxR = float.NegativeInfinity;
            float minU = float.PositiveInfinity;
            float maxU = float.NegativeInfinity;

            for (int i = 0; i < 4; i++)
            {
                float sx = ((i & 1) == 0) ? -e.x : e.x;
                float sy = ((i & 2) == 0) ? -e.y : e.y;
                Vector3 world = sprite.transform.TransformPoint(c + new Vector3(sx, sy, 0f));
                float r = Vector3.Dot(world, right);
                float u = Vector3.Dot(world, up);
                if (r < minR) minR = r;
                if (r > maxR) maxR = r;
                if (u < minU) minU = u;
                if (u > maxU) maxU = u;
            }

            return new Vector2(Mathf.Max(0f, maxR - minR), Mathf.Max(0f, maxU - minU));
        }

        /// <summary>
        /// World-space lengths contributed by localScale X=1 / Y=1 along camera right/up.
        /// </summary>
        public static bool MeasureUnitAxesOnCameraPlane(
            SpriteRenderer sprite,
            Camera camera,
            out float widthPerLocalX,
            out float widthPerLocalY,
            out float heightPerLocalX,
            out float heightPerLocalY)
        {
            widthPerLocalX = widthPerLocalY = heightPerLocalX = heightPerLocalY = 0f;
            if (sprite == null || camera == null || sprite.sprite == null)
                return false;

            Transform t = sprite.transform;
            Vector3 saved = t.localScale;
            try
            {
                t.localScale = new Vector3(1f, 1f, saved.z == 0f ? 1f : saved.z);
                Vector2 spriteSize = sprite.sprite.bounds.size;
                Vector3 edgeX = t.TransformVector(new Vector3(spriteSize.x, 0f, 0f));
                Vector3 edgeY = t.TransformVector(new Vector3(0f, spriteSize.y, 0f));

                Vector3 right = camera.transform.right;
                Vector3 up = camera.transform.up;
                widthPerLocalX = Mathf.Abs(Vector3.Dot(edgeX, right));
                widthPerLocalY = Mathf.Abs(Vector3.Dot(edgeY, right));
                heightPerLocalX = Mathf.Abs(Vector3.Dot(edgeX, up));
                heightPerLocalY = Mathf.Abs(Vector3.Dot(edgeY, up));
                return true;
            }
            finally
            {
                t.localScale = saved;
            }
        }

        #endregion

        #region Solve scale

        public static bool SolveLocalScale(
            Vector2 view,
            ResponsiveCameraSpriteFitMode mode,
            float widthPerLocalX,
            float widthPerLocalY,
            float heightPerLocalX,
            float heightPerLocalY,
            out float scaleX,
            out float scaleY)
        {
            scaleX = 1f;
            scaleY = 1f;

            float wX = widthPerLocalX;
            float wY = widthPerLocalY;
            float hX = heightPerLocalX;
            float hY = heightPerLocalY;

            float unitW = wX + wY;
            float unitH = hX + hY;
            if (unitW < 1e-6f || unitH < 1e-6f)
                return false;

            if (mode == ResponsiveCameraSpriteFitMode.Cover ||
                mode == ResponsiveCameraSpriteFitMode.Fit)
            {
                float sx = view.x / unitW;
                float sy = view.y / unitH;
                float s = mode == ResponsiveCameraSpriteFitMode.Cover
                    ? Mathf.Max(sx, sy)
                    : Mathf.Min(sx, sy);
                scaleX = s;
                scaleY = s;
                return true;
            }

            // Prefer dominant-axis mapping when the sprite is nearly camera-aligned
            // (tabletop boards). Avoids unstable 2×2 solves that under-fill one axis.
            const float dominant = 0.92f;
            float xRight = wX / unitW;
            float yRight = wY / unitW;
            float xUp = hX / unitH;
            float yUp = hY / unitH;

            if (xRight >= dominant && yUp >= dominant)
            {
                scaleX = wX > 1e-6f ? view.x / wX : view.x / unitW;
                scaleY = hY > 1e-6f ? view.y / hY : view.y / unitH;
                return true;
            }

            if (yRight >= dominant && xUp >= dominant)
            {
                scaleX = hX > 1e-6f ? view.y / hX : view.y / unitH;
                scaleY = wY > 1e-6f ? view.x / wY : view.x / unitW;
                return true;
            }

            float det = wX * hY - wY * hX;
            if (Mathf.Abs(det) > 1e-6f)
            {
                scaleX = (view.x * hY - wY * view.y) / det;
                scaleY = (wX * view.y - view.x * hX) / det;
                if (scaleX > 1e-6f && scaleY > 1e-6f)
                    return true;
            }

            if (wX >= wY && hY >= hX)
            {
                scaleX = wX > 1e-6f ? view.x / wX : view.x / unitW;
                scaleY = hY > 1e-6f ? view.y / hY : view.y / unitH;
                return true;
            }

            if (wY >= wX && hX >= hY)
            {
                scaleX = hX > 1e-6f ? view.y / hX : view.y / unitH;
                scaleY = wY > 1e-6f ? view.x / wY : view.x / unitW;
                return true;
            }

            scaleX = view.x / unitW;
            scaleY = view.y / unitH;
            return true;
        }

        #endregion

        #region Internal apply

        static void ApplySimple(
            SpriteRenderer sprite,
            Camera camera,
            Vector2 view,
            ResponsiveCameraSpriteFitMode mode)
        {
            if (!MeasureUnitAxesOnCameraPlane(
                    sprite, camera, out float widthPerLocalX, out float widthPerLocalY,
                    out float heightPerLocalX, out float heightPerLocalY))
                return;

            if (!SolveLocalScale(
                    view, mode,
                    widthPerLocalX, widthPerLocalY, heightPerLocalX, heightPerLocalY,
                    out float scaleX, out float scaleY))
                return;

            Vector3 current = sprite.transform.localScale;
            sprite.transform.localScale = new Vector3(
                SignedScale(scaleX, current.x),
                SignedScale(scaleY, current.y),
                current.z);
        }

        static void ApplyDrawMode(
            SpriteRenderer sprite,
            Camera camera,
            Vector2 view,
            ResponsiveCameraSpriteFitMode mode)
        {
            Vector2 saved = sprite.size;
            if (saved.x < 1e-5f) saved.x = 1f;
            if (saved.y < 1e-5f) saved.y = 1f;

            Vector2 sizeX = MeasureProjectedSizeWithDrawSize(sprite, camera, new Vector2(1f, 0f));
            Vector2 sizeY = MeasureProjectedSizeWithDrawSize(sprite, camera, new Vector2(0f, 1f));
            float wX = sizeX.x;
            float hX = sizeX.y;
            float wY = sizeY.x;
            float hY = sizeY.y;

            if (!SolveLocalScale(view, mode, wX, wY, hX, hY, out float sx, out float sy))
            {
                // Draw size is expressed in the renderer's local space, while view
                // is in world space. Keep the authored size if its projected axes
                // cannot be solved instead of assigning incompatible world units.
                sprite.size = saved;
                return;
            }

            sprite.size = new Vector2(Mathf.Abs(sx), Mathf.Abs(sy));
        }

        static Vector2 MeasureProjectedSizeWithDrawSize(
            SpriteRenderer sprite,
            Camera camera,
            Vector2 size)
        {
            Vector2 saved = sprite.size;
            sprite.size = size;
            Vector2 projected = MeasureProjectedSize(sprite, camera);
            sprite.size = saved;
            return projected;
        }

        static float SignedScale(float magnitude, float previous) =>
            previous < 0f ? -Mathf.Abs(magnitude) : Mathf.Abs(magnitude);

        #endregion
    }

}
