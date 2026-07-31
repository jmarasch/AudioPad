using Avalonia;
using Avalonia.VisualTree;

namespace AudioPad.UI.Interactions;

public static class VisualTreeHelpers
{
    /// <summary>Walks up from a visual (inclusive) to find the nearest ancestor whose DataContext is a <typeparamref name="T"/>.</summary>
    public static T? FindAncestorDataContext<T>(Visual? start)
        where T : class
    {
        for (var candidate = start; candidate is not null; candidate = candidate.GetVisualParent())
        {
            if (candidate is StyledElement { DataContext: T match })
            {
                return match;
            }
        }

        return null;
    }
}
