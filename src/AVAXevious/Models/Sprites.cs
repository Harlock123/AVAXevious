namespace AVAXevious.Models;

/// <summary>
/// The game's pixel art, this game's own drawings. The ship is drawn row by row; the rounder things
/// are painted from ellipses and boxes into a pixel grid, which keeps them easy to read and to change. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F8, // white
        ['m'] = 0xFFA8ACB8, // silver
        ['d'] = 0xFF5C6070, // dark grey
        ['k'] = 0xFF16161C, // black
        ['r'] = 0xFFE83A3A, // red
        ['o'] = 0xFFFF8A1E, // orange
        ['y'] = 0xFFFFE14A, // yellow
        ['b'] = 0xFF3A7AE8, // blue
        ['c'] = 0xFF4FD7FF, // cyan
        ['g'] = 0xFF3CD84A, // green
        ['G'] = 0xFF1E7A2A, // dark green
    };

    // ----- the ship: a silver delta with a red cockpit stripe ---------------------------------------

    public static readonly Sprite Ship = S(
        ".......w.......",
        "......www......",
        "......wmw......",
        ".....wwrww.....",
        ".....wmrmw.....",
        "....wwmrmww....",
        "....wmmrmmw....",
        "...wwmmbmmww...",
        "..wwmmmbmmmww..",
        ".wwmmmmbmmmmww.",
        "wwmmmmmmmmmmmww",
        "wmmmmwdddwmmmmw",
        "wmmmw.....wmmmw",
        "wmmw.......wmmw",
        ".ww.........ww.");

    /// <summary>The ship leaning into a turn (to the right; mirrored for the left).</summary>
    public static readonly Sprite ShipBank = S(
        "........w......",
        ".......ww......",
        ".......wmw.....",
        "......wwrw.....",
        "......wmrmw....",
        ".....wwmrmw....",
        ".....wmmrmmw...",
        "....wwmmbmmw...",
        "...wwmmmbmmmw..",
        "..wwmmmmbmmmmw.",
        ".wwmmmmmmmmmmmw",
        ".wmmmmwdddwmmmw",
        ".wmmmw.....wmmw",
        "..mmw.......wmw",
        "...w.........w.");

    // ----- flyers -----------------------------------------------------------------------------------

    /// <summary>The Toroid: a ring seen turning, four frames.</summary>
    public static readonly Sprite[] Toroid =
    [
        new Canvas(12, 12).Ellipse(6, 6, 5.8, 5.8, 'c').Ellipse(6, 6, 3, 3, '.').Ellipse(6, 6, 5.8, 5.8, 'c').Ellipse(6, 6, 2.6, 2.6, '.').Dot(3, 3, 'w').Done(),
        new Canvas(12, 12).Ellipse(6, 6, 4.4, 5.8, 'c').Ellipse(6, 6, 2, 2.8, '.').Dot(4, 3, 'w').Done(),
        new Canvas(12, 12).Ellipse(6, 6, 2.2, 5.8, 'c').Box(6, 3, 6, 9, 'b').Done(),
        new Canvas(12, 12).Ellipse(6, 6, 4.4, 5.8, 'c').Ellipse(6, 6, 2, 2.8, '.').Dot(8, 3, 'w').Done(),
    ];

    /// <summary>The Torkan: a dark saucer with a glowing eye.</summary>
    public static readonly Sprite Torkan = new Canvas(14, 12).Ellipse(7, 7, 6.8, 3.6, 'd').Ellipse(7, 5, 4, 3.4, 'm')
        .Ellipse(7, 5, 1.8, 1.6, 'o').Dot(7, 5, 'y').Box(1, 7, 2, 8, 'r').Box(11, 7, 12, 8, 'r').Done();

    /// <summary>The Zoshi: a spinning cross, two frames.</summary>
    public static readonly Sprite[] Zoshi =
    [
        new Canvas(12, 12).Box(5, 0, 6, 11, 'r').Box(0, 5, 11, 6, 'r').Ellipse(6, 6, 2.4, 2.4, 'm').Dot(5, 5, 'w').Done(),
        new Canvas(12, 12).Dot(1, 1, 'r').Dot(2, 2, 'r').Dot(3, 3, 'r').Dot(4, 4, 'r').Dot(7, 7, 'r').Dot(8, 8, 'r').Dot(9, 9, 'r').Dot(10, 10, 'r')
            .Dot(10, 1, 'r').Dot(9, 2, 'r').Dot(8, 3, 'r').Dot(7, 4, 'r').Dot(4, 7, 'r').Dot(3, 8, 'r').Dot(2, 9, 'r').Dot(1, 10, 'r')
            .Box(2, 1, 2, 1, 'r').Box(9, 1, 9, 1, 'r').Ellipse(6, 6, 2.4, 2.4, 'm').Dot(5, 5, 'w').Done(),
    ];

    /// <summary>The Jara: a green swooping wing.</summary>
    public static readonly Sprite Jara = new Canvas(14, 10).Ellipse(7, 5, 6.8, 2.4, 'G').Ellipse(7, 4, 3, 3, 'g')
        .Dot(7, 3, 'y').Box(0, 4, 1, 6, 'g').Box(12, 4, 13, 6, 'g').Done();

    /// <summary>A spario, the enemies' shot: two frames, glowing.</summary>
    public static readonly Sprite[] Spario =
    [
        new Canvas(5, 5).Ellipse(2.5, 2.5, 2.5, 2.5, 'o').Dot(2, 2, 'w').Done(),
        new Canvas(5, 5).Ellipse(2.5, 2.5, 2.5, 2.5, 'y').Dot(2, 2, 'w').Dot(2, 0, 'o').Dot(2, 4, 'o').Dot(0, 2, 'o').Dot(4, 2, 'o').Done(),
    ];

    /// <summary>The flag: dug up, an extra ship.</summary>
    public static readonly Sprite Flag = S(
        "..rrrrrr..",
        "..rrrrry..",
        "..rrrryy..",
        "..rrryyy..",
        "..rrrrrr..",
        "..w.......",
        "..w.......",
        "..w.......",
        ".mwm......",
        "mmmmm.....");
}
