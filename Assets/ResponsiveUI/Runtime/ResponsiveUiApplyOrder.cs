using System.Collections.Generic;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Shared client-notify priority and hierarchy apply order.
    /// Widgets settle before camera Focus Bounds; banners stay last (deferred capture).
    /// </summary>
    public static class ResponsiveUiApplyOrder
    {
        #region Client notify

        public const int CameraPriority = 2;

        public static int ClientPriority(IResponsiveUiClient client)
        {
            switch (client)
            {
                case ResponsiveUiController _:
                    return 0;
                case ResponsiveTwoSideUiTweenController _:
                    return 1;
                case ResponsiveCamera _:
                    return CameraPriority;
                case ResponsiveScrollView _:
                    return 3;
                case ResponsiveTextBanner _:
                    return 4;
                default:
                    return 5;
            }
        }

        public static void InsertionSortByPriority(
            List<IResponsiveUiClient> clients) =>
            InsertionSortByPriority(clients, 0, clients.Count);

        public static void InsertionSortByPriority(
            List<IResponsiveUiClient> clients,
            int start) =>
            InsertionSortByPriority(clients, start, clients.Count);

        public static void InsertionSortByPriority(
            List<IResponsiveUiClient> clients,
            int start,
            int end)
        {
            for (int i = start + 1; i < end; i++)
            {
                IResponsiveUiClient key = clients[i];
                int keyPriority = ClientPriority(key);
                int j = i - 1;
                while (j >= start &&
                       ClientPriority(clients[j]) > keyPriority)
                {
                    clients[j + 1] = clients[j];
                    j--;
                }

                clients[j + 1] = key;
            }
        }

        #endregion

        #region Hierarchy

        public static int HierarchyDepth(Transform target)
        {
            int depth = 0;
            for (Transform current = target;
                 current != null;
                 current = current.parent)
                depth++;
            return depth;
        }

        public static void InsertionSortByDepth(int[] order, int[] depths, int count)
        {
            for (int i = 1; i < count; i++)
            {
                int key = order[i];
                int keyDepth = depths[key];
                int j = i - 1;
                while (j >= 0 && depths[order[j]] > keyDepth)
                {
                    order[j + 1] = order[j];
                    j--;
                }

                order[j + 1] = key;
            }
        }

        #endregion

        #region Design Scale size reapply

        public static bool RequiresDesignScaleSizeReapply(
            ResponsiveUI hub,
            RectTransform target,
            bool scaleFromDesignCanvas)
        {
            if (!scaleFromDesignCanvas)
                return false;
            if (hub == null)
                return true;
            if (ResponsiveDesignScale.GetConstrainedCanvasSize(target, out _))
                return true;
            return !hub.UsesCanvasScalerResolutionFitFor(target);
        }

        #endregion
    }
}
