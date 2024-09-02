using Sandbox;
using Sandbox.Events;

namespace Vidya;


/// <summary>
/// A component with a collider that can cause damage.
/// </summary>
public class HazardComponent : TemporaryComponent, Component.ITriggerListener
{
    public static void InflictDamage( Collider other )
    {
        if ( !other.IsValid() )
            return;

        if ( other.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndAncestors ) )
        {
            p.GameObject.Dispatch( new DamageEvent( 1 ) );
        }
    }

    public void OnTriggerEnter( Collider other )
    {
		InflictDamage( other );
    }

    public void OnTriggerExit( Collider other )
    {
    }
}
