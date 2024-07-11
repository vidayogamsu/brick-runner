using System;
using System.Text.Json.Serialization;
using Sandbox;

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


public partial class GameSystem : Component
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


    /*
            Level
    */

    [Property, ReadOnly] public float Level { get; set; } = 1;
    [Property, ReadOnly] public int Coins { get; set; } = 0;

    [Property, ReadOnly] public double Score { get; set; } = 0;
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


    protected override async void OnStart()
    {
        base.OnStart();

        Instance = this;

        await Task.FrameEnd();

        //Level = 100;

        RestartLevel();
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

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

        RestartLevel();
    }

    public async void RestartLevel()
    {
        // Turn off camera for an easy black screen.
        CameraController.Instance.Cam.Enabled = false;

        // Destroy previous level objects.
        foreach ( var obj in Game.ActiveScene.GetAllObjects( false ) )
            if ( obj.Components.TryGet<TemporaryComponent>( out var _, FindMode.EverythingInSelf ) )
                obj.Destroy();

        await Task.Frame();

        // Reset where chunks spawn to the origin.
        SpawnPosition = 0;

        // Spawn starting chunk.
        SpawnChunk( ChunkStyle.Start );

        var spawnPoint = Game.ActiveScene.GetAllComponents<SpawnPoint>()?.FirstOrDefault();

        if ( !spawnPoint.IsValid() )
        {
            Log.Error( "Couldn't find spawn point for player in first chunk." );
            return;
        }

        spawnPoint.Transform.ClearInterpolation();
        var spawnPos = spawnPoint.Transform.Position;

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

        // Wait a second before spawning the player. Prevents instadeath on game startup(???).
        if ( !Player.IsValid() )
        {
            await Task.DelayRealtimeSeconds( 1.0f );

            Player = PlayerPrefab.Clone( spawnPos );

            CameraController.Instance.Player = PlayerController.Instance;
        }

        // Restore health and set position.
        if ( Player.IsValid() && Player.Components.TryGet<PlayerController>( out var p ) )
        {
            // Restore Health
            p.Health = p.MaxLives;

            // Safe Teleport
            p.Velocity = Vector3.Zero;
            p.PreviousVelocity = Vector3.Zero;

            p.StartBlinking(); // funky collision protection
            p.SetPosition( spawnPos );

            // Reactivate Camera
            CameraController.Instance.Cam.Enabled = true;
        }

        // Spawn Death Wall
        if ( DeathWallPrefab.IsValid() )
            DeathWallPrefab.Clone( new Vector3( 0f, -512f, 0f ) );

        // Update Scoreboard
        if ( !Scores.Any() )
            await GetScores();
    }


    public void EndGame()
    {
        if ( GameOver )
            return;

        GameOver = true;

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
        var pos = new Vector3( 0f, SpawnPosition, 0f );

        GameObject chunk = null;

        switch ( style )
        {
            case ChunkStyle.Start:
                chunk = Random.Shared.FromList( StartChunks )?.Clone( pos );
                break;
            case ChunkStyle.Final:
                chunk = Random.Shared.FromList( FinalChunks )?.Clone( pos );
                break;

            case ChunkStyle.Easy:
                chunk = Random.Shared.FromList( EasyChunks )?.Clone( pos );
                break;
            case ChunkStyle.Normal:
                chunk = Random.Shared.FromList( NormalChunks )?.Clone( pos );
                break;
            case ChunkStyle.Medium:
                chunk = Random.Shared.FromList( MediumChunks )?.Clone( pos );
                break;
            case ChunkStyle.Hard:
                chunk = Random.Shared.FromList( HardChunks )?.Clone( pos );
                break;
            case ChunkStyle.Extreme:
                chunk = Random.Shared.FromList( ExtremeChunks )?.Clone( pos );
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
    }
}
