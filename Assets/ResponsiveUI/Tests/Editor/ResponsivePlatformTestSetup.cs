using FDPlus.ResponsiveUI;
using NUnit.Framework;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Assembly-wide setup: detection tests cover every form (Android Fold / Flip, iPhone Duo, Wide
    /// Screen) whatever the Editor build target. Platform tests pin it with
    /// <see cref="ResponsivePlatforms.SetOverride"/> and turn <see cref="ResponsivePlatforms.ConstrainsForms"/>
    /// back on for their own scope.
    /// </summary>
    [SetUpFixture]
    public class ResponsivePlatformTestSetup
    {
        [OneTimeSetUp]
        public void Unconstrain() => ResponsivePlatforms.ConstrainsForms = false;

        [OneTimeTearDown]
        public void Restore()
        {
            ResponsivePlatforms.ConstrainsForms = true;
            ResponsivePlatforms.SetOverride(null);
        }
    }
}
