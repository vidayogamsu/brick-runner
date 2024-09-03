namespace Vidya;

using Sandbox.Events;

public sealed class BombRushGamemode : Component, IGameEventHandler<GameModeStartEvent>, IGameEventHandler<PlayerConnectedEvent>,
	IGameEventHandler<OnPlayerDeath>
{
	[Property] public GameObject PlayerPrefab { get; set; }
	[Property] public GameObject BombPrefab { get; set; }


	void IGameEventHandler<GameModeStartEvent>.OnGameEvent( GameModeStartEvent eventArgs )
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		gs.OngoingGame = true;
	}

	void IGameEventHandler<PlayerConnectedEvent>.OnGameEvent( PlayerConnectedEvent eventArgs )
	{
		var gs = GameSystem.Instance;

		if ( !gs.IsValid() )
			return;

		if ( !gs.StartServer || !PlayerPrefab.IsValid() || !BombPrefab.IsValid() )
			return;

		var player = PlayerPrefab.Clone();

		var shooter = player.Components.Create<BombShooterComponent>();
		shooter.BombPrefab = BombPrefab;

		player.NetworkSpawn( eventArgs.connection );

		var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();

		if ( spawn.IsValid() )
			player.Transform.Position = spawn.Transform.Position;
	}

	[Broadcast( NetPermission.HostOnly )]
	public async void EndGame()
	{
		var hud = HUD.Instance;

		var lastPlayer = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.Dead );

		if ( hud.IsValid() && lastPlayer.IsValid() )
		{
			var loadingPanel = new LoadingPanel();
			loadingPanel.Text = $"{lastPlayer.Network.OwnerConnection.DisplayName} wins!";

			hud.Panel.AddChild( loadingPanel );


			await Task.DelaySeconds( 1f );

			Scene.Dispatch( new FadeScreen( 1f ));

			await Task.DelaySeconds( 2f );

			var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();

			var spawnPos = spawn.IsValid() ? spawn.Transform.Position : Vector3.Zero;

			Scene.Dispatch( new PlayerRestart( spawnPos ) );

			hud.Panel.Children.FirstOrDefault( x => x is LoadingPanel )?.Delete();

			Scene.Dispatch( new FadeScreen( 0f ) );
			
			GameSystem.RestartScene();
		}
	}

	void IGameEventHandler<OnPlayerDeath>.OnGameEvent( OnPlayerDeath eventArgs )
	{
		var player = eventArgs.player;

		if ( !player.IsValid() )
			return;

		if ( Scene.GetAllComponents<PlayerController>().Count( x => !x.Dead) == 1 )
		{
			EndGame();
		}
	}
}

public class BombShooterComponent : Component
{
	[Property] public GameObject BombPrefab { get; set; }

	[Property] public float ShootInterval { get; set; } = 1f;

	public TimeUntil NextShoot { get; set; }

	public bool CarryingBomb { get; set; } = false;

	public Bomb CarriedBomb { get; set; }

	protected override void OnUpdate()
	{
		if ( IsProxy )
			return;

		if ( NextShoot && Input.Pressed( "attack1" ) && !CarryingBomb )
		{
			ShootBomb();
			NextShoot = ShootInterval;
		}

		if ( Input.Pressed( "attack1" ) && CarryingBomb && CarriedBomb.IsValid() && CarriedBomb.GameObject.Components.TryGet<Rigidbody>( out var rb ) )
		{
			CarriedBomb.GameObject.SetParent( null );

			rb.MotionEnabled = true;
			rb.Velocity += PlayerController.Local.Velocity + PlayerController.Local.Model.Transform.World.Up * 200f + PlayerController.Local.Model.Transform.World.Forward * 300f;

			CarriedBomb.Network.DropOwnership();

			CarriedBomb.IsCarried = false;

			CarryingBomb = false;
		}
		else if ( !CarriedBomb.IsValid() )
		{
			CarryingBomb = false;
		}
	}

	protected override void OnFixedUpdate()
	{
		var local = PlayerController.Local;

		var collider = local.Components.Get<Collider>( FindMode.EnabledInSelfAndChildren );

		if ( IsProxy || !collider.IsValid() )
			return;

		foreach ( var c in collider.Touching )
		{
			if ( c.GameObject.IsValid() && c.GameObject.Components.TryGet<Bomb>( out var bomb, FindMode.EverythingInSelfAndParent ) && Input.Pressed( "use" ) && !CarryingBomb )
			{
				if ( bomb.IsCarried )
					return;

				bomb.Network.TakeOwnership();

				bomb.GameObject.SetParent( local.GameObject );

				bomb.Transform.LocalPosition = local.Transform.World.Up * 42;
				bomb.Transform.ClearInterpolation();

				if ( bomb.Components.TryGet<Rigidbody>( out var rb ) )
				{
					rb.MotionEnabled = false;
				}

				bomb.IsCarried = true;

				CarryingBomb = true;

				CarriedBomb = bomb;
			}
		}
	}

	public void ShootBomb()
	{
		var local = PlayerController.Local;

		if ( !BombPrefab.IsValid() || !local.IsValid() && !local.Model.IsValid() )
			return;

		var bomb = BombPrefab.Clone( Transform.Position +  local.Model.Transform.World.Forward * 30 + local.Model.Transform.World.Up * 20 );

		if ( bomb.Components.TryGet<Rigidbody>( out var rb ) )
		{
			rb.Velocity += PlayerController.Local.Velocity + local.Model.Transform.World.Up * 200f + local.Model.Transform.World.Forward * 300f;
		}

		bomb.NetworkSpawn( null );
	}
}
