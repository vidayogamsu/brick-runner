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
            GameSystem.Instance.Level++;
            // GameSystem.Instance.Level += 14;
		
            GameSystem.Instance.SendScore();

			if ( Networking.IsHost )
            	GameSystem.Instance.RestartLevel();

			GameSystem.Instance.SpawnPosition = 0;

			foreach ( var player in Scene.GetAllComponents<PlayerController>() )
			{
				var spawn = Scene.GetAllComponents<SpawnPoint>().FirstOrDefault();
	
				if ( spawn.IsValid() )
					player.SetPosition( spawn.Transform.Position );
				else
					player.SetPosition( Vector3.Zero );
			}
			
			Triggered = true;
        }
    }
}
