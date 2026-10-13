using AVAXevious.Models;

namespace AVAXevious.Engine;

public sealed partial class GameEngine
{
    public const double ShipSpeed = 105;
    public const double ShotSpeed = 340;
    public const double FireEvery = 0.12;
    public const int MaxShots = 6;
    public const double BombSeconds = 0.5;
    /// <summary>How far ahead of the ship the bomb sight is.</summary>
    public const double SightAhead = 72;
    public const double ReadySeconds = 1.6;
    public const double DyingSeconds = 2.4;
    public const double MotherHover = 14;
    public const int SolPoints = 2000;
    public const int GunPoints = 1000;
    public const int CorePoints = 4000;

    public int Area { get; private set; } = 1;
    /// <summary>How far the current area has scrolled.</summary>
    public double Distance { get; private set; }
    public AreaPlan Plan { get; private set; } = null!;
    public Ship Ship { get; } = new();
    public List<Shot> Shots { get; } = [];
    public Bomb? Bomb { get; private set; }
    public List<AirEnemy> Air { get; } = [];
    public List<GroundTarget> Ground { get; } = [];
    public List<EnemyShot> EnemyShots { get; } = [];
    public Mothership? Mother { get; private set; }
    /// <summary>Seconds since the area began, for its banner.</summary>
    public double AreaTime { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private int _waveIndex;
    private bool _motherDone;
    private double _fireClock;
    private bool _bombPrev;
    private bool _calm;

    public static int Points(AirKind k) => k switch
    {
        AirKind.Toroid => 30, AirKind.Torkan => 50, AirKind.Zoshi => 70, AirKind.Jara => 150, _ => 0,
    };

    public static int Points(GroundKind k) => k switch
    {
        GroundKind.Barra => 100, GroundKind.Zolbak => 600, GroundKind.Logram => 300, GroundKind.Grobda => 200,
        GroundKind.Derota => 1000, GroundKind.Sol => SolPoints, _ => 0,
    };

    public double EnemyShotSpeed => Math.Min(150, 92 + 5 * Area);

    /// <summary>Where on the screen a place on the ground is now.</summary>
    public double ScreenY(double w) => Distance - w;

    public Vec Sight => Ship.Pos + new Vec(0, -SightAhead);

    /// <summary>
    /// The sight is over something a bomb would hit (it flashes then), hidden Sol towers included,
    /// as on the arcade.
    /// </summary>
    public bool SightOnTarget
    {
        get
        {
            var s = Sight;
            foreach (var g in Ground)
                if (!g.Dead && !g.Wrecked && g.Kind != GroundKind.Flag && Math.Abs(g.X - s.X) < 10 && Math.Abs(ScreenY(g.W) - s.Y) < 10) return true;
            if (Mother is { Destroyed: false } m)
            {
                if ((m.Centre - s).Length < 12) return true;
                for (int i = 0; i < 4; i++)
                    if (!m.GunDown[i] && (m.Centre + Mothership.GunAt[i] - s).Length < 10) return true;
            }
            return false;
        }
    }

    // ----- areas ----------------------------------------------------------------------------------

    private void StartArea(int area, double distance)
    {
        Area = area;
        Plan = AreaPlan.Build(area);
        Distance = distance;
        LoadGround();
        Air.Clear();
        Shots.Clear();
        EnemyShots.Clear();
        Bomb = null;
        Mother = null;
        _motherDone = Plan.MotherAt is not { } at || distance > at;
        _waveIndex = Plan.Waves.FindIndex(w => w.W >= distance);
        if (_waveIndex < 0) _waveIndex = Plan.Waves.Count;
        Ship.Pos = new Vec(Field.Width / 2.0, 214);
        Ship.Bank = 0;
        AreaTime = 0;
        SetPhase(Phase.Ready);
        Music(false);
    }

    private void LoadGround()
    {
        Ground.Clear();
        if (_calm) return;
        foreach (var g in Plan.Ground)
            Ground.Add(new GroundTarget { Kind = g.Kind, X = g.X, W = g.W, Reload = g.Reload });
    }

    /// <summary>On into the next area without a pause.</summary>
    private void NextArea()
    {
        Area++;
        Plan = AreaPlan.Build(Area);
        Distance -= Field.AreaLength;
        LoadGround();
        _waveIndex = 0;
        _motherDone = Plan.MotherAt is null;
        AreaTime = 0;
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        AreaTime += dt;
        Distance += Field.ScrollSpeed * dt;
        if (Distance >= Field.AreaLength) NextArea();

        switch (Phase)
        {
            case Phase.Ready:
                MoveShip(dt, c);
                // Anything due meanwhile is let go by.
                while (_waveIndex < Plan.Waves.Count && Plan.Waves[_waveIndex].W <= Distance) _waveIndex++;
                if (PhaseTime >= ReadySeconds) { SetPhase(Phase.Playing); Music(true); }
                break;
            case Phase.Playing:
                MoveShip(dt, c);
                Fire(dt, c);
                SpawnWaves();
                break;
            case Phase.Dying:
                if (PhaseTime >= DyingSeconds) { LoseLife(); return; }
                break;
        }
        _bombPrev = c.Bomb;
        UpdateShots(dt);
        UpdateBomb(dt);
        UpdateAir(dt);
        UpdateGround(dt);
        UpdateMother(dt);
        foreach (var s in EnemyShots)
        {
            s.Pos += s.Vel * dt;
            if (s.Pos.Y < -20 || s.Pos.Y > Field.Height + 20 || s.Pos.X < -20 || s.Pos.X > Field.Width + 20) s.Dead = true;
        }
        EnemyShots.RemoveAll(s => s.Dead);
        if (Phase == Phase.Playing) Collide();
    }

    private void LoseLife()
    {
        Lives--;
        if (Lives <= 0) { GameOver(); return; }
        StartArea(Area, Distance >= Field.Checkpoint ? Field.Checkpoint : 0);
    }

    private void Die(string why)
    {
        LastDeath = why;
        LossLog.Add($"area {Area} at {Distance:0}: {why}");
        Effects.Add(new Effect { Kind = EffectKind.BigBlast, Pos = Ship.Pos, Life = 1.6 });
        Bomb = null;
        Music(false);
        Play(Sfx.Death);
        SetPhase(Phase.Dying);
    }

    // ----- the ship -------------------------------------------------------------------------------

    private void MoveShip(double dt, Controls c)
    {
        double dx = Math.Clamp(c.Dx, -1, 1), dy = Math.Clamp(c.Dy, -1, 1);
        double len = Math.Sqrt(dx * dx + dy * dy);
        if (len > 1) { dx /= len; dy /= len; }
        var p = Ship.Pos + new Vec(dx, dy) * (ShipSpeed * dt);
        Ship.Pos = new Vec(Math.Clamp(p.X, 10, Field.Width - 10), Math.Clamp(p.Y, 100, Field.Height - 14));
        Ship.Bank = dx < -0.3 ? -1 : dx > 0.3 ? 1 : 0;
    }

    private void Fire(double dt, Controls c)
    {
        _fireClock -= dt;
        if (c.Fire && _fireClock <= 0 && Shots.Count < MaxShots)
        {
            _fireClock = FireEvery;
            Shots.Add(new Shot { Pos = Ship.Pos + new Vec(0, -10) });
            Play(Sfx.Zap);
        }
        // One bomb at a time, dropped on the press.
        if (c.Bomb && !_bombPrev && Bomb is null)
        {
            var s = Sight;
            Bomb = new Bomb { From = Ship.Pos, TargetX = s.X, TargetW = Distance - s.Y, TargetY = s.Y };
            Play(Sfx.Drop);
        }
    }

    private void UpdateShots(double dt)
    {
        foreach (var s in Shots)
        {
            s.Pos += new Vec(0, -ShotSpeed * dt);
            if (s.Pos.Y < -10) { s.Dead = true; continue; }
            foreach (var e in Air)
            {
                if (e.Dead) continue;
                if (e.Kind == AirKind.Bacura)
                {
                    if (Math.Abs(e.Pos.X - s.Pos.X) < 13 && Math.Abs(e.Pos.Y - s.Pos.Y) < 6)
                    {
                        s.Dead = true;
                        Play(Sfx.Clang);
                        break;
                    }
                    continue;
                }
                if ((e.Pos - s.Pos).Length > 9) continue;
                s.Dead = true;
                e.Dead = true;
                AddScore(Points(e.Kind));
                Popup(e.Pos, Points(e.Kind));
                Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = e.Pos, Life = 0.5 });
                Play(Sfx.AirHit);
                break;
            }
        }
        Shots.RemoveAll(s => s.Dead);
    }

    /// <summary>The bomb lands where the sight was: anything on the ground there is hit.</summary>
    private void UpdateBomb(double dt)
    {
        if (Bomb is not { } b) return;
        b.T += dt / BombSeconds;
        if (b.T < 1) return;
        Bomb = null;
        var at = new Vec(b.TargetX, ScreenY(b.TargetW));
        Effects.Add(new Effect { Kind = EffectKind.Crater, Pos = at, W = b.TargetW, Life = 3 });
        Effects.Add(new Effect { Kind = EffectKind.Blast, Pos = at, W = b.TargetW, Life = 0.45 });
        Play(Sfx.BombHit);
        foreach (var g in Ground)
        {
            if (g.Dead || g.Wrecked || Math.Abs(g.X - b.TargetX) >= 11 || Math.Abs(g.W - b.TargetW) >= 11) continue;
            switch (g.Kind)
            {
                case GroundKind.Sol when !g.Revealed:
                    g.Revealed = true;
                    AddScore(SolPoints);
                    Popup(at, SolPoints, b.TargetW);
                    Play(Sfx.Sol);
                    break;
                case GroundKind.Flag:
                    if (!g.Revealed) { g.Revealed = true; Play(Sfx.Sol); }
                    break;
                default:
                    g.Wrecked = true;
                    AddScore(Points(g.Kind));
                    Popup(at, Points(g.Kind), b.TargetW);
                    break;
            }
        }
        if (Mother is { Destroyed: false } m)
        {
            at = new Vec(b.TargetX, b.TargetY);
            if ((m.Centre - at).Length < 12)
            {
                m.Destroyed = true;
                AddScore(CorePoints);
                Popup(m.Centre, CorePoints);
                Effects.Add(new Effect { Kind = EffectKind.BigBlast, Pos = m.Centre, Life = 2 });
                Play(Sfx.MotherDown);
                return;
            }
            for (int i = 0; i < 4; i++)
            {
                if (m.GunDown[i] || (m.Centre + Mothership.GunAt[i] - at).Length >= 10) continue;
                m.GunDown[i] = true;
                AddScore(GunPoints);
                Popup(m.Centre + Mothership.GunAt[i], GunPoints);
                Play(Sfx.GunDown);
            }
        }
    }

    // ----- flyers ---------------------------------------------------------------------------------

    private void SpawnWaves()
    {
        if (_calm) return;
        while (_waveIndex < Plan.Waves.Count && Plan.Waves[_waveIndex].W <= Distance)
        {
            var w = Plan.Waves[_waveIndex++];
            for (int i = 0; i < w.Count; i++) Air.Add(MakeFlyer(w.Kind, w.X, i, w.Count));
        }
    }

    private AirEnemy MakeFlyer(AirKind kind, double x, int i, int count)
    {
        double clampX(double v) => Math.Clamp(v, 12, Field.Width - 12);
        double speed = 1 + 0.03 * (Area - 1);
        return kind switch
        {
            AirKind.Toroid => new AirEnemy { Kind = kind, Pos = new Vec(clampX(x + (i - count / 2.0) * 14), -10 - i * 16), Vel = new Vec(0, 105 * speed), Phase = i },
            AirKind.Torkan => new AirEnemy { Kind = kind, Pos = new Vec(clampX(x + i * 46 - (count - 1) * 23), -10), Vel = new Vec(0, 110), Stop = 50 + _rng.NextDouble() * 60 },
            AirKind.Zoshi => new AirEnemy { Kind = kind, Pos = new Vec(clampX(x + i * 18), -10 - i * 10), Vel = new Vec(0, 80), Reload = 1.5 + _rng.NextDouble(), Phase = i },
            AirKind.Jara => new AirEnemy { Kind = kind, Pos = new Vec(clampX(x), -10 - i * 26), BaseX = Math.Clamp(x, 60, Field.Width - 60), Phase = i * 0.9 },
            _ => new AirEnemy { Kind = kind, Pos = new Vec(Math.Clamp(x - (count - 1) * 15, 16, Field.Width - 16 - (count - 1) * 30) + i * 30, -16), Vel = new Vec(0, Field.ScrollSpeed + 14) },
        };
    }

    private void FireAt(Vec from, double spread = 0)
    {
        if (Phase != Phase.Playing) return;
        var to = (Ship.Pos - from).Normal;
        double a = Math.Atan2(to.Y, to.X) + spread;
        EnemyShots.Add(new EnemyShot { Pos = from, Vel = new Vec(Math.Cos(a), Math.Sin(a)) * EnemyShotSpeed });
        Play(Sfx.EnemyFire);
    }

    private void UpdateAir(double dt)
    {
        var ship = Ship.Pos;
        foreach (var e in Air)
        {
            if (e.Dead) continue;
            e.Age += dt;
            switch (e.Kind)
            {
                case AirKind.Toroid:
                    // Dives in, then turns tail, sometimes with a parting shot.
                    if (!e.Fleeing && (e.Pos.Y > ship.Y - 85 || e.Age > 1.8))
                    {
                        e.Fleeing = true;
                        e.Vel = new Vec(e.Pos.X < ship.X ? -95 : 95, 10);
                        if (_rng.NextDouble() < 0.15 + 0.04 * Area) FireAt(e.Pos);
                    }
                    if (e.Fleeing) e.Vel += new Vec(0, -170 * dt);
                    break;
                case AirKind.Torkan:
                    if (!e.Fleeing && e.Vel.Y > 0 && e.Pos.Y >= e.Stop) { e.Vel = new Vec(0, 0); e.Reload = 0.45; }
                    if (!e.Fleeing && e.Vel.Y == 0)
                    {
                        e.Reload -= dt;
                        if (e.Reload <= 0)
                        {
                            FireAt(e.Pos);
                            e.Fleeing = true;
                            e.Vel = new Vec(0, -40);
                        }
                    }
                    if (e.Fleeing) e.Vel += new Vec(0, -160 * dt);
                    break;
                case AirKind.Zoshi:
                {
                    var want = e.Age > 7 ? new Vec(0, 170) : (ship - e.Pos).Normal * 112;
                    e.Vel += (want - e.Vel) * Math.Min(1, 2.2 * dt);
                    e.Reload -= dt;
                    if (e.Reload <= 0) { e.Reload = 2 + _rng.NextDouble() * 1.5; FireAt(e.Pos); }
                    break;
                }
                case AirKind.Jara:
                {
                    double nx = e.BaseX + Math.Sin(e.Age * 3 + e.Phase) * 56;
                    e.Vel = new Vec((nx - e.Pos.X) / dt, 92);
                    break;
                }
            }
            e.Pos += e.Vel * dt;
            if (e.Pos.Y > Field.Height + 30 || (e.Age > 1 && e.Pos.Y < -40) || e.Pos.X < -40 || e.Pos.X > Field.Width + 40) e.Dead = true;
        }
        Air.RemoveAll(e => e.Dead);
    }

    // ----- the ground -----------------------------------------------------------------------------

    private void UpdateGround(double dt)
    {
        var sight = Sight;
        foreach (var g in Ground)
        {
            if (g.Dead || g.Wrecked) continue;
            double y = ScreenY(g.W);
            if (y < -30 || y > Field.Height + 30) continue;
            switch (g.Kind)
            {
                case GroundKind.Logram when y is > 10 and < 200:
                    g.Reload -= dt;
                    if (g.Reload <= 0) { g.Reload = Math.Max(1.4, 2.7 - 0.08 * Area); FireAt(new Vec(g.X, y)); }
                    break;
                case GroundKind.Derota when y is > 10 and < 210:
                    g.Reload -= dt;
                    if (g.Reload <= 0)
                    {
                        g.Reload = 2.2;
                        foreach (double s in new[] { -0.22, 0, 0.22 }) FireAt(new Vec(g.X, y), s);
                    }
                    break;
                case GroundKind.Sol when g.Revealed && y is > 10 and < 200:
                    g.Reload -= dt;
                    if (g.Reload <= 0) { g.Reload = 2.4; FireAt(new Vec(g.X, y)); }
                    break;
                case GroundKind.Grobda:
                    // The tank runs for it when the sight comes over it.
                    if (g.Vx == 0 && Math.Abs(g.X - sight.X) < 26 && Math.Abs(y - sight.Y) < 26)
                        g.Vx = g.X < sight.X ? -48 : 48;
                    g.X = Math.Clamp(g.X + g.Vx * dt, 10, Field.Width - 10);
                    break;
            }
        }
    }

    private void UpdateMother(double dt)
    {
        if (Mother is null && !_motherDone && Plan.MotherAt is { } at && Distance >= at && Phase == Phase.Playing)
        {
            Mother = new Mothership();
            _motherDone = true;
            Play(Sfx.MotherArrives);
        }
        if (Mother is not { } m) return;
        if (m.Destroyed)
        {
            m.DeathTime += dt;
            m.Y += Field.ScrollSpeed * dt;
            if (m.DeathTime > 2.5) Mother = null;
            return;
        }
        if (m.Hover < MotherHover)
        {
            if (m.Y < 84) m.Y += (Field.ScrollSpeed + 26) * dt;
            else m.Hover += dt;
        }
        else m.Y += Field.ScrollSpeed * 1.6 * dt;
        if (m.Y > Field.Height + 80) { Mother = null; return; }
        m.Reload -= dt;
        if (m.Reload <= 0)
        {
            m.Reload = Math.Max(0.7, 1.2 - 0.03 * Area);
            for (int i = 0; i < 4; i++)
            {
                var gun = m.Centre + Mothership.GunAt[i];
                if (!m.GunDown[i] && gun.Y > 0 && gun.Y < Field.Height - 20) FireAt(gun);
            }
        }
    }

    // ----- hits on the ship -----------------------------------------------------------------------

    private void Collide()
    {
        var p = Ship.Pos;
        foreach (var e in Air)
        {
            if (e.Dead) continue;
            bool hit = e.Kind == AirKind.Bacura
                ? Math.Abs(e.Pos.X - p.X) < 14 && Math.Abs(e.Pos.Y - p.Y) < 8
                : (e.Pos - p).Length < 10;
            if (hit) { Die($"flew into a {e.Kind.ToString().ToLowerInvariant()}"); return; }
        }
        foreach (var s in EnemyShots)
            if ((s.Pos - p).Length < 5.5) { Die("hit by a spario"); return; }
        // A flag dug out of the ground: fly over it for an extra ship.
        foreach (var g in Ground)
        {
            if (g.Kind != GroundKind.Flag || !g.Revealed || g.Dead) continue;
            if ((new Vec(g.X, ScreenY(g.W)) - p).Length < 12)
            {
                g.Dead = true;
                Lives++;
                Effects.Add(new Effect { Kind = EffectKind.Score, Pos = p, Life = 1.4, Text = "1UP" });
                Play(Sfx.Flag);
            }
        }
    }

    // ----- for the tests and screenshots ----------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Tick(default);
    }

    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        Music(true);
    }

    internal void JumpTo(int area, double distance = 0)
    {
        StartArea(area, distance);
        Go();
    }

    /// <summary>Nothing new arrives, and nothing on the ground.</summary>
    internal void Calm()
    {
        _calm = true;
        Ground.Clear();
        Air.Clear();
        _motherDone = true;
    }

    internal AirEnemy PutAir(AirKind kind, Vec at)
    {
        var e = MakeFlyer(kind, at.X, 0, 1);
        e.Pos = at;
        Air.Add(e);
        return e;
    }

    internal GroundTarget PutGround(GroundKind kind, double x, double screenY)
    {
        var g = new GroundTarget { Kind = kind, X = x, W = Distance - screenY, Reload = 1 };
        Ground.Add(g);
        return g;
    }

    internal Mothership SummonMother(double y)
    {
        Mother = new Mothership { Y = y };
        _motherDone = true;
        return Mother;
    }

    internal void PutShip(Vec at) => Ship.Pos = at;
}
