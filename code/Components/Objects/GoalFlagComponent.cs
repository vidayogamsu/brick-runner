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
		GameSystem.Instance.Level++;
		Triggered = true;
		// GameSystem.Instance.Level += 14;

		GameSystem.Instance.SendScore();
		
		//Fade out the screen

		foreach ( var player in Scene.GetAllComponents<PlayerController>() )
		{
			player.AbleToMove = false;
			player.Velocity = Vector3.Zero;

			var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();

			if ( spawn.IsValid() )
				player.SetPosition( spawn.Transform.Position );
			else
				player.SetPosition( Vector3.Zero );
		}

		Scene.Dispatch( new FadeScreen( 1f ) );

		await Task.DelaySeconds( 2f );

		if ( Networking.IsHost )
			GameSystem.Instance.RestartLevel();

		GameSystem.Instance.SpawnPosition = 0;
	}
}
