using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVAXevious.Engine;
using AVAXevious.Models;
using AVAXevious.Views;
using Controls = AVAXevious.Engine.Controls;

namespace AVAXevious.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set XEVIOUS_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xevious-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("XEVIOUS_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVAXevious-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_an_area_under_way()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++)
        {
            g.Tick(pilot.Decide(g));
            if (i > 60 * 12 && g.Phase == Phase.Playing && g.Air.Count >= 4 && g.EnemyShots.Count >= 1 && g.Shots.Count >= 2) break;
        }
        Shot(w, c, "play");
        w.Close();
    }

    [AvaloniaFact]
    public void Bombing_the_ground_and_a_sol_risen()
    {
        var (w, c, g) = Open(seed: 3);
        g.StartGame();
        g.JumpTo(3, 500);
        g.Calm();
        g.Idle(160);
        g.PutShip(new Vec(100, 214));
        g.PutGround(GroundKind.Barra, 40, 60);
        g.PutGround(GroundKind.Barra, 62, 60);
        g.PutGround(GroundKind.Zolbak, 170, 40);
        g.PutGround(GroundKind.Grobda, 150, 110);
        var sol = g.PutGround(GroundKind.Sol, 60, 120);
        sol.Revealed = true;
        g.PutGround(GroundKind.Derota, 100, g.Sight.Y);
        g.PutAir(AirKind.Toroid, new Vec(140, 70));
        g.PutAir(AirKind.Bacura, new Vec(60, 30));
        g.Tick(new Controls(0, 0, Bomb: true));
        for (int i = 0; i < 18; i++) g.Tick(new Controls(0, 0, Fire: true));
        Shot(w, c, "bombing");
        w.Close();
    }

    [AvaloniaFact]
    public void The_andor_genesis()
    {
        var (w, c, g) = Open(seed: 4);
        g.StartGame();
        g.JumpTo(2, 1600);
        g.Calm();
        g.Idle(160);
        var m = g.SummonMother(84);
        m.Hover = 2;
        m.GunDown[1] = true;
        g.PutShip(new Vec(m.X - 20, 214));
        g.PutAir(AirKind.Zoshi, new Vec(40, 150));
        g.PutAir(AirKind.Zoshi, new Vec(190, 170));
        for (int i = 0; i < 50; i++) g.Tick(new Controls(0.4, 0, Fire: true));
        Shot(w, c, "mothership");
        w.Close();
    }

    [AvaloniaFact]
    public void Shot_down_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.Go();
        g.Calm();
        g.Idle(160);
        g.PutShip(new Vec(112, 200));
        g.PutAir(AirKind.Bacura, new Vec(112, 190));
        for (int i = 0; i < 60 && g.Phase == Phase.Playing; i++) g.Tick(default);
        Assert.Equal(Phase.Dying, g.Phase);
        g.Idle(14);
        Shot(w, c, "shot-down");
        while (g.State == GameState.Playing) g.Tick(default);
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(5); }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
