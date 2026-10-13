using AVAXevious.Models;

namespace AVAXevious.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It lines up under the nearest flyer it can
/// shoot (leading it a little), or brings the bomb sight over something on the ground; it drops a bomb
/// whenever the sight flashes, and keeps the zapper going. Shots and flyers close by push it away,
/// judged by where they'll be a moment from now; a row of Bacura it steers round in good time.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private double _wanderX = Field.Width / 2.0;
    private double _wanderClock;
    private bool _bombHeld;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase == Phase.Dying) return default;
        var ship = g.Ship.Pos;
        _wanderClock -= Field.Step;
        if (_wanderClock <= 0)
        {
            _wanderClock = 1.5 + _rng.NextDouble() * 2;
            _wanderX = 40 + _rng.NextDouble() * (Field.Width - 80);
        }

        double tx = _wanderX, ty = 205;
        var prey = g.Air
            .Where(e => e.Kind != AirKind.Bacura && e.Pos.Y > 0 && e.Pos.Y < ship.Y - 20)
            .OrderBy(e => Math.Abs(e.Pos.X - ship.X) + (ship.Y - e.Pos.Y) * 0.4)
            .FirstOrDefault();
        var flag = g.Ground.FirstOrDefault(x => x.Kind == GroundKind.Flag && x.Revealed && !x.Dead && g.ScreenY(x.W) is > 40 and < 240);
        var mark = g.Ground
            .Where(x => !x.Dead && !x.Wrecked && x.Kind is not (GroundKind.Flag or GroundKind.Sol) && g.ScreenY(x.W) is > 20 and < 150)
            .OrderBy(x => Math.Abs(x.X - ship.X))
            .FirstOrDefault();
        if (flag is not null) { tx = flag.X; ty = g.ScreenY(flag.W); }
        else if (prey is not null)
        {
            double lead = (ship.Y - prey.Pos.Y) / GameEngine.ShotSpeed;
            tx = prey.Pos.X + prey.Vel.X * lead;
        }
        else if (g.Mother is { Destroyed: false } m && m.Y > 30)
        {
            tx = m.X;
            ty = m.Y + GameEngine.SightAhead;
        }
        else if (mark is not null)
        {
            tx = mark.X;
            ty = g.ScreenY(mark.W) + GameEngine.SightAhead + 6;
        }
        ty = Math.Clamp(ty, 150, 236);

        // A row of Bacura coming down: get out from under it in good time, to the nearest clear spot.
        var plates = g.Air.Where(e => e.Kind == AirKind.Bacura && e.Pos.Y > ship.Y - 140 && e.Pos.Y < ship.Y + 12).ToList();
        bool Blocked(double x) => plates.Any(p => Math.Abs(p.Pos.X - x) < 24);
        if (plates.Count > 0 && (Blocked(ship.X) || Blocked(tx)))
        {
            double best = ship.X, bestDist = double.MaxValue;
            for (double x = 12; x <= Field.Width - 12; x += 4)
            {
                if (Blocked(x)) continue;
                double d = Math.Abs(x - ship.X);
                if (d < bestDist) { bestDist = d; best = x; }
            }
            tx = best;
        }

        // Keep away from what's coming.
        var push = new Vec(0, 0);
        foreach (var s in g.EnemyShots)
            foreach (double t in new[] { 0.0, 0.2, 0.4 })
            {
                var d = ship - (s.Pos + s.Vel * t);
                if (d.Length < 40) push += d.Normal * ((40 - d.Length) * 4);
            }
        foreach (var e in g.Air)
            foreach (double t in new[] { 0.0, 0.25 })
            {
                var d = ship - (e.Pos + e.Vel * t);
                double r = e.Kind == AirKind.Bacura ? 46 : 38;
                if (d.Length < r) push += d.Normal * ((r - d.Length) * 4);
            }
        double dx = (tx - ship.X) / 12 + push.X / 25;
        double dy = (ty - ship.Y) / 20 + push.Y / 25;

        bool wantBomb = g.SightOnTarget && g.Bomb is null;
        _bombHeld = wantBomb && !_bombHeld;
        return new Controls(Math.Clamp(dx, -1, 1), Math.Clamp(dy, -1, 1), Fire: true, Bomb: _bombHeld);
    }
}
