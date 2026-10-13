using AVAXevious.Engine;
using AVAXevious.Models;

namespace AVAXevious.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xevious-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>An area under way with nothing coming and nothing on the ground.</summary>
    private GameEngine Calm(int area = 1, double distance = 300, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(area, distance);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Tick(c);
    }

    private static readonly Controls FireC = new(0, 0, Fire: true);
    private static readonly Controls BombC = new(0, 0, Bomb: true);

    [Fact]
    public void The_ground_scrolls_and_areas_follow_one_another()
    {
        var g = Calm(distance: Field.AreaLength - 30);
        double d = g.Distance;
        Steps(g, 30);
        Assert.Equal(d + Field.ScrollSpeed * 0.5, g.Distance, 3);
        Steps(g, 60);
        Assert.Equal(2, g.Area);
        Assert.InRange(g.Distance, 0, 60);
        // Something on the ground comes down the screen with it.
        var t = g.PutGround(GroundKind.Barra, 100, 50);
        Steps(g, 60);
        Assert.Equal(50 + Field.ScrollSpeed, g.ScreenY(t.W), 3);
    }

    [Fact]
    public void Every_area_has_ground_targets_flyers_and_a_hidden_sol_and_every_other_one_a_mothership()
    {
        for (int a = 1; a <= Field.Areas; a++)
        {
            var p = AreaPlan.Build(a);
            Assert.True(p.Ground.Count(x => x.Kind != GroundKind.Sol && x.Kind != GroundKind.Flag) > 15, $"area {a}");
            Assert.Contains(p.Ground, x => x.Kind == GroundKind.Sol);
            Assert.True(p.Waves.Count > 15);
            Assert.Equal(a % 2 == 0, p.MotherAt is not null);
            Assert.All(p.Ground, x => Assert.InRange(x.X, 0, Field.Width));
        }
        Assert.Contains(AreaPlan.Build(1).Ground, x => x.Kind == GroundKind.Flag);
        Assert.Equal(AreaPlan.Build(3).Waves, AreaPlan.Build(3).Waves);
    }

    [Fact]
    public void The_ship_flies_within_its_part_of_the_screen()
    {
        var g = Calm();
        Steps(g, 240, new Controls(-1, -1));
        Assert.Equal(10, g.Ship.Pos.X, 3);
        Assert.Equal(100, g.Ship.Pos.Y, 3);
        Assert.Equal(-1, g.Ship.Bank);
        Steps(g, 240, new Controls(1, 1));
        Assert.Equal(Field.Width - 10, g.Ship.Pos.X, 3);
        Assert.Equal(Field.Height - 14, g.Ship.Pos.Y, 3);
    }

    [Fact]
    public void The_zapper_shoots_down_flyers_but_bounces_off_a_bacura()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(112, 220));
        var toroid = g.PutAir(AirKind.Toroid, new Vec(112, 60));
        toroid.Vel = new Vec(0, 0);
        toroid.Fleeing = true;
        for (int i = 0; i < 60 && !toroid.Dead; i++) { toroid.Vel = new Vec(0, 0); g.Tick(FireC); }
        Assert.True(toroid.Dead);
        Assert.Equal(GameEngine.Points(AirKind.Toroid), g.Score);
        Assert.Contains(Sfx.Zap, heard);
        Assert.Contains(Sfx.AirHit, heard);

        var plate = g.PutAir(AirKind.Bacura, new Vec(112, 90));
        for (int i = 0; i < 40; i++) { plate.Pos = new Vec(112, 90); g.Tick(FireC); }
        Assert.False(plate.Dead);
        Assert.Contains(Sfx.Clang, heard);
        Assert.Equal(GameEngine.Points(AirKind.Toroid), g.Score);
    }

    [Fact]
    public void Shots_go_over_ground_targets_and_bombs_under_flyers()
    {
        var g = Calm();
        g.PutShip(new Vec(112, 200));
        var barra = g.PutGround(GroundKind.Barra, 112, 100);
        Steps(g, 60, FireC);
        Steps(g, 60);
        Assert.False(barra.Wrecked);
        var torkan = g.PutAir(AirKind.Torkan, new Vec(112, 128));
        torkan.Vel = new Vec(0, 0);
        g.Tick(BombC);
        Assert.NotNull(g.Bomb);
        Steps(g, 40);
        Assert.Null(g.Bomb);
        Assert.False(torkan.Dead);
    }

    [Fact]
    public void A_bomb_lands_where_the_sight_was_and_the_sight_flashes_over_a_target()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(80, 200));
        var sight = g.Sight;
        Assert.Equal(200 - GameEngine.SightAhead, sight.Y);
        var logram = g.PutGround(GroundKind.Logram, 80, sight.Y);
        Assert.True(g.SightOnTarget);
        g.Tick(BombC);
        Assert.Contains(Sfx.Drop, heard);
        // Holding the button doesn't drop another.
        Steps(g, 10, BombC);
        Steps(g, 30);
        Assert.True(logram.Wrecked);
        Assert.Equal(GameEngine.Points(GroundKind.Logram), g.Score);
        Assert.Contains(Sfx.BombHit, heard);
        Assert.False(g.SightOnTarget);
        // A miss: nothing.
        g.PutGround(GroundKind.Barra, 160, g.Sight.Y);
        g.Tick(BombC);
        Steps(g, 40);
        Assert.Equal(GameEngine.Points(GroundKind.Logram), g.Score);
    }

    [Fact]
    public void A_hidden_sol_shows_in_the_sight_and_rises_when_bombed_then_can_be_destroyed()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(112, 220));
        var sol = g.PutGround(GroundKind.Sol, 112, g.Sight.Y);
        Assert.True(g.SightOnTarget);
        g.Tick(BombC);
        Steps(g, 40);
        Assert.True(sol.Revealed);
        Assert.False(sol.Wrecked);
        Assert.Equal(GameEngine.SolPoints, g.Score);
        Assert.Contains(Sfx.Sol, heard);
        // Bring the sight back over it and bomb it again.
        g.PutShip(new Vec(112, g.ScreenY(sol.W) + GameEngine.SightAhead));
        g.Tick(BombC);
        Steps(g, 40);
        Assert.True(sol.Wrecked);
        Assert.Equal(2 * GameEngine.SolPoints, g.Score);
    }

    [Fact]
    public void A_flag_dug_up_and_flown_over_is_an_extra_ship()
    {
        var g = Calm();
        g.PutShip(new Vec(112, 220));
        var flag = g.PutGround(GroundKind.Flag, 112, g.Sight.Y);
        Assert.False(g.SightOnTarget); // it doesn't give itself away
        g.Tick(BombC);
        Steps(g, 40);
        Assert.True(flag.Revealed);
        int lives = g.Lives;
        for (int i = 0; i < 240 && !flag.Dead; i++)
        {
            var at = new Vec(flag.X, g.ScreenY(flag.W));
            var d = at - g.Ship.Pos;
            g.Tick(new Controls(Math.Clamp(d.X / 5, -1, 1), Math.Clamp(d.Y / 5, -1, 1)));
        }
        Assert.True(flag.Dead);
        Assert.Equal(lives + 1, g.Lives);
    }

    [Fact]
    public void A_grobda_runs_from_the_sight()
    {
        var g = Calm();
        g.PutShip(new Vec(100, 220));
        var tank = g.PutGround(GroundKind.Grobda, 108, g.Sight.Y);
        g.Tick(default);
        Assert.True(tank.Vx > 0);
        double x = tank.X;
        Steps(g, 30);
        Assert.True(tank.X > x);
    }

    [Fact]
    public void Flyers_behave_each_in_their_own_way()
    {
        var g = Calm();
        g.PutShip(new Vec(112, 220));
        var toroid = g.PutAir(AirKind.Toroid, new Vec(60, 0));
        var torkan = g.PutAir(AirKind.Torkan, new Vec(170, 0));
        var zoshi = g.PutAir(AirKind.Zoshi, new Vec(20, 30));
        double zoshiStart = (zoshi.Pos - g.Ship.Pos).Length;
        bool torkanStopped = false;
        for (int i = 0; i < 90; i++)
        {
            g.Tick(default);
            if (!torkan.Dead && torkan.Vel.Length < 1e-6) torkanStopped = true;
        }
        Assert.True(toroid.Fleeing, "the toroid turned tail");
        Assert.True(torkanStopped, "the torkan stopped to fire");
        Assert.NotEmpty(g.EnemyShots);
        Assert.True((zoshi.Pos - g.Ship.Pos).Length < zoshiStart, "the zoshi closed in");
    }

    [Fact]
    public void A_spario_or_a_flyer_brings_the_ship_down_and_it_starts_again_from_the_middle()
    {
        var g = Calm(distance: 1500);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(112, 200));
        var logram = g.PutGround(GroundKind.Logram, 112, 120);
        logram.Reload = 0;
        for (int i = 0; i < 120 && g.Phase == Phase.Playing; i++) g.Tick(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("hit by a spario", g.LastDeath);
        Assert.Contains(Sfx.Death, heard);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
        Assert.InRange(g.Distance, Field.Checkpoint, Field.Checkpoint + 2);

        g = Calm(distance: 500);
        g.PutShip(new Vec(112, 200));
        var bacura = g.PutAir(AirKind.Bacura, new Vec(112, 150));
        for (int i = 0; i < 120 && g.Phase == Phase.Playing; i++) g.Tick(default);
        Assert.Equal("flew into a bacura", g.LastDeath);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.InRange(g.Distance, 0, 2); // before the middle: back to the start of the area
    }

    [Fact]
    public void The_mothership_hovers_fires_and_falls_to_a_bomb_on_its_core()
    {
        var g = Calm(area: 2, distance: 1600);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutShip(new Vec(112, 236));
        var m = g.SummonMother(-60);
        for (int i = 0; i < 60 * 5; i++) { g.Tick(default); g.EnemyShots.Clear(); }
        Assert.InRange(m.Y, 84, 86);
        Assert.True(m.Hover > 0);
        // A gun first, then the core.
        g.PutShip(new Vec(m.X + Mothership.GunAt[2].X, m.Y + Mothership.GunAt[2].Y + GameEngine.SightAhead));
        Assert.True(g.SightOnTarget);
        g.Tick(BombC);
        for (int i = 0; i < 40; i++) { g.Tick(default); g.EnemyShots.Clear(); }
        Assert.True(m.GunDown[2]);
        Assert.Equal(GameEngine.GunPoints, g.Score);
        g.PutShip(new Vec(m.X, m.Y + GameEngine.SightAhead));
        g.Tick(BombC);
        for (int i = 0; i < 40; i++) { g.Tick(default); g.EnemyShots.Clear(); }
        Assert.True(m.Destroyed);
        Assert.Equal(GameEngine.GunPoints + GameEngine.CorePoints, g.Score);
        Assert.Contains(Sfx.MotherDown, heard);
        Steps(g, 60 * 3);
        Assert.Null(g.Mother);
    }

    [Fact]
    public void The_mothership_comes_by_itself_in_even_areas_and_leaves_if_not_brought_down()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(2, 1600);
        g.Air.Clear();
        bool seen = false;
        for (int i = 0; i < 60 * 40; i++)
        {
            g.Tick(default);
            g.EnemyShots.Clear();
            g.Air.Clear();
            seen |= g.Mother is not null;
        }
        Assert.True(seen);
        Assert.Null(g.Mother);
    }

    [Fact]
    public void Bonus_ships_at_20000_60000_and_every_60000_after()
    {
        var g = Calm();
        Assert.Equal(3, g.Lives);
        g.AddScore(19999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(40000);
        Assert.Equal(5, g.Lives);
        g.AddScore(60000);
        Assert.Equal(6, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_scores_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 90 && g.State == GameState.Playing; i++) g.Tick(pilot.Decide(g));
        Assert.True(g.Score >= 5000, $"scored {g.Score}; lost: {string.Join("; ", g.LossLog)}");
        Assert.True(g.LossLog.Count <= 2, $"lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Tick(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        if (g.HighScores.Qualifies(g.Score))
        {
            Assert.Equal(GameState.EnterInitials, g.State);
            g.Initials!.Finish();
            g.Idle(2);
        }
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Tick(pilot.Decide(g));
            return $"{g.Area} {g.Distance:0.00} {g.Score} {g.Lives} {g.Ship.Pos} {g.Air.Count} {g.EnemyShots.Count}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 20)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Music, heard);
        Assert.Contains(Sfx.Zap, heard);
        Assert.Contains(Sfx.AirHit, heard);
        g.Idle((int)(60 * (GameEngine.AttractSeconds - 18)));
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.Music or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_holds_everything()
    {
        var g = Calm();
        var at = g.Ship.Pos;
        double d = g.Distance;
        g.Pause();
        Steps(g, 30, new Controls(1, 0));
        Assert.Equal(at, g.Ship.Pos);
        Assert.Equal(d, g.Distance);
        g.Resume();
        Steps(g, 30, new Controls(1, 0));
        Assert.True(g.Ship.Pos.X > at.X);
    }

    [Fact]
    public void Every_sound_and_sprite_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var files = Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop).Select(s => s.ToString());
        Assert.All(files, f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
        foreach (var s in new[] { Sprites.Ship, Sprites.ShipBank, Sprites.Toroid[0], Sprites.Torkan, Sprites.Zoshi[1], Sprites.Jara, Sprites.Spario[0], Sprites.Flag })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
    }
}
