using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Scene component that applies layout from a Canvas <see cref="ResponsiveUI"/> hub.
    /// Controllers, cameras, scroll views, and text banners register on enable and receive
    /// <see cref="ApplyFromHub"/> whenever the hub resolves a layout.
    /// </summary>
    public interface IResponsiveUiClient
    {
        ResponsiveUI Hub { get; }

        void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly);

        /// <summary>
        /// Play Mode same-form size ticks: when true, the hub also runs
        /// <see cref="ApplyFromHub"/> as layout-only. Device Simulator
        /// canvas-ready notifies every client regardless
        /// (<c>notifyAllLayoutClients</c>).
        /// </summary>
        bool SizeChangeRequiresReapply();

#if UNITY_EDITOR
        bool IsAuthoringSelectedTarget();
#endif
    }

    /// <summary>Resolves the Canvas hub for a client component and binds Apply.</summary>
    public static class ResponsiveUiHub
    {
        #region Resolve

        public static ResponsiveUI Resolve(ResponsiveUI assigned, Component from)
        {
            if (assigned != null)
                return assigned;
            if (from == null)
                return null;

            Canvas canvas = from.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                ResponsiveUI onCanvas = canvas.GetComponent<ResponsiveUI>();
                if (onCanvas != null)
                    return onCanvas;

                Canvas root = canvas.rootCanvas;
                if (root != canvas)
                {
                    ResponsiveUI onRoot = root.GetComponent<ResponsiveUI>();
                    if (onRoot != null)
                        return onRoot;
                }
            }

            return from.GetComponentInParent<ResponsiveUI>(true);
        }

        public static void BindClient(
            IResponsiveUiClient client,
            Component from,
            ref ResponsiveUI assigned,
            ref ResponsiveUI bound,
            bool bind)
        {
            if (bound != null)
            {
                bound.UnregisterClient(client);
                bound = null;
            }

            if (!bind)
                return;

            bound = Resolve(assigned, from);
            if (assigned == null)
                assigned = bound;
            if (bound != null)
                bound.RegisterClient(client);
        }

        /// <summary>
        /// Re-registers when the assigned Hub field no longer matches the bound hub
        /// (Inspector or <c>AssignHub</c> while the client stays enabled).
        /// </summary>
        public static bool RebindIfChanged(
            IResponsiveUiClient client,
            Component from,
            ref ResponsiveUI assigned,
            ref ResponsiveUI bound)
        {
            if (Resolve(assigned, from) == bound)
                return false;
            BindClient(client, from, ref assigned, ref bound, bind: true);
            return true;
        }

        /// <summary>
        /// Shared opening of <see cref="IResponsiveUiClient.ApplyFromHub"/>: rebinds when the
        /// Hub field changed, and when <paramref name="hub"/> is not the client's bound hub
        /// re-applies from the bound one instead. Returns false when the caller must stop.
        /// </summary>
        public static bool BeginApplyFromHub(
            IResponsiveUiClient client,
            Component from,
            ResponsiveUI hub,
            ref ResponsiveUI assigned,
            ref ResponsiveUI bound,
            bool layoutOnly)
        {
            bool rebound = RebindIfChanged(client, from, ref assigned, ref bound);
            if ((rebound || bound != null) && hub != bound)
            {
                ApplyNow(client, layoutOnly);
                return false;
            }

            return true;
        }

        public static void ApplyNow(IResponsiveUiClient client, bool layoutOnly)
        {
            ResponsiveUI hub = client.Hub;
            if (hub == null || !hub.enabled)
                return;
            if (hub.ApplyInFlight)
                return;
            client.ApplyFromHub(hub, hub.CurrentLayout, layoutOnly);
        }

        #endregion
    }
}
