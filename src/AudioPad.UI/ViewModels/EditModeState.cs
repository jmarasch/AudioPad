using CommunityToolkit.Mvvm.ComponentModel;

namespace AudioPad.UI.ViewModels;

/// <summary>
/// Whether the board is being arranged rather than played, shared by reference down to every pad.
///
/// The mode exists because a pad had to mean too many things at once. Editing was a double-tap and
/// reordering was a long press, both competing with an ordinary tap that fires the clip — so the
/// app had to tell them apart by timing, mid-performance, while the carousel's swipe recogniser
/// fought for the same pointer. Splitting them into two modes makes each gesture unambiguous:
/// in edit mode a press-and-release opens settings and a press-and-drag reorders, and neither can
/// happen by accident while playing.
///
/// Deliberately not persisted. A soundboard should open ready to perform, not in whatever state it
/// was left in last night.
/// </summary>
public sealed partial class EditModeState : ObservableObject
{
    [ObservableProperty]
    private bool _isEditing;
}
