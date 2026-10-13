namespace AVAXevious.Models;

/// <summary>A 2D point or vector in game pixels (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec Normal => Length < 1e-9 ? new Vec(0, 0) : this * (1 / Length);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: 224 by 256, portrait like the arcade's. The ground scrolls down it as the
/// ship flies on; a place on the ground is given by how far into the area it is (<i>W</i>), and it's
/// at the top of the screen when the area has scrolled that far.
/// </summary>
public static class Field
{
    public const int Width = 224;
    public const int Height = 256;
    public const double Step = 1.0 / 60;
    /// <summary>Ground pixels a second.</summary>
    public const double ScrollSpeed = 36;
    /// <summary>How far an area goes; a ship lost past the middle starts again from there.</summary>
    public const double AreaLength = 2400;
    public const double Checkpoint = 1200;
    public const int Areas = 16;
}

/// <summary>
/// What the ground looks like: forest, rivers, roads and the long straight lines scratched across the
/// plains. All worked out from the area and the distance, so it never needs storing.
/// </summary>
public static class Terrain
{
    private static uint Hash(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 374761393u + (uint)b * 668265263u + (uint)c * 2246822519u;
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    /// <summary>A number from 0 to 1, the same every time for the same arguments.</summary>
    public static double Noise(int a, int b, int c) => Hash(a, b, c) / (double)uint.MaxValue;

    /// <summary>Where the river runs at distance w, and how wide it is (0: no river here).</summary>
    public static (double X, double Width) River(int area, double w)
    {
        double phase = area * 1.7;
        double x = Field.Width / 2.0 + Math.Sin(w / 190 + phase) * 62 + Math.Sin(w / 77 + phase * 2) * 14;
        // The river comes and goes along the area.
        double on = Math.Sin(w / 520 + area);
        double width = on > 0.1 ? Math.Min(34, (on - 0.1) * 120) : 0;
        return (x, width);
    }

    /// <summary>Forest in this 16-pixel square of ground (by its column and row).</summary>
    public static bool Forest(int area, int col, int row)
    {
        // Big patches: low-frequency blobs, broken up a little.
        double big = Math.Sin(col * 0.9 + area) + Math.Sin(row * 0.31 + area * 2.3) + Math.Sin((col + row) * 0.23);
        return big > 0.9 || (big > 0.3 && Noise(area, col, row) > 0.55);
    }

    /// <summary>A road across the ground at this row of 16 pixels, and which way it slants.</summary>
    public static int Road(int area, int row) => Noise(area + 99, row, 7) < 0.035 ? (Noise(area, row, 3) < 0.5 ? -1 : 1) : 0;
}
