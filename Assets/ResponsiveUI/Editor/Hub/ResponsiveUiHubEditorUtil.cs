#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// SerializedObject helpers for the Canvas <see cref="ResponsiveUI"/> hub used by client
/// Inspectors (widget slot seeding) as well as <see cref="ResponsiveUIEditor"/>.
/// </summary>
public static class ResponsiveUiHubEditorUtil
{
    #region Canvas Scaler

    /// <summary>
    /// Wide Screen slots seeded from Pad keep Pad canvas units. While the owning hub's Wide Screen
    /// Canvas Scaler refs are still the field defaults (built-in Wide Screen canvas, match 0.5),
    /// copy its Pad refs so they match, in the seed's Undo step. Authored refs are left alone.
    /// </summary>
    public static bool CopyPadScalerRefsToWideScreen(SerializedObject clientOrHub)
    {
        SerializedObject hub = ResponsiveUiDetectionDrawer.HubSerializedObject(clientOrHub);
        SerializedProperty wideReference = hub?.FindProperty("wideScreenReferenceResolution");
        SerializedProperty wideMatch = hub?.FindProperty("wideScreenMatchWidthOrHeight");
        SerializedProperty padReference = hub?.FindProperty("padReferenceResolution");
        SerializedProperty padMatch = hub?.FindProperty("padMatchWidthOrHeight");
        if (wideReference == null || wideMatch == null || padReference == null || padMatch == null)
            return false;
        if (wideReference.vector2Value != ResponsiveRectState.BuiltinWideScreenDesignCanvasSize ||
            !Mathf.Approximately(wideMatch.floatValue, 0.5f))
            return false;

        wideReference.vector2Value = padReference.vector2Value;
        wideMatch.floatValue = padMatch.floatValue;
        if (hub != clientOrHub)
            hub.ApplyModifiedProperties();
        return true;
    }

    #endregion
}
}

#endif
