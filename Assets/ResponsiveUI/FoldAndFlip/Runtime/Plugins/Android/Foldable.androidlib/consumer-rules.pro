# Unity reaches this bridge through JNI/reflection. The layout payload is a packed string
# (FoldableLayoutInfo.toPayload), so FoldableLayoutInfo needs no keep rule.
-keep class com.futuredigitalplus.foldandflip.FoldableHelper {
    public *;
}
-keep interface com.futuredigitalplus.foldandflip.FoldableHelper$WindowInfoLayoutListener {
    public *;
}
-keep class com.futuredigitalplus.foldandflip.FoldableWindowAreaTracker {
    *;
}
-keepattributes InnerClasses,EnclosingMethod
