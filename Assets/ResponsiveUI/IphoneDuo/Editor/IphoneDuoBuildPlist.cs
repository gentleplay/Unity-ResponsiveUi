using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>
    /// iOS builds: writes the iPhone Duo launch settings to Info.plist for the native launch hold
    /// (IphoneDuoHinge.mm), which runs before any C#. The splash and first scenes then follow the
    /// posture rules too (held across the Player lock: Sideways turns them, Rotate gives the hold's
    /// shape, System Default leaves the lock). Keys: FDPIphoneDuoLaunchHold, FDPIphoneDuoFlatRule,
    /// FDPIphoneDuoSeatedRule, FDPIphoneDuoSeatedRotation, FDPIphoneDuoSeatedLockedPose,
    /// FDPIphoneDuoFlatRotation, FDPIphoneDuoFlatLockedPose,
    /// FDPIphoneDuoSidewaysQuarterTurns, FDPIphoneDuoTraceLog, FDPIphoneDuoModels (the hinge model
    /// list). With
    /// <see cref="IphoneDuoRules.launchScreenFillsAcrossHold"/> it also lists the other axis in
    /// <c>UISupportedInterfaceOrientations~iphone</c>, so iOS's launch screen fills a hold across the
    /// lock (iPads keep their own list). The game still runs on its own axis: Unity's view controller
    /// allows only the Player orientations at runtime.
    /// </summary>
    sealed class IphoneDuoBuildPlist : IPostprocessBuildWithReport
    {
        public const string LaunchHoldKey = "FDPIphoneDuoLaunchHold";
        public const string FlatRuleKey = "FDPIphoneDuoFlatRule";
        public const string SeatedRuleKey = "FDPIphoneDuoSeatedRule";
        /// <summary><see cref="IphoneDuoRules.seatedRotation"/> as an integer (0 Locked, 1 Follows hold).</summary>
        public const string SeatedRotationKey = "FDPIphoneDuoSeatedRotation";
        /// <summary><see cref="IphoneDuoRules.seatedLockedPose"/> as an integer (0 Auto, 1 Seated Portrait, 2 Seated Landscape).</summary>
        public const string SeatedLockedPoseKey = "FDPIphoneDuoSeatedLockedPose";
        /// <summary><see cref="IphoneDuoRules.flatRotation"/> as an integer (0 Follow device, 1 Locked).</summary>
        public const string FlatRotationKey = "FDPIphoneDuoFlatRotation";
        /// <summary><see cref="IphoneDuoRules.flatLockedPose"/> as an integer (0 Auto, 1 Wide, 2 Tall).</summary>
        public const string FlatLockedPoseKey = "FDPIphoneDuoFlatLockedPose";
        public const string TraceLogKey = "FDPIphoneDuoTraceLog";
        public const string QuarterTurnsKey = "FDPIphoneDuoSidewaysQuarterTurns";
        public const string ModelsKey = "FDPIphoneDuoModels";
        public const string IphoneOrientationsKey = "UISupportedInterfaceOrientations~iphone";
        const string BaseOrientationsKey = "UISupportedInterfaceOrientations";

        static readonly string[] PortraitOrientations =
            { "UIInterfaceOrientationPortrait", "UIInterfaceOrientationPortraitUpsideDown" };

        static readonly string[] LandscapeOrientations =
            { "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight" };

        /// <summary>
        /// Info.plist orientations for iPhones: the Player's (<paramref name="current"/>) plus the other
        /// axis of a one-axis lock when <see cref="IphoneDuoRules.launchScreenFillsAcrossHold"/> is on
        /// under Auto Rotation (<paramref name="autoRotation"/>); null when nothing changes.
        /// </summary>
        public static string[] LaunchOrientations(
            IphoneDuoRules rules,
            IphoneDuoAutorotateFlags project,
            bool autoRotation,
            string[] current)
        {
            if (!rules.launchScreenFillsAcrossHold || !autoRotation || !project.IsOneAxis)
                return null;

            var orientations = new System.Collections.Generic.List<string>(current);
            foreach (string other in project.AllowsLandscapeAxis ? PortraitOrientations : LandscapeOrientations)
            {
                if (!orientations.Contains(other))
                    orientations.Add(other);
            }
            return orientations.ToArray();
        }

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;
#if UNITY_IOS
            string path = Path.Combine(report.summary.outputPath, "Info.plist");
            if (!File.Exists(path))
                return;

            IphoneDuoRules rules = IphoneDuoEditorRules.Rules;
            var plist = new PlistDocument();
            plist.ReadFromFile(path);
            PlistElementDict root = plist.root;
            WriteRules(root, rules, (report.summary.options & BuildOptions.Development) != 0);

            var current = new System.Collections.Generic.List<string>();
            if (root[BaseOrientationsKey] is PlistElementArray baseOrientations)
            {
                foreach (PlistElement element in baseOrientations.values)
                    current.Add(element.AsString());
            }
            string[] launch = LaunchOrientations(
                rules, IphoneDuoEditorRules.ProjectFlags(), IphoneDuoEditorRules.RulesApply, current.ToArray());
            if (launch != null)
            {
                PlistElementArray iphone = root.CreateArray(IphoneOrientationsKey);
                foreach (string orientation in launch)
                    iphone.AddString(orientation);
            }
            plist.WriteToFile(path);
#endif
        }

#if UNITY_IOS
        /// <summary>Writes the rule keys the native launch hold reads to <paramref name="root"/>.</summary>
        static void WriteRules(PlistElementDict root, IphoneDuoRules rules, bool development)
        {
            root.SetBoolean(LaunchHoldKey, IphoneDuoEditorRules.RulesApply);
            root.SetInteger(FlatRuleKey, (int)rules.flatAcrossLock);
            root.SetInteger(SeatedRuleKey, (int)rules.seatedAcrossLock);
            root.SetInteger(SeatedRotationKey, (int)rules.seatedRotation);
            root.SetInteger(SeatedLockedPoseKey, (int)rules.seatedLockedPose);
            root.SetInteger(FlatRotationKey, (int)rules.flatRotation);
            root.SetInteger(FlatLockedPoseKey, (int)rules.flatLockedPose);
            // The native launch hold traces like the C# rules: development builds only.
            root.SetBoolean(TraceLogKey, rules.traceLog && development);
            root.SetInteger(QuarterTurnsKey, rules.SidewaysQuarterTurns);
            PlistElementArray models = root.CreateArray(ModelsKey);
            foreach (string model in IphoneDuoHinge.Models)
                models.AddString(model);
        }
#endif
    }
}
