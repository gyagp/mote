using Mote;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine($"PASS {name}");
}

Check(Placement.FindFreeSlot(12, 1908, 180, [new(600, 1100), new(1650, 1920)], 12) == 12,
    "Defaults to the far left when it is free");
Check(Placement.FindFreeSlot(12, 1908, 180, [new(0, 180), new(600, 1100), new(1650, 1920)], 12) == 192,
    "Uses the leftmost free gap when system controls occupy the left edge");
Check(Placement.FindFreeSlot(0, 1000, 180, [new(0, 470), new(450, 920)], 12) is null,
    "Crowded taskbar falls back without covering buttons");
Check(Placement.FindFreeSlot(-1920, 0, 180, [new(-1920, -1700), new(-400, 0)], 12) == -1688,
    "Negative display coordinates remain valid");
Check(Placement.FindFreeSlot(0, 600, 180, [new(420, 600), new(-100, 100), new(90, 210)], 12) == 222,
    "Unsorted and overlapping spans merge safely");
Check(Placement.FindFreeSlot(0, 200, 180, [new(500, 700)], 12) == 0,
    "Off-display rectangles do not block placement");
Check(Placement.FindFreeSlot(0, 100, 180, [], 12) is null,
    "Widget wider than the display does not produce invalid coordinates");
Check(Placement.FromFraction(0, 100, 200, 0.8) == 0, "Narrow displays clamp saved position");
Check(Placement.ToFraction(0, 1000, 180, 9999) == 1, "Dragging is constrained to the taskbar");
var roundtrip = Placement.ToFraction(-1000, 1000, 240, Placement.FromFraction(-1000, 1000, 240, 0.73));
Check(Math.Abs(roundtrip - 0.73) < 0.00001, "Saved position survives a coordinate conversion");

// Exercise changing icon layouts and DPI widths: every chosen placement must be on-screen and clear of controls.
var random = new Random(42);
for (var sample = 0; sample < 2000; sample++)
{
    var width = random.Next(120, 400);
    var spans = Enumerable.Range(0, random.Next(1, 20)).Select(_ =>
    {
        var start = random.Next(-100, 2000);
        return new Span(start, start + random.Next(20, 300));
    }).ToArray();
    var x = Placement.FindFreeSlot(12, 1908, width, spans, 12);
    if (x is null) continue;
    if (x < 12 || x + width > 1908 || spans.Any(s => x < s.End + 12 && x + width > s.Start - 12 && s.End > 12 && s.Start < 1908))
        throw new Exception($"Overlapping placement at sample {sample}");
}
Console.WriteLine($"PASS 2,000 generated taskbar layouts\n{checks + 1} checks passed.");
