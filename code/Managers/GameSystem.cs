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

public record CameraDisable( bool Enabled ) : IGameEvent;

public record RoundCleanup() : IGameEvent;

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

    [Property, ReadOnly] public bool GameOver { get; set; } = false;
    [Property, ReadOnly] public bool GameStarted { get; set; } = false;

	[Sync] public bool OngoingGame { get; set; } = false;


    /*
            Level
    */

    [Property, ReadOnly, Sync] public float Level { get; set; } = 1;
    [Property, ReadOnly, Sync] public int Coins { get; set; } = 0;

    [Property, ReadOnly, Sync] public double Score { get; set; } = 0;
    public static double HighScore { get; set; }

    /// <summary>
    /// The position where next to spawn a chunk.
    /// </summary>
    [Property, ReadOnly] public float SpawnPosition { get; set; } = 0;


    /*
            Prefabs
    */

    public GameObject Player { get; set; }
    [Property] public GameObject PlayerPrefab { get; set; }

    [Property] public GameObject DeathWallPrefab { get; set; }

    [Property, Group( "Chunks" )] public List<GameObject> StartChunks { get; set; } = new();
    [Property, Group( "Chunks" )] public List<GameObject> FinalChunks { get; set; } = new();

    [Property, Group( "Chunks" )] public List<GameObject> EasyChunks { get; set; } = new();
    [Property, Group( "Chunks" )] public List<GameObject> NormalChunks { get; set; } = new();
    [Property, Group( "Chunks" )] public List<GameObject> MediumChunks { get; set; } = new();
    [Property, Group( "Chunks" )] public List<GameObject> HardChunks { get; set; } = new();
    [Property, Group( "Chunks" )] public List<GameObject> ExtremeChunks { get; set; } = new();

    [Property, Sync] public bool StartServer { get; set; } = false;
    [Property] public bool SpawnWorld { get; set; } = true;


    protected override async void OnStart()
    {
        base.OnStart();

        Instance = this;

        if ( StartServer && !GameNetworkSystem.IsActive )
        {
            GameNetworkSystem.CreateLobby();
        }

        await Task.FrameEnd();

        //Level = 100;

        if ( Networking.IsHost )
            RestartLevel();
    }

    public void OnActive( Connection connection )
    {
        var player = PlayerPrefab.Clone();

        player.NetworkSpawn( connection );

        if ( player.Components.TryGet<PlayerController>( out var playerController ) && Networking.IsHost )
        {
			var camera = Scene.GetAllComponents<CameraController>().FirstOrDefault();

			if ( camera.IsValid() )
				playerController.CameraController = camera;


			var nextPlayer = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.Dead );

			if ( OngoingGame && nextPlayer.IsValid() )
				playerController.SetPosition( nextPlayer.Transform.Position );

			var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();

			if ( spawn.IsValid() )
				playerController.SetPosition( spawn.Transform.Position );
        }
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        if ( IsProxy )
            return;

        if ( GameOver && Input.Pressed( "Restart" ) )
        {
            var scene = Game.ActiveScene;
            Scene.Load( scene.Source );

            return;
        }

        if ( Input.Pressed( "Score" ) )
            ShowLeaderboard = !ShowLeaderboard;
    }

    public void RestartGame()
    {
        GameOver = false;

        Level = 1;
        Coins = 0;
        Score = 0;

		if ( Networking.IsHost )
        	RestartLevel();
    }

    [ConCmd( "br_restart_scene" )]
    public static void RestartScene()
    {
        Game.ActiveScene.Load( Game.ActiveScene.Source );
    }

   	[Broadcast]
    public async void RestartLevel()
    {
		OngoingGame = false;
		Log.Info( "Restarting Level" );

		if ( !Player.IsValid() && !StartServer && PlayerPrefab.IsValid() )
        {
            Player = PlayerPrefab.Clone();
        }

        // Turn off camera for an easy black screen.
		Scene.Dispatch( new CameraDisable( false ) );
        // Destroy previous level objects.
        Scene.Dispatch( new RoundCleanup() );

        // Reset where chunks spawn to the origin.
        SpawnPosition = 0;

        // Spawn starting chunk.
        SpawnChunk( ChunkStyle.Start );

        var spawnPoint = Game.ActiveScene.GetAllComponents<SpawnPoint>()?.FirstOrDefault();

        if ( !spawnPoint.IsValid() && SpawnWorld )
        {
            Log.Warning( "Couldn't find spawn point for player in first chunk." );
        }

        if ( spawnPoint.IsValid() )
            spawnPoint.Transform.ClearInterpolation();

        var spawnPos = spawnPoint?.Transform.Position ?? Vector3.Zero;

		if ( Player.IsValid() && !StartServer && Player.Components.TryGet<PlayerController>( out var playerController ) )
		{
			playerController.SetPosition( spawnPos );
		}

        if ( Networking.IsHost )
        {
            // Spawn chunks.
            var maxChunkDist = 3500f + ((Level - 1) * 80f);

            for ( var i = 0; i < 1000; i++ )
            {
                SpawnChunk( RandomDifficulty(), true );

                if ( SpawnPosition > maxChunkDist )
                    break;
            }

            // Spawn final chunk.
            SpawnChunk( ChunkStyle.Final );
        }

		// Wait a second before spawning the player. Prevents instadeath on game startup(???).
		await Task.DelayRealtimeSeconds( 1f );

        if ( Networking.IsHost )
        {
            // Spawn Death Wall
            if ( DeathWallPrefab.IsValid() && SpawnWorld && Networking.IsHost )
            {
                var clone = DeathWallPrefab.Clone( new Vector3( 0f, -512f, 0f ) );

                clone.NetworkSpawn( null );
            }
        }

		Scene.Dispatch( new CameraDisable( true ) );

		Scene.Dispatch( new PlayerRestart( spawnPos ) );

        // Update Scoreboard
        if ( !Scores.Any() )
            await GetScores();

		OngoingGame = true;
    }

    [Broadcast]
    public void EndGame()
    {
        if ( GameOver )
            return;

        GameOver = true;

		if ( !StartServer)
        	SendScore();
    }


    public ChunkStyle RandomDifficulty()
    {
        var weight = 0f + ((Level - 1) * 5f);
        weight = Game.Random.Float( weight - 15f, weight + 10f ).Clamp( 0f, 100f );

        // Log.Info( "weight: " + weight );

        return weight switch
        {
            < 20f => ChunkStyle.Easy,
            < 40f => ChunkStyle.Normal,
            < 60f => ChunkStyle.Medium,
            < 80f => ChunkStyle.Hard,
            <= 100f => ChunkStyle.Extreme,
            _ => ChunkStyle.Extreme,
        };
    }


    /*
            Level Generation
    */

    public void SpawnChunk( ChunkStyle style, bool withHazards = false )
    {
        if ( !SpawnWorld || !Networking.IsHost )
            return;

        var pos = new Vector3( 0f, SpawnPosition, 0f );

        GameObject chunk = null;

        switch ( style )
        {
            case ChunkStyle.Start:
                chunk = Game.Random.FromList( StartChunks )?.Clone( pos );
                break;
            case ChunkStyle.Final:
                chunk = Game.Random.FromList( FinalChunks )?.Clone( pos );
                break;

            case ChunkStyle.Easy:
                chunk = Game.Random.FromList( EasyChunks )?.Clone( pos );
                break;
            case ChunkStyle.Normal:
                chunk = Game.Random.FromList( NormalChunks )?.Clone( pos );
                break;
            case ChunkStyle.Medium:
                chunk = Game.Random.FromList( MediumChunks )?.Clone( pos );
                break;
            case ChunkStyle.Hard:
                chunk = Game.Random.FromList( HardChunks )?.Clone( pos );
                break;
            case ChunkStyle.Extreme:
                chunk = Game.Random.FromList( ExtremeChunks )?.Clone( pos );
                break;
        }

        if ( !chunk.IsValid() )
        {
            Log.Error( "Couldn't spawn prefab of style: " + style );
            return;
        }

        if ( !chunk.Components.TryGet<ChunkComponent>( out var chunkComp ) )
        {
            Log.Error( "Couldn't find ChunkComponent on: " + chunk.Name );
            return;
        }

        // Increment next position by chunk width.
        SpawnPosition += chunkComp.Width;

        // Remove hazards if not enabled.
        if ( !withHazards )
        {
            foreach ( var haz in chunk.Components.GetAll<HazardComponent>( FindMode.EverythingInDescendants ) )
            {
                haz.GameObject.Destroy();
            }
        }

        chunk.NetworkSpawn( null );
    }
}
