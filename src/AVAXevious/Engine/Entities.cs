using AVAXevious.Models;

namespace AVAXevious.Engine;

/// <summary>The player's fighter.</summary>
public sealed class Ship
{
    public Vec Pos { get; set; }
    /// <summary>Which way it's leaning, for the drawing (−1, 0, 1).</summary>
    public int Bank { get; set; }
}

/// <summary>A zapper shot, going straight up.</summary>
public sealed class Shot
{
    public Vec Pos { get; set; }
    public bool Dead { get; set; }
}

/// <summary>A bomb falling to the ground where the sight was when it was dropped.</summary>
public sealed class Bomb
{
    public Vec From { get; init; }
    /// <summary>Where it lands, in ground terms: across, and how far into the area.</summary>
    public double TargetX { get; init; }
    public double TargetW { get; init; }
    /// <summary>Where the sight was on the screen: the mothership hangs in the air, so it's hit there.</summary>
    public double TargetY { get; init; }
    public double T { get; set; }
}

public enum AirKind
{
    /// <summary>Spinning rings: dive in, then turn tail.</summary>
    Toroid,
    /// <summary>Drops in, stops, fires, and climbs away.</summary>
    Torkan,
    /// <summary>Spinning crosses that home in, in twos and threes.</summary>
    Zoshi,
    /// <summary>Swoops back and forth on the way down.</summary>
    Jara,
    /// <summary>A great spinning plate that can't be destroyed: shots bounce off it.</summary>
    Bacura,
}

public sealed class AirEnemy
{
    public required AirKind Kind { get; init; }
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public double Age { get; set; }
    /// <summary>Seconds until it next fires.</summary>
    public double Reload { get; set; }
    /// <summary>Turned to run.</summary>
    public bool Fleeing { get; set; }
    public double Phase { get; init; }
    /// <summary>Where a Torkan stops to fire; where a Jara swoops about.</summary>
    public double Stop { get; init; }
    public double BaseX { get; init; }
    public bool Dead { get; set; }
}

public enum GroundKind
{
    /// <summary>A pyramid: just sits there.</summary>
    Barra,
    /// <summary>A radar dome.</summary>
    Zolbak,
    /// <summary>A dome turret that fires at the ship.</summary>
    Logram,
    /// <summary>A tank that runs for it when the sight is on it.</summary>
    Grobda,
    /// <summary>A heavy gun emplacement firing streams of shots.</summary>
    Derota,
    /// <summary>A tower hidden under the ground until a bomb finds it.</summary>
    Sol,
    /// <summary>A flag hidden in the ground: bomb it out and fly over it for an extra ship.</summary>
    Flag,
}

public sealed class GroundTarget
{
    public required GroundKind Kind { get; init; }
    public double X { get; set; }
    /// <summary>How far into the area it stands.</summary>
    public double W { get; set; }
    public double Reload { get; set; }
    /// <summary>For the Sol and the flag: dug out by a bomb.</summary>
    public bool Revealed { get; set; }
    /// <summary>A tank on the move.</summary>
    public double Vx { get; set; }
    public bool Dead { get; set; }
    /// <summary>Bombed: a crater stays where it was.</summary>
    public bool Wrecked { get; set; }
}

/// <summary>An enemy shot (a spario): a glowing ball that can't be shot down.</summary>
public sealed class EnemyShot
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public bool Dead { get; set; }
}

/// <summary>
/// The Andor Genesis: the great mothership that hovers over some areas. Its four guns fire at the
/// ship; a bomb on the core in the middle brings the whole thing down.
/// </summary>
public sealed class Mothership
{
    public double X { get; set; } = Field.Width / 2.0;
    public double Y { get; set; } = -70;
    /// <summary>How long it has hung over the ground.</summary>
    public double Hover { get; set; }
    public bool[] GunDown { get; } = new bool[4];
    public double Reload { get; set; } = 1;
    public bool Destroyed { get; set; }
    public double DeathTime { get; set; }

    public const double Radius = 46;
    public static readonly Vec[] GunAt = [new(-26, -26), new(26, -26), new(-26, 26), new(26, 26)];
    public Vec Centre => new(X, Y);
}

public enum EffectKind { Score, Blast, Crater, BigBlast }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    /// <summary>For things on the ground: how far into the area (they scroll with it).</summary>
    public double? W { get; init; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: the stick (each axis −1 to 1), the zapper and the blaster.</summary>
public record struct Controls(double Dx, double Dy, bool Fire = false, bool Bomb = false);
