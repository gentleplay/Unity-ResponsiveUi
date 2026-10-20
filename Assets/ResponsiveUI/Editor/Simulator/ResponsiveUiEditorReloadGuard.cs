#if UNITY_EDITOR
using UnityEditor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Removes editor-to-runtime hooks and transient Simulator UI before scripts
    /// reload. InitializeOnLoad owners bind fresh delegates after compilation.
    /// </summary>
    [InitializeOnLoad]
    static class ResponsiveUiEditorReloadGuard
    {
        static ResponsiveUiEditorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Teardown;
            AssemblyReloadEvents.beforeAssemblyReload += Teardown;
        }

        internal static void Teardown()
        {
            // Cancel deferred editor work and Simulator UI only. Do not null
            // EditorBeginApply / EditorPushHost / similar static Actions here —
            // a Teardown without a matching domain re-init would leave scroll
            // LivePush and camera capture suppress unbound until a full reload.
            ResponsiveUiEditorLayoutScheduler.CancelPending();
            ResponsiveUiSimulatorSlotFocus.Teardown();
            ResponsiveUiHingeGuideOverlay.Teardown();
        }
    }
}
#endif
