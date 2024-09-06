using System;
using System.Text.Json.Serialization;
using Sandbox;
using Sandbox.Events;
using Sandbox.Network;

namespace Vidya;


public enum ChunkStyle
{
    /// <summary> The beginning of a level. </summary>
    Start = 0,
    /// <summary> The end of a level. </summary>
    Final = 1,

    Easy = 10,
    Normal = 20,
    Medium = 30,
    Hard = 40,
    Extreme = 50,
}

public record PlayerRestart( Vector3 Pos ) : IGameEvent;

public record FadeScreen( float opacity ) : IGameEvent;

public record RoundCleanup() : IGameEvent;

public record GameModeStartEvent() : IGameEvent;

public record PlayerConnectedEvent( Connection connection ) : IGameEvent;

public record GameEndEvent() : IGameEvent;

public partial class GameSystem : Component, Component.INetworkListener
{
    public static GameSystem Instance { get; set; }

    public GameSystem()
    {
        Instance = this;
    }


    /*
            Game State
    */

    [Property, JsonIgnore] public static bool MainMenu { get; set; } = true;
    [Property, JsonIgnore] public bool Paused { get; set; } = false;

    public bool GameOver { get; set; } = false;
    [Property, ReadOnly] public bool GameStarted { get; set; } = false;
	[Property, ReadOnly, Sync] public float Level { get; set; } = 1;
	[Property, ReadOnly, Sync] public int Coins { get; set; } = 0;

	[Property, ReadOnly, Sync] public double Score { get; set; } = 0;
	public static double HighScore { get; set; }

	[Sync] public bool OngoingGame { get; set; } = false;


    /*
            Level
    */

  
    [Property, Sync] public bool StartServer { get; set; } = false;
	[Property] public GameModeResource GameModeOverride { get; set; }

	public static Dictionary<string, GameModeResource> GameModes { get; set; } = new();

    public static GameModeResource CurrentGameMode { get; set; }

    protected override async void OnStart()
    {
        base.OnStart();

		LoadGameMode( "adventure" );

        Instance = this;

		if ( Networking.IsHost )
			GameOver = false;

        if ( StartServer && !GameNetworkSystem.IsActive )
        {
            GameNetworkSystem.CreateLobby();
        }

        await Task.FrameEnd();

		Scene.Dispatch( new GameModeStartEvent() );

        //Level = 100;
    }

	public static void LoadGameMode( string name )
	{
		if ( GameModes is null )
		{
            Log.Warning( "GameModes is null" );
            return;
        }

		GameModes.TryGetValue( name, out var resource );

        if ( resource is null )
        {
            Log.Warning( $"GameMode {name} not found" );
            return;
        }

        CurrentGameMode = resource;

		var mode = resource.Prefab.Clone();
		mode.NetworkSpawn(null);

	}

    public void OnActive( Connection connection )
    {
		Scene.Dispatch( new PlayerConnectedEvent( connection ) );
    }

    [ConCmd( "br_restart_scene" )]
    public static void RestartScene()
    {
        Game.ActiveScene.Load( Game.ActiveScene.Source );
    }
}

[GameResource( "GameMode", "mode", "A gamemode" )]
public class GameModeResource : GameResource
{
	public GameObject Prefab { get; set; }
	public bool Hidden { get; set; }
    public string LeaderboardStat { get; set; } = "lb_v1_stat";

	protected override void PostLoad()
	{
		base.PostLoad();

		if ( Hidden )
			return;

		if ( !GameSystem.GameModes.ContainsKey(ResourceName) )
		{
			GameSystem.GameModes.Add( ResourceName, this );
		}
	}
}
