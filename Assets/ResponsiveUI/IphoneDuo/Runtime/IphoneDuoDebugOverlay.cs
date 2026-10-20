using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Development builds on an iPhone Duo, and Play Mode in the Editor on any build target: shows
    /// <see cref="IphoneDuoDevice.State"/> in the top-left of the safe area while
    /// <see cref="IphoneDuoRules.debugOverlay"/> is on. Created only then (at the first scene, when rules
    /// are assigned or a state is published) and removed when the flag turns off.
    /// </summary>
    [AddComponentMenu("")]
    sealed class IphoneDuoDebugOverlay : MonoBehaviour
    {
        static IphoneDuoDebugOverlay s_Instance;

        GUIStyle style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create() => Ensure();

        /// <summary>Creates the overlay when it is enabled and missing (a no-op outside Play Mode).</summary>
        internal static void Ensure()
        {
            if (s_Instance != null || !Application.isPlaying || !Debug.isDebugBuild)
                return;
#if UNITY_EDITOR
            // Device Simulator iPhone Duo preview works on any Editor build target.
            bool platform = true;
#else
            bool platform = IphoneDuoDevice.IsSupported;
#endif
            if (!platform || !IphoneDuoDevice.Rules.debugOverlay)
                return;
            var host = new GameObject("iPhone Duo Debug Overlay") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(host);
            s_Instance = host.AddComponent<IphoneDuoDebugOverlay>();
        }

        void OnGUI()
        {
            IphoneDuoRules rules = IphoneDuoDevice.Rules;
            if (!rules.debugOverlay)
            {
                s_Instance = null;
                Destroy(gameObject);
                return;
            }

            int fontSize = Mathf.Max(12, Mathf.Min(Screen.width, Screen.height) / 45);
            if (style == null || style.fontSize != fontSize)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = fontSize,
                    wordWrap = false,
                    padding = new RectOffset(fontSize / 2, fontSize / 2, fontSize / 3, fontSize / 3),
                };
            }

            IphoneDuoState state = IphoneDuoDevice.State;
            string text =
                $"iPhone Duo · {state}\n" +
                $"Screen {Screen.width}×{Screen.height} · device {Input.deviceOrientation}\n" +
                $"Across lock: cover {rules.coverAcrossLock} · seated {rules.seatedAcrossLock} · flat {rules.flatAcrossLock}" +
                (rules.flatFlip180 ? " · flip 180°" : "") + $" · {rules.sidewaysTurn}\n" +
                $"Seated rotation {rules.seatedRotation}" +
                (rules.seatedRotation == IphoneDuoSeatedRotation.Locked ? $" · pose {rules.seatedLockedPose}" : "") +
                $"\nFlat rotation {rules.flatRotation}" +
                (rules.flatRotation == IphoneDuoFlatRotation.Locked ? $" · pose {rules.flatLockedPose}" : "");
            Vector2 size = style.CalcSize(new GUIContent(text));
            Rect safe = Screen.safeArea;
            // GUI space has y down; Screen.safeArea has y up.
            var rect = new Rect(safe.xMin + fontSize / 2f, Screen.height - safe.yMax + fontSize / 2f, size.x, size.y);
            GUI.Box(rect, text, style);
        }
    }
}
