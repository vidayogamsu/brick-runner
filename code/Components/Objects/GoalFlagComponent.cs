using System.Threading.Tasks;
using Sandbox;
using Sandbox.Events;

namespace Vidya;


public sealed class GoalFlagComponent : TemporaryComponent, Component.ITriggerListener, IGameEventHandler<RoundCleanup>
{
	[Property, Sync] public bool Triggered { get; set; } = false;

	void IGameEventHandler<RoundCleanup>.OnGameEvent( RoundCleanup eventArgs )
	{
		Triggered = false;
	}

	public void OnTriggerEnter( Collider other )
	{
		if ( !other.IsValid() || Triggered )
			return;

		if ( other.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndAncestors ) )
		{
			if ( Networking.IsHost )
				EndLevel();
		}
	}

	[Broadcast]
	public async void EndLevel()
	{
		if ( !GameSystem.Instance.IsValid() )
			return;
		
		Triggered = true;

		GameSystem.Instance.SendScore();

		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			player.AbleToDie = false;
		}

		// GameSystem.Instance.Level += 14;
		var wall = Scene.GetAllComponents<DeathWallComponent>().FirstOrDefault();
		if ( wall.IsValid() )
			wall.Moving = false;

		var hud = HUD.Instance;

		if ( hud.IsValid() )
			hud.Panel.AddChild( new LevelCompletePanel() );

		await Task.DelaySeconds( 1f );
		//Fade out the screen
		Scene.Dispatch( new FadeScreen( 1f ) );

		GameSystem.Instance.Level++;

		await Task.DelaySeconds( 2f );

		if ( Networking.IsHost )
			GameSystem.Instance.RestartLevel();
		
		GameSystem.Instance.SpawnPosition = 0;
	}
}
