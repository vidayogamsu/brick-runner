using Sandbox.Events;

namespace Vidya;

public sealed class AdventureGamemode : Component, IGameEventHandler<GameModeStartEvent>, IGameEventHandler<PlayerConnectedEvent>, IGameEventHandler<GameEndEvent>
{
	/// <summary>
	/// The position where next to spawn a chunk.
	/// </summary>
	[Property, ReadOnly] public float SpawnPosition { get; set; } = 0;


	/*
            Prefabs
    */

	public static AdventureGamemode Instance { get; set; }

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
	[Property] public bool SpawnWorld { get; set; } = true;

	protected override void OnStart()
	{
		Instance = this;
	}

	void IGameEventHandler<GameModeStartEvent>.OnGameEvent( GameModeStartEvent eventArgs )
	{
		if ( Networking.IsHost )
		{
			BroadcastLoadingPanel();
			RestartLevel();
		}
	}

	void IGameEventHandler<PlayerConnectedEvent>.OnGameEvent( PlayerConnectedEvent eventArgs )
	{
		var gs = GameSystem.Instance;
		
		if ( !gs.IsValid() )
			return;

		if ( !gs.StartServer || !PlayerPrefab.IsValid() )
			return;

		var player = PlayerPrefab.Clone();

		player.NetworkSpawn( eventArgs.connection );

		if ( player.Components.TryGet<PlayerController>( out var playerController ) )
		{
			if ( gs.OngoingGame )
			{
				playerController.Die();
				return;
			}

			var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();

			if ( spawn.IsValid() )
				playerController.SetPosition( spawn.Transform.Position );
		}
	}

	[Broadcast]
	public static void BroadcastLoadingPanel()
	{
		var hud = HUD.Instance;

		if ( hud.IsValid() )
			HUD.Instance.Panel.AddChild( new LoadingPanel() );
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		var gs = GameSystem.Instance;

		if ( IsProxy || !gs.IsValid() )
			return;

		if ( gs.GameOver && Input.Pressed( "Restart" ) )
		{
			var scene = Game.ActiveScene;
			Scene.Load( scene.Source );

			return;
		}

		if ( Input.Pressed( "Score" ) )
			GameSystem.ShowLeaderboard = !GameSystem.ShowLeaderboard;
	}

	public void RestartGame()
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		gs.GameOver = false;

		gs.Level = 1;
		gs.Coins = 0;
		gs.Score = 0;

		if ( Networking.IsHost )
			RestartLevel();
	}

	[Broadcast]
	public async void RestartLevel()
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		gs.OngoingGame = false;
		Log.Info( "Restarting Level" );

		if ( !Player.IsValid() && !gs.StartServer && PlayerPrefab.IsValid() )
		{
			Player = PlayerPrefab.Clone();
		}

		var hud = HUD.Instance;

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

		if ( Player.IsValid() && !gs.StartServer && Player.Components.TryGet<PlayerController>( out var playerController ) )
		{
			playerController.SetPosition( spawnPos );
		}

		if ( Networking.IsHost )
		{
			// Spawn chunks.
			var maxChunkDist = 3500f + ((gs.Level - 1) * 80f);

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

			var wall = Scene.GetAllComponents<DeathWallComponent>().FirstOrDefault();
			if ( wall.IsValid() )
				wall.Moving = true;
		}

		if ( hud.IsValid() )
		{
			hud.Panel.Children.FirstOrDefault( x => x is LoadingPanel )?.Delete();
		}

		Scene.Dispatch( new FadeScreen( 0f ) );

		Scene.Dispatch( new PlayerRestart( spawnPos ) );

		// Update Scoreboard
		await gs.GetScores();

		gs.OngoingGame = true;
	}

	[Broadcast( NetPermission.HostOnly )]
	private void EndGame()
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		if ( gs.GameOver )
			return;

		gs.GameOver = true;
		
		gs.SendScore();
	}

	void IGameEventHandler<GameEndEvent>.OnGameEvent( GameEndEvent eventArgs )
	{
		EndGame();
	}


	public ChunkStyle RandomDifficulty()
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return ChunkStyle.Normal;

		var weight = 0f + ((gs.Level - 1) * 5f);
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
