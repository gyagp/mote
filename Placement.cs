namespace Mote;

// Coordinates here are physical pixels. Keep layout independent of WPF's DPI-scaled units.
public readonly record struct Span(double Start, double End);

public static class Placement
{
    public static double? FindFreeSlot(double start, double end, double width, IEnumerable<Span> occupied, double gap)
    {
        if (width <= 0 || end - start < width) return null;
        var cursor = start;
        foreach (var item in occupied.Where(s => s.End > start && s.Start < end).OrderBy(s => s.Start))
        {
            var left = Math.Max(start, item.Start - gap);
            if (left - cursor >= width) return cursor;
            cursor = Math.Max(cursor, Math.Min(end, item.End + gap));
        }
        return end - cursor >= width ? cursor : null;
    }

    public static double FromFraction(double start, double end, double width, double fraction) =>
        start + Math.Max(0, end - start - width) * Math.Clamp(fraction, 0, 1);

    public static double ToFraction(double start, double end, double width, double position) =>
        end - start <= width ? 0 : Math.Clamp((position - start) / (end - start - width), 0, 1);
}
