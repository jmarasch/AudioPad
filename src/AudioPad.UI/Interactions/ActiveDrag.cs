namespace AudioPad.UI.Interactions;

/// <summary>
/// Guarantees that at most one hold-drag is in progress anywhere in the app.
///
/// Every draggable item owns its own behaviour instance, and each one tracks the drag from the
/// TopLevel. When a drag ends without its pointer release arriving — which Android does whenever
/// something else claims the pointer — that instance keeps its handlers attached and goes on
/// believing it is being dragged. Traces showed five items in that state simultaneously, all
/// reacting to the same events, each holding its own leftover drop line and its own suppressed
/// carousel swipe. Since a swipe is only re-enabled when the drag that disabled it finishes,
/// stranding one drag left swipe-to-change-page broken for the rest of the session.
///
/// Starting a drag therefore ends any other one first, so a strand can outlive at most one gesture.
/// </summary>
internal static class ActiveDrag
{
    private static object? s_owner;
    private static Action? s_end;

    /// <summary>Registers a starting drag, ending whichever one was previously in progress.</summary>
    public static void Begin(object owner, Action end)
    {
        if (s_owner is not null && !ReferenceEquals(s_owner, owner))
        {
            var strandedEnd = s_end;
            s_owner = null;
            s_end = null;

            GestureLog.Write("ending stranded drag to start a new one");
            strandedEnd?.Invoke();
        }

        s_owner = owner;
        s_end = end;
    }

    /// <summary>Clears the registration for a drag that has finished normally.</summary>
    public static void Finish(object owner)
    {
        if (ReferenceEquals(s_owner, owner))
        {
            s_owner = null;
            s_end = null;
        }
    }
}
