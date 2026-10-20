using System;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>Normalizes ResponsiveUI-authored Device Simulator display labels.</summary>
    public static class ResponsiveUiSimulatorName
    {
        #region Prefixes

        public const string PhoneListPrefix = "● Phone / ";
        public const string PadListPrefix = "● Pad / ";
        /// <summary>Marks a Design Canvas reference skin in the Device Simulator list.</summary>
        public const string DesignReferenceMarker = "◆ ";
        public const string PhoneDesignListPrefix = DesignReferenceMarker + "Phone / ";
        public const string PadDesignListPrefix = DesignReferenceMarker + "Pad / ";

        /// <summary>Fold &amp; Flip toolkit skins that are not Design Canvas references.</summary>
        public const string ToolkitFoldPrefix = "★ Fold / ";
        public const string ToolkitFlipPrefix = "★ Flip / ";
        public const string ToolkitDualPrefix = "★ Dual / ";

        /// <summary>Design Canvas reference role labels (Fold &amp; Flip catalog).</summary>
        public const string DesignRoleFoldCover = "Fold Cover / ";
        public const string DesignRoleIphoneDuo = "iPhone Duo / ";
        public const string DesignRoleFold = "Fold / ";
        public const string DesignRoleFlip = "Flip / ";

        static readonly string[] VisiblePrefixes =
        {
            ToolkitFoldPrefix,
            ToolkitFlipPrefix,
            ToolkitDualPrefix,
            PhoneListPrefix,
            PadListPrefix,
            PhoneDesignListPrefix,
            PadDesignListPrefix,
            DesignReferenceMarker,
            // No role label is a prefix of another, so order does not matter here.
            DesignRoleFoldCover,
            DesignRoleIphoneDuo,
            DesignRoleFold,
            DesignRoleFlip,
        };

        #endregion

        #region Normalize

        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            name = name.TrimStart('\u0001');
            for (int i = 0; i < VisiblePrefixes.Length; i++)
            {
                if (name.StartsWith(VisiblePrefixes[i], StringComparison.Ordinal))
                {
                    name = name.Substring(VisiblePrefixes[i].Length);
                    break;
                }
            }

            return name.Equals("Foldable Generic", StringComparison.OrdinalIgnoreCase)
                ? "Generic"
                : name;
        }

        public static bool Match(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return false;
            return string.Equals(
                Normalize(first),
                Normalize(second),
                StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves Phone vs Pad from Simulator list role (<c>●/◆ Phone|Pad / …</c>).
        /// Catalog Kind and aspect are not consulted — list role is authored with the skin.
        /// </summary>
        public static bool ResolvePhonePadListRole(string friendlyName, out bool isPad)
        {
            isPad = false;
            if (string.IsNullOrEmpty(friendlyName))
                return false;

            string name = friendlyName.TrimStart('\u0001');
            if (name.StartsWith(PadListPrefix, StringComparison.Ordinal) ||
                name.StartsWith(PadDesignListPrefix, StringComparison.Ordinal))
            {
                isPad = true;
                return true;
            }

            if (name.StartsWith(PhoneListPrefix, StringComparison.Ordinal) ||
                name.StartsWith(PhoneDesignListPrefix, StringComparison.Ordinal))
            {
                isPad = false;
                return true;
            }

            return false;
        }

        #endregion
    }
}
