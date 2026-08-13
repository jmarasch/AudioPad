namespace AudioPad.UI.Interactions;

/// <summary>
/// One switch for the touch-gesture tracing that diagnosing this app's gestures actually depends
/// on. Desktop uses mouse input with no competing pan/swipe recognizer, so it cannot reproduce
/// Android's touch behaviour — every gesture bug so far was found by tracing on the tablet, not by
/// reasoning about it on the desktop. <see cref="Console"/> output reaches <c>adb logcat</c> under
/// the <c>DOTNET</c> tag, so a trace here is readable on-device with no extra plumbing.
/// </summary>
internal static class GestureLog
{
    /// <summary>
    /// Flip to true to trace gestures while diagnosing one. Off in shipped builds. A readonly
    /// field rather than a const so that switching it off doesn't make every call site
    /// unreachable code, which the compiler rightly warns about.
    /// </summary>
    private static readonly bool Enabled = false;

    /// <summary>Writes one trace line, prefixed so it can be grepped out of a noisy logcat.</summary>
    public static void Write(string message)
    {
        if (Enabled)
        {
            Console.WriteLine($"[gesture] {message}");
        }
    }
}
