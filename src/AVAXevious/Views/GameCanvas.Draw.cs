using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AVAXevious.Engine;
using AVAXevious.Models;

namespace AVAXevious.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), IPen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVAXevious/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static IPen Pen(uint argb, double thickness)
    {
        if (!PenCache.TryGetValue((argb, thickness), out var p))
        {
            p = new ImmutablePen(argb, thickness);
            PenCache[(argb, thickness)] = p;
        }
        return p;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Orange = Brush(0xFFFF8A1E);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    private const uint Plain = 0xFF7C9A4A;
    private const uint PlainDark = 0xFF6E8C40;
    private const uint Forest = 0xFF2F5A26;
    private const uint Tree = 0xFF1E431A;
    private const uint TreeLight = 0xFF3F7432;
    private const uint Water = 0xFF2C5CA8;
    private const uint WaterLight = 0xFF4A7CC8;
    private const uint Bank = 0xFFB8A878;
    private const uint Road = 0xFF8C8478;

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private static void Polygon(DrawingContext dc, uint argb, params Point[] pts)
    {
        var geo = new StreamGeometry();
        using (var ctx = geo.Open())
        {
            ctx.BeginFigure(pts[0], true);
            foreach (var pt in pts.Skip(1)) ctx.LineTo(pt);
            ctx.EndFigure(true);
        }
        dc.DrawGeometry(Brush(argb), null, geo);
    }

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            DrawGround(dc, g.Area, g.Distance);
            switch (g.State)
            {
                case GameState.Title: DrawTitle(dc); break;
                case GameState.HighScores: DrawHighScores(dc); break;
                default: DrawPlay(dc); break;
            }
        }
        DrawPanels(dc);
    }

    // ----- the ground -----------------------------------------------------------------------------

    /// <summary>Plains, forest, a river and the odd road, scrolling down.</summary>
    private static void DrawGround(DrawingContext dc, int area, double distance)
    {
        Fill(dc, Plain, 0, 0, Field.Width, Field.Height);
        // Rows of 16 ground pixels, by how far into the area they are.
        int top = (int)Math.Floor((distance - Field.Height) / 16) - 1, bottom = (int)Math.Floor(distance / 16) + 1;
        for (int row = top; row <= bottom; row++)
        {
            double y = distance - (row + 1) * 16;
            if ((row & 1) == 0) Fill(dc, PlainDark, 0, y + 6, Field.Width, 2);
            for (int col = 0; col < Field.Width / 16; col++)
            {
                if (!Terrain.Forest(area, col, row)) continue;
                Fill(dc, Forest, col * 16, y, 16, 16);
                for (int t = 0; t < 3; t++)
                {
                    double tx = col * 16 + 3 + Terrain.Noise(area, col * 7 + t, row) * 10, ty = y + 3 + Terrain.Noise(row, t, col + area) * 10;
                    Fill(dc, Tree, tx, ty, 4, 4);
                    Fill(dc, TreeLight, tx, ty, 2, 2);
                }
            }
            if (Terrain.Road(area, row) is var slant and not 0)
            {
                var pen = Pen(Road, 3 * Scale);
                dc.DrawLine(pen, P(-4, y + 8 - slant * 30), P(Field.Width + 4, y + 8 + slant * 30));
            }
        }
        // The river, in thin strips so it winds smoothly.
        for (double y = 0; y < Field.Height; y += 3)
        {
            var (x, width) = Terrain.River(area, distance - y);
            if (width <= 0) continue;
            Fill(dc, Bank, x - width / 2 - 3, y, width + 6, 3);
            Fill(dc, Water, x - width / 2, y, width, 3);
            Fill(dc, WaterLight, x - width / 4, y + 1, 3, 1);
        }
    }

    private void DrawGroundTarget(DrawingContext dc, GroundTarget t, double y)
    {
        var c = P(t.X, y);
        double s = Scale;
        if (t.Wrecked)
        {
            dc.DrawEllipse(Brush(0xFF3A2A1A), null, c, 7 * s, 5 * s);
            dc.DrawEllipse(Brush(0xFF1A120A), null, c, 4 * s, 3 * s);
            return;
        }
        switch (t.Kind)
        {
            case GroundKind.Barra:
                // A pyramid seen from above: four faces in different shades.
                Polygon(dc, 0xFFB8BCC8, c + new Point(-8 * s, -8 * s), c + new Point(8 * s, -8 * s), c);
                Polygon(dc, 0xFF8A8E9C, c + new Point(8 * s, -8 * s), c + new Point(8 * s, 8 * s), c);
                Polygon(dc, 0xFF5A5E6C, c + new Point(8 * s, 8 * s), c + new Point(-8 * s, 8 * s), c);
                Polygon(dc, 0xFFA0A4B0, c + new Point(-8 * s, 8 * s), c + new Point(-8 * s, -8 * s), c);
                break;
            case GroundKind.Zolbak:
                dc.DrawEllipse(Brush(0xFF6A6E7A), null, c, 8 * s, 8 * s);
                dc.DrawEllipse(Brush(0xFFC8CCD8), null, c, 6 * s, 6 * s);
                {
                    double a = _clock * 3;
                    dc.DrawLine(Pen(0xFFE83A3A, 2 * s), c, c + new Point(Math.Cos(a) * 6 * s, Math.Sin(a) * 6 * s));
                }
                dc.DrawEllipse(Brush(0xFFE83A3A), null, c, 1.5 * s, 1.5 * s);
                break;
            case GroundKind.Logram:
                dc.DrawEllipse(Brush(0xFF4A4E5A), null, c, 7 * s, 7 * s);
                dc.DrawEllipse(Brush(0xFF9A9EAA), null, c, 5 * s, 5 * s);
                dc.DrawEllipse(Brush(t.Reload < 0.3 ? 0xFFFF8A1Eu : 0xFF2A2E3Au), null, c, 2.5 * s, 2.5 * s);
                break;
            case GroundKind.Grobda:
            {
                var body = new Rect(c.X - 7 * s, c.Y - 5 * s, 14 * s, 10 * s);
                dc.FillRectangle(Brush(0xFF5A6A3A), body);
                dc.FillRectangle(Brush(0xFF2A2A22), new Rect(body.X, body.Y, body.Width, 2 * s));
                dc.FillRectangle(Brush(0xFF2A2A22), new Rect(body.X, body.Bottom - 2 * s, body.Width, 2 * s));
                dc.DrawEllipse(Brush(0xFF8A9A5A), null, c, 3.5 * s, 3.5 * s);
                dc.FillRectangle(Brush(0xFF8A9A5A), new Rect(c.X - 1 * s, c.Y - 9 * s, 2 * s, 6 * s));
                break;
            }
            case GroundKind.Derota:
            {
                var bse = new Rect(c.X - 10 * s, c.Y - 10 * s, 20 * s, 20 * s);
                dc.FillRectangle(Brush(0xFF5A5E6A), bse);
                dc.FillRectangle(Brush(0xFF8A8E9A), bse.Deflate(2 * s));
                dc.DrawEllipse(Brush(0xFF2A2E3A), null, c, 6 * s, 6 * s);
                dc.DrawEllipse(Brush(t.Reload < 0.4 ? 0xFFFF8A1Eu : 0xFFE83A3Au), null, c, 3 * s, 3 * s);
                break;
            }
            case GroundKind.Sol when t.Revealed:
            {
                // The Sol tower, risen out of the ground: a tall cylinder seen from above and to the side.
                Fill(dc, 0xFF8A8E9C, t.X - 6, y - 14, 12, 16);
                Fill(dc, 0xFFC8CCD8, t.X - 6, y - 14, 4, 16);
                dc.DrawEllipse(Brush(0xFFE8ECF4), null, P(t.X, y - 14), 6 * s, 2.5 * s);
                dc.DrawEllipse(Brush(0xFFE83A3A), null, P(t.X, y - 14), 2 * s, 1 * s);
                break;
            }
            case GroundKind.Flag when t.Revealed:
                DrawSprite(dc, Sprites.Flag, t.X - 5, y - 9);
                break;
        }
    }

    private void DrawMother(DrawingContext dc, Mothership m)
    {
        var c = P(m.Centre);
        double s = Scale, r = Mothership.Radius;
        var pts = Enumerable.Range(0, 8).Select(i =>
        {
            double a = Math.PI / 8 + i * Math.PI / 4;
            return c + new Point(Math.Cos(a) * r * s, Math.Sin(a) * r * s);
        }).ToArray();
        // A shadow on the ground, then the hull in rings.
        Polygon(dc, 0x50000000, pts.Select(p => p + new Point(8 * s, 10 * s)).ToArray());
        Polygon(dc, 0xFF6A6E7C, pts);
        var inner = pts.Select(p => c + (p - c) * 0.78).ToArray();
        Polygon(dc, 0xFF9CA0AE, inner);
        var core = pts.Select(p => c + (p - c) * 0.36).ToArray();
        Polygon(dc, 0xFF5A5E6A, core);
        for (int i = 0; i < 8; i++) dc.DrawLine(Pen(0xFF4A4E5A, 1.5 * s), c + (pts[i] - c) * 0.36, pts[i]);
        for (int i = 0; i < 4; i++)
        {
            var gun = P(m.Centre + Mothership.GunAt[i]);
            dc.DrawEllipse(Brush(0xFF3A3E4A), null, gun, 7 * s, 7 * s);
            dc.DrawEllipse(Brush(m.GunDown[i] ? 0xFF1A120Au : 0xFFE83A3Au), null, gun, 4 * s, 4 * s);
        }
        // The core: it pulses.
        bool lit = (int)(_clock * 6) % 2 == 0;
        dc.DrawEllipse(Brush(lit ? 0xFFFF8A1Eu : 0xFFFFE14Au), null, c, 6 * s, 6 * s);
        dc.DrawEllipse(Brush(0xFFFFFFFF), null, c, 2 * s, 2 * s);
    }

    // ----- the play -------------------------------------------------------------------------------

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var fx in g.Effects.Where(e => e.Kind == EffectKind.Crater))
        {
            double y = g.ScreenY(fx.W!.Value);
            dc.DrawEllipse(Brush(0xFF3A2A1A), null, P(fx.Pos.X, y), 6 * Scale, 4 * Scale);
        }
        foreach (var t in g.Ground)
        {
            if (t.Dead) continue;
            double y = g.ScreenY(t.W);
            if (y < -30 || y > Field.Height + 30) continue;
            DrawGroundTarget(dc, t, y);
        }
        if (g.Mother is { } m && !(m.Destroyed && m.DeathTime > 0.6)) DrawMother(dc, m);

        // The bomb on its way down, getting smaller.
        if (g.Bomb is { } b)
        {
            var to = new Vec(b.TargetX, g.ScreenY(b.TargetW));
            var at = b.From + (to - b.From) * b.T;
            double r = 3 - 1.8 * b.T;
            dc.DrawEllipse(Brush(0xFF2A2A2A), null, P(at), r * Scale, r * Scale);
            dc.DrawEllipse(Brush(0xFFD8D8E0), null, P(at) + new Point(-0.5 * Scale, -0.5 * Scale), r * 0.4 * Scale, r * 0.4 * Scale);
        }

        // The ship's shadow, then the flyers above the ground.
        foreach (var e in g.Air) DrawFlyer(dc, e);
        foreach (var s in g.Shots)
        {
            Fill(dc, 0xFFFFFFFF, s.Pos.X - 3.5, s.Pos.Y - 4, 1, 7);
            Fill(dc, 0xFFFFFFFF, s.Pos.X + 2.5, s.Pos.Y - 4, 1, 7);
        }
        foreach (var s in g.EnemyShots) DrawSprite(dc, Sprites.Spario[(int)(_clock * 12) % 2], s.Pos.X - 2.5, s.Pos.Y - 2.5);

        bool alive = g.Phase != Phase.Dying;
        if (alive)
        {
            // The bomb sight: flashes red over something to bomb.
            var sight = g.Sight;
            bool on = g.SightOnTarget;
            uint col = on && (int)(_clock * 10) % 2 == 0 ? 0xFFFF3D3Du : 0xFFF2F2F2u;
            var sp = P(sight);
            var pen = Pen(col, 2);
            double q = 7 * Scale, k = 3 * Scale;
            foreach (var (dx, dy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            {
                var corner = sp + new Point(dx * q, dy * q);
                dc.DrawLine(pen, corner, corner + new Point(-dx * k, 0));
                dc.DrawLine(pen, corner, corner + new Point(0, -dy * k));
            }
            if (on) dc.DrawEllipse(Brush(col), null, sp, 1.5 * Scale, 1.5 * Scale);

            var ship = g.Ship.Pos;
            DrawSprite(dc, g.Ship.Bank == 0 ? Sprites.Ship : Sprites.ShipBank, ship.X - 7.5, ship.Y - 7.5, flip: g.Ship.Bank < 0, shadow: true);
            DrawSprite(dc, g.Ship.Bank == 0 ? Sprites.Ship : Sprites.ShipBank, ship.X - 7.5, ship.Y - 7.5, flip: g.Ship.Bank < 0);
        }

        foreach (var fx in g.Effects)
        {
            var at = fx.W is { } w ? fx.Pos with { Y = g.ScreenY(w) } : fx.Pos;
            switch (fx.Kind)
            {
                case EffectKind.Blast or EffectKind.BigBlast:
                {
                    double t = fx.Age / fx.Life, big = fx.Kind == EffectKind.BigBlast ? 3 : 1;
                    for (int i = 0; i < 10; i++)
                    {
                        double a = i * Math.PI / 5 + t;
                        double r0 = (2 + t * 10) * big, r1 = (5 + t * 18) * big;
                        uint c = ((i + (int)(_clock * 20)) % 3) switch { 0 => 0xFFFFFFFFu, 1 => 0xFFFFE14Au, _ => 0xFFFF6A1Au };
                        dc.DrawLine(Pen(c, 2 * Scale), P(at.X + Math.Cos(a) * r0, at.Y + Math.Sin(a) * r0), P(at.X + Math.Cos(a) * r1, at.Y + Math.Sin(a) * r1));
                    }
                    break;
                }
                case EffectKind.Score:
                    Text(dc, fx.Text, P(at.X, at.Y - 8 - fx.Age * 10), 26, Yellow, center: true);
                    break;
            }
        }

        switch (g.State)
        {
            case GameState.Paused:
                Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME");
                break;
            case GameState.GameOver:
                Banner(dc, "GAME  OVER", null);
                break;
            case GameState.EnterInitials:
                DrawInitials(dc);
                break;
            default:
                if (g.Phase == Phase.Ready || g.AreaTime < 2.5)
                    Banner(dc, $"AREA {g.Area}", g.State == GameState.Attract ? "DEMONSTRATION" : g.Phase == Phase.Ready ? "READY" : null);
                else if (g.State == GameState.Attract) Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 14), 32, Red, center: true);
                else if (g.Mother is { Destroyed: false, Hover: < 3 } && g.Mother.Y < 84)
                    Text(dc, "ANDOR GENESIS!", P(Field.Width / 2.0, 150), 40, Red, center: true);
                break;
        }
    }

    private void DrawFlyer(DrawingContext dc, AirEnemy e)
    {
        var p = e.Pos;
        switch (e.Kind)
        {
            case AirKind.Toroid: DrawSprite(dc, Sprites.Toroid[(int)(e.Age * 10) % 4], p.X - 6, p.Y - 6); break;
            case AirKind.Torkan: DrawSprite(dc, Sprites.Torkan, p.X - 7, p.Y - 6); break;
            case AirKind.Zoshi: DrawSprite(dc, Sprites.Zoshi[(int)(e.Age * 8) % 2], p.X - 6, p.Y - 6); break;
            case AirKind.Jara: DrawSprite(dc, Sprites.Jara, p.X - 7, p.Y - 5, flip: e.Vel.X < 0); break;
            case AirKind.Bacura:
            {
                // A flat plate turning end over end: its height goes up and down.
                double h = Math.Abs(Math.Cos(e.Age * 3)) * 7 + 1;
                Fill(dc, 0x50000000, p.X - 12 + 6, p.Y - h / 2 + 8, 26, h);
                Fill(dc, 0xFF3A3E4A, p.X - 13, p.Y - h / 2, 26, h);
                Fill(dc, 0xFFA8ACB8, p.X - 13, p.Y - h / 2, 26, Math.Min(1.5, h));
                Fill(dc, 0xFF6A6E7A, p.X - 13, p.Y - h / 2, 2, h);
                break;
            }
        }
    }

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xC0000000, 16, 108, Field.Width - 32, sub is null ? 24 : 36);
        Text(dc, title, P(Field.Width / 2.0, 120), 48, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 135), 26, White, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored, or as its shadow on the ground.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, bool shadow = false, double px = 1)
    {
        if (shadow) { x += 10; y += 12; }
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                uint col = shadow ? 0x58000000 : Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF);
                dc.FillRectangle(Brush(col), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
