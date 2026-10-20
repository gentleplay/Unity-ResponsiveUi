using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("com.futuredigitalplus.iphoneduo.tests.editor")]
// The Simulator panel publishes its preview through IphoneDuoDevice.Publish; the iOS build step
// copies IphoneDuoHinge.Models to Info.plist.
[assembly: InternalsVisibleTo("com.futuredigitalplus.iphoneduo.editor")]
