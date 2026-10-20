using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Hardware posture source for a player that is not Android (the iPhone Duo module ships one).
    /// <see cref="FoldablePostureDetector"/> keeps ownership, commit and publishing; the backend
    /// only reads the device. A module registers its backend before the first scene loads
    /// (<see cref="FoldablePostureBackend.Register"/>), so the shared assembly never references it.
    /// </summary>
    public interface IFoldablePostureBackend
    {
        /// <summary>This player runs on hardware with a hinge the backend reads.</summary>
        bool IsSupportedDevice { get; }

        /// <summary>Starts native delivery. Called on every owner activation; must be idempotent.</summary>
        void Attach();

        /// <summary>Stops native delivery and undoes any player policy the backend applied.</summary>
        void Detach();

        /// <summary>Application pause / resume while attached.</summary>
        void SetPaused(bool paused);

        /// <summary>
        /// Writes the current posture into <paramref name="layout"/> and returns true when the hinge
        /// or the screen changed since the last poll (always when <paramref name="force"/>).
        /// </summary>
        bool Poll(bool force, FoldableLayoutState layout);

        /// <summary>Runs every frame after the poll with the committed layout.</summary>
        void Tick(FoldableLayoutState committed);
    }

    /// <summary>The registered <see cref="IFoldablePostureBackend"/> of this player (null if none).</summary>
    public static class FoldablePostureBackend
    {
        public static IFoldablePostureBackend Current { get; private set; }

        /// <summary>Called by a backend module from <c>RuntimeInitializeOnLoadMethod</c>.</summary>
        public static void Register(IFoldablePostureBackend backend)
        {
            if (Current != null && backend != null && Current != backend)
            {
                Debug.LogWarning(
                    $"FoldAndFlip: posture backend {Current.GetType().Name} replaced by " +
                    backend.GetType().Name + ".");
            }
            Current = backend;
        }
    }
}
