using Sandbox;

namespace Vidya;


public sealed class GoalFlagComponent : TemporaryComponent, Component.ITriggerListener
{
	protected override void OnStart()
	{
		if ( Networking.IsHost )
			GameObject.NetworkSpawn();
	}

    public void OnTriggerEnter( Collider other )
    {
        if ( !other.IsValid() )
            return;

        if ( other.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndAncestors ) )
        {
			if ( !p.AbleToMove )
				return;

            GameSystem.Instance.Level++;
            // GameSystem.Instance.Level += 14;
		
            GameSystem.Instance.SendScore();

			if ( Networking.IsHost )
            	GameSystem.Instance.RestartLevel();

			p.AbleToMove = false;

			GameObject.Destroy();
        }
    }
}
