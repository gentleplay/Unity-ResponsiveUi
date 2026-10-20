namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Defines the ordered slot fallback chain shared by runtime Apply and editor capture.
    /// Landscape falls back to Portrait of the same form. Cover / HalfOpened / Pad then
    /// fall back to Phone when their slots are disabled.
    /// iPhone Duo Seated skips its other slot (the opposite screen shape): Pad in the same
    /// orientation, Pad in the other one, then Phone the same way. Portrait (tall) → Pad Portrait →
    /// Pad Landscape → Phone Portrait → Phone Landscape; Landscape (wide) → Pad Landscape → Pad
    /// Portrait → Phone Landscape → Phone Portrait.
    /// Fold / Fold Cover Half Opened name the posture, so their two slots are opposite screen
    /// shapes: they skip the other slot and fall back to Phone by screen shape. Landscape (tall) →
    /// Phone Portrait; Portrait (wide) → Phone Landscape → Phone Portrait.
    /// Wide Screen never uses mobile slots: its exact slot, then the other Wide Screen orientation.
    /// A candidate of another platform's form is skipped (<see cref="ResponsivePlatforms"/>),
    /// except while an Editor Device Simulator profile is selected: that profile's chain applies
    /// on any build target.
    /// </summary>
    public static class ResponsiveWidgetSlotResolver
    {
        #region Fallback chain

        public const int MaxCandidates = 5;

        public static bool GetCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            out ResponsiveDevice.FormFactor form,
            out ResponsiveDevice.Orientation orientation)
        {
            if (!GetChainCandidate(key, index, out form, out orientation))
                return false;
            // e.g. Wide Screen on iOS / Android, or Pad / Phone on Wide Screen builds.
            // A selected Device Simulator profile applies its own chain on any Editor target.
            if (!ResponsivePlatforms.ConstrainsForms ||
                ResponsivePlatforms.IsActiveForm(form) ||
                ResponsiveDevice.EditorSimulatorDrivesLayout)
                return true;
            return false;
        }

        static bool GetChainCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            out ResponsiveDevice.FormFactor form,
            out ResponsiveDevice.Orientation orientation)
        {
            form = key.Form;
            orientation = key.Orientation;

            if (key.Form == ResponsiveDevice.FormFactor.WideScreen)
            {
                if (index == 1)
                {
                    orientation = orientation == ResponsiveDevice.Orientation.Landscape
                        ? ResponsiveDevice.Orientation.Portrait
                        : ResponsiveDevice.Orientation.Landscape;
                }
                return index == 0 || index == 1;
            }
            if (UsesPadFirstChain(key.Form))
                return GetPadFirstCandidate(key, index, ref form, ref orientation);
            if (UsesPhoneByShapeChain(key.Form))
                return GetPhoneByShapeCandidate(key, index, ref form, ref orientation);

            bool landscape =
                key.Orientation == ResponsiveDevice.Orientation.Landscape;
            // Every other non-Phone form falls back to Phone.
            bool fallsBackToPhone = key.Form != ResponsiveDevice.FormFactor.Phone;

            switch (index)
            {
                case 0:
                    return true;
                case 1 when landscape:
                    orientation = ResponsiveDevice.Orientation.Portrait;
                    return true;
                case 2 when fallsBackToPhone:
                    form = ResponsiveDevice.FormFactor.Phone;
                    return true;
                case 3 when fallsBackToPhone && landscape:
                    form = ResponsiveDevice.FormFactor.Phone;
                    orientation = ResponsiveDevice.Orientation.Portrait;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Where the Editor copies a newly enabled / seeded slot from (one-time authoring copy, not
        /// Apply). Same as <see cref="GetCandidate"/> without the platform filter, except Wide Screen,
        /// which never reads mobile slots at runtime, seeds from Pad then Phone by shape.
        /// </summary>
        public static bool GetSeedCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            out ResponsiveDevice.FormFactor form,
            out ResponsiveDevice.Orientation orientation)
        {
            if (key.Form != ResponsiveDevice.FormFactor.WideScreen)
                return GetChainCandidate(key, index, out form, out orientation);

            form = key.Form;
            orientation = key.Orientation;
            return GetPadFirstCandidate(key, index, ref form, ref orientation);
        }

        /// <summary>Forms whose disabled slot tries Pad before Phone (by screen shape).</summary>
        static bool UsesPadFirstChain(ResponsiveDevice.FormFactor form) =>
            form == ResponsiveDevice.FormFactor.iPhoneDuoSeated;

        /// <summary>
        /// Forms whose slots name the posture, not the screen shape, so a disabled slot falls back
        /// to Phone by screen shape.
        /// </summary>
        static bool UsesPhoneByShapeChain(ResponsiveDevice.FormFactor form) =>
            form == ResponsiveDevice.FormFactor.FoldHalfOpened ||
            form == ResponsiveDevice.FormFactor.FoldCoverHalfOpened;

        /// <summary>
        /// Exact slot, then Pad and Phone by screen shape (matching axis first). The form's
        /// other slot is skipped: it is the opposite shape.
        /// </summary>
        static bool GetPadFirstCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            ref ResponsiveDevice.FormFactor form,
            ref ResponsiveDevice.Orientation orientation)
        {
            if (index == 0)
                return true;
            if (index < 1 || index > 4)
                return false;

            ResponsiveDevice.Orientation screen = key.Orientation;
            ResponsiveDevice.Orientation other =
                screen == ResponsiveDevice.Orientation.Landscape
                    ? ResponsiveDevice.Orientation.Portrait
                    : ResponsiveDevice.Orientation.Landscape;
            form = index <= 2
                ? ResponsiveDevice.FormFactor.Pad
                : ResponsiveDevice.FormFactor.Phone;
            orientation = (index & 1) == 1 ? screen : other;
            return true;
        }

        /// <summary>
        /// Exact slot, then Phone by screen shape (<see cref="ResponsiveDevice.OrientationForForm"/>):
        /// the Phone slot of the same shape, then Phone Portrait. The form's other slot is
        /// skipped: it is the opposite shape.
        /// </summary>
        static bool GetPhoneByShapeCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            ref ResponsiveDevice.FormFactor form,
            ref ResponsiveDevice.Orientation orientation)
        {
            ResponsiveDevice.Orientation screen =
                ResponsiveDevice.OrientationForForm(key.Form, key.Orientation);
            switch (index)
            {
                case 0:
                    return true;
                case 2:
                    form = ResponsiveDevice.FormFactor.Phone;
                    orientation = screen;
                    return true;
                case 3 when screen == ResponsiveDevice.Orientation.Landscape:
                    form = ResponsiveDevice.FormFactor.Phone;
                    orientation = ResponsiveDevice.Orientation.Portrait;
                    return true;
                default:
                    return false;
            }
        }

        #endregion
    }

}
