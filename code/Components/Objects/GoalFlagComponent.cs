using Sandbox;

namespace Vidya;


public sealed class GoalFlagComponent : TemporaryComponent, Component.ITriggerListener
{
    public void OnTriggerEnter( Collider other )
    {
        if ( !other.IsValid() )
            return;

        if ( other.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndAncestors ) )
        {
            GameSystem.Instance.Level++;
            // GameSystem.Instance.Level += 14;

            GameSystem.Instance.SendScore();
            
            GameSystem.Instance.RestartLevel();
        }
    }
}
