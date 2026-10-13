using Avalonia;
using Avalonia.Media;
using AVAXevious.Engine;
using AVAXevious.Models;

namespace AVAXevious.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>The panels either side: scores and spare ships on the left; the area and keys on the right.</summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;

        Text(dc, "PLAYER 1", new Point(lx, 60), 30, Orange, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 92), 34, White, center: true);
        Text(dc, "HI-SCORE", new Point(lx, 160), 30, Orange, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(lx, 192), 34, White, center: true);

        if (playing)
        {
            int spare = g.Lives - (g.Phase == Phase.Dying ? 0 : 1);
            Text(dc, "SHIPS", new Point(lx, 270), 28, Dim, center: true);
            for (int i = 0; i < Math.Min(spare, 6); i++) DrawPanelSprite(dc, Sprites.Ship, lx - 64 + (i % 4) * 32, 296 + i / 4 * 34);
            Text(dc, "AREA", new Point(rx, 60), 30, Orange, center: true);
            Text(dc, g.Area.ToString(), new Point(rx, 92), 34, White, center: true);
            // How far through the area: a bar, with the halfway mark where a lost ship starts again.
            Text(dc, "PROGRESS", new Point(rx, 160), 26, Orange, center: true);
            var bar = new Rect(rx - 60, 180, 120, 12);
            dc.DrawRectangle(null, Pen(0xFF5A5A7A, 1.5), bar);
            dc.FillRectangle(Brush(0xFF4FD7FF), new Rect(bar.X + 2, bar.Y + 2, (bar.Width - 4) * Math.Clamp(g.Distance / Field.AreaLength, 0, 1), bar.Height - 4));
            dc.FillRectangle(Brush(0xFFFFE14A), new Rect(bar.X + bar.Width / 2 - 1, bar.Y - 3, 2, bar.Height + 6));
            if (AreaPlan.HasMothership(g.Area)) Text(dc, "MOTHERSHIP AHEAD", new Point(rx, 212), 20, Dim, center: true);
        }
        Text(dc, "AVA XEVIOUS", new Point(rx, 500), 24, Dim, center: true);
        Text(dc, "Arrows move", new Point(rx, 530), 20, Dim, center: true);
        Text(dc, "Z or Space: zapper", new Point(rx, 550), 20, Dim, center: true);
        Text(dc, "X or Shift: bomb", new Point(rx, 570), 20, Dim, center: true);
        Text(dc, "Enter starts", new Point(rx, 600), 20, Dim, center: true);
        Text(dc, "M mute  P pause", new Point(rx, 630), 20, Dim, center: true);
        Text(dc, "Ctrl+S screenshot", new Point(rx, 650), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 1.8;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xD0000010), new Rect(P(8, top), P(Field.Width - 8, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 8, 248);
        Text(dc, "AVA", P(cx, 22), 60, Orange, center: true);
        Text(dc, "XEVIOUS", P(cx, 40), 96, Orange, center: true);
        Text(dc, "AFTER THE 1982 ARCADE CLASSIC", P(cx, 58), 26, Dim, center: true);
        Text(dc, "ZAP WHAT FLIES, BOMB WHAT'S ON THE GROUND", P(cx, 70), 26, White, center: true);

        double y = 85;
        void Row(Action<double, double> icon, string name, string points)
        {
            // The icons a little smaller than life, so the bigger ones fit their rows.
            using (dc.PushTransform(Matrix.CreateTranslation(-P(26, y).X, -P(26, y).Y) * Matrix.CreateScale(0.72, 0.72) * Matrix.CreateTranslation(P(26, y).X, P(26, y).Y)))
                icon(26, y);
            Text(dc, name, P(42, y), 24, White);
            Text(dc, points, P(214, y), 24, Yellow, right: true);
            y += 14;
        }
        Row((x, yy) => DrawSprite(dc, Sprites.Toroid[(int)(_clock * 10) % 4], x - 6, yy - 6), "TOROID", "30");
        Row((x, yy) => DrawSprite(dc, Sprites.Torkan, x - 7, yy - 6), "TORKAN", "50");
        Row((x, yy) => DrawSprite(dc, Sprites.Zoshi[(int)(_clock * 8) % 2], x - 6, yy - 6), "ZOSHI", "70");
        Row((x, yy) => DrawSprite(dc, Sprites.Jara, x - 7, yy - 5), "JARA", "150");
        Row((x, yy) => { Fill(dc, 0xFF3A3E4A, x - 10, yy - 3, 20, 6); Fill(dc, 0xFFA8ACB8, x - 10, yy - 3, 20, 1.5); }, "BACURA: SHOTS BOUNCE OFF", "");
        Row((x, yy) => DrawGroundTarget(dc, new GroundTarget { Kind = GroundKind.Barra, X = x }, yy), "BARRA", "100");
        Row((x, yy) => DrawGroundTarget(dc, new GroundTarget { Kind = GroundKind.Logram, X = x, Reload = 1 }, yy), "LOGRAM  /  ZOLBAK", "300 / 600");
        Row((x, yy) => DrawGroundTarget(dc, new GroundTarget { Kind = GroundKind.Derota, X = x, Reload = 1 }, yy), "DEROTA", "1000");
        Row((x, yy) => DrawGroundTarget(dc, new GroundTarget { Kind = GroundKind.Sol, X = x, Revealed = true }, yy + 6), "SOL: HIDDEN. THE SIGHT KNOWS", "2000");
        Row((x, yy) => dc.DrawEllipse(Brush(0xFFFF8A1E), null, P(x, yy), 6 * Scale, 6 * Scale), "ANDOR GENESIS CORE", "4000");
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 230), 30, Yellow, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 242), 22, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 10, 236);
        Text(dc, "TOP PILOTS", P(cx, 30), 60, Orange, center: true);
        Text(dc, "RANK    SCORE   NAME  AREA", P(cx, 50), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Orange : White;
            Text(dc, $"{i + 1,2}.  {e.Score,8}   {e.Initials}   {e.Stage,3}", P(cx, 66 + i * 14), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 220), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(16, 92), P(208, 170)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 102), 44, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 116), 40, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 12 + i * 12, 138);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 70, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4)
                dc.FillRectangle(Yellow, new Rect(at.X - 16, at.Y + 32, 32, 5));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 162), 22, Dim, center: true);
    }
}
