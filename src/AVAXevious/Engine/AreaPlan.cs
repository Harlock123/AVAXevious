using AVAXevious.Models;

namespace AVAXevious.Engine;

/// <summary>A wave of flyers arriving when the area has scrolled so far.</summary>
public sealed record AirWave(double W, AirKind Kind, int Count, double X);

/// <summary>
/// What an area holds: the things on the ground, the hidden ones, the flyers and when they come, and
/// whether the mothership passes over. Worked out from the area's number, so it's the same every time.
/// </summary>
public sealed class AreaPlan
{
    public required int Area { get; init; }
    public List<GroundTarget> Ground { get; } = [];
    public List<AirWave> Waves { get; } = [];
    /// <summary>Where the mothership arrives, or null for none.</summary>
    public double? MotherAt { get; init; }

    public static bool HasMothership(int area) => area % 2 == 0;

    public static AreaPlan Build(int area)
    {
        var rng = new Random(area * 7919 + 17);
        var plan = new AreaPlan { Area = area, MotherAt = HasMothership(area) ? 1650 : null };
        int a = (area - 1) % Field.Areas + 1;

        // Things on the ground, every so often; more kinds as the areas go on.
        for (double w = 140; w < Field.AreaLength - 80; w += 55 + rng.NextDouble() * 50)
        {
            if (plan.MotherAt is { } m && w > m - 80 && w < m + 420) continue;
            double roll = rng.NextDouble();
            var kind = a >= 4 && roll < 0.1 ? GroundKind.Derota
                : a >= 2 && roll < 0.28 ? GroundKind.Grobda
                : roll < 0.48 ? GroundKind.Logram
                : roll < 0.62 ? GroundKind.Zolbak
                : GroundKind.Barra;
            int group = kind == GroundKind.Barra && rng.NextDouble() < 0.5 ? 3 : 1;
            double x = 30 + rng.NextDouble() * (Field.Width - 60 - (group - 1) * 22);
            for (int i = 0; i < group; i++)
                plan.Ground.Add(new GroundTarget { Kind = kind, X = x + i * 22, W = w, Reload = 1 + rng.NextDouble() * 2 });
        }
        // Hidden: a Sol tower or two, and now and then a flag.
        for (int i = 0; i < 1 + a / 6; i++)
            plan.Ground.Add(new GroundTarget { Kind = GroundKind.Sol, X = 40 + rng.NextDouble() * 144, W = 400 + i * 900 + rng.NextDouble() * 500 });
        if (a % 4 == 1)
            plan.Ground.Add(new GroundTarget { Kind = GroundKind.Flag, X = 50 + rng.NextDouble() * 124, W = 700 + rng.NextDouble() * 800 });
        plan.Ground.Sort((p, q) => p.W.CompareTo(q.W));

        // Flyers.
        double gap = Math.Max(48, 95 - a * 4);
        for (double w = 60; w < Field.AreaLength - 40; w += gap + rng.NextDouble() * 40)
        {
            double roll = rng.NextDouble();
            var kind = a >= 3 && roll < 0.14 ? AirKind.Jara
                : a >= 2 && roll < 0.24 ? AirKind.Bacura
                : a >= 2 && roll < 0.44 ? AirKind.Zoshi
                : roll < 0.62 ? AirKind.Torkan
                : AirKind.Toroid;
            int count = kind switch
            {
                AirKind.Toroid => 4 + rng.Next(3),
                AirKind.Torkan => 1 + rng.Next(2),
                AirKind.Zoshi => 2 + rng.Next(2),
                AirKind.Jara => 3,
                _ => 2 + rng.Next(3),
            };
            plan.Waves.Add(new AirWave(w, kind, count, 30 + rng.NextDouble() * (Field.Width - 60)));
        }
        return plan;
    }
}
