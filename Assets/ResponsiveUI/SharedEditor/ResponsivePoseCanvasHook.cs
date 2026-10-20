using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>
    /// Design canvas per pose row (Project Settings → Responsive UI), for settings pages outside
    /// the layout editor (Android Fold / Flip, iPhone Duo). Registered by ResponsiveUiEditorSettings;
    /// null until it loads. Form is the <c>ResponsiveDevice.FormFactor</c> value as an int.
    /// </summary>
    public static class ResponsivePoseCanvasHook
    {
        #region Hooks

        /// <summary>
        /// Row size for (form, landscape slot), oriented to the slot's screen shape
        /// (Fold half open Landscape slot = tall).
        /// </summary>
        public static Func<int, bool, Vector2> Get;

        /// <summary>
        /// Sets the row size for (form, landscape slot). Landscape (0,0) clears the override
        /// (the Portrait row rotated again).
        /// </summary>
        public static Action<int, bool, Vector2> Set;

        /// <summary>
        /// True when the row for (form, landscape slot) comes from the form's reference device and so
        /// is read-only (the Portrait row of every form with a catalog reference). Null: every row editable.
        /// </summary>
        public static Func<int, bool, bool> IsFromReference;

        #endregion
    }
}
