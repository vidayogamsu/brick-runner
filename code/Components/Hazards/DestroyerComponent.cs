using System.ComponentModel.Design.Serialization;
using Sandbox;

namespace Vidya;


/// <summary>
/// Destroys stuff within chunks that it touches.
/// </summary>
public sealed class DestroyerComponent : TemporaryComponent, Component.ITriggerListener
{
    [Property] public SoundEvent BreakSound { get; set; }
    [Property] public float SoundCooldown = 0.05f;

    public TimeUntil NextSound { get; set; } = -1;


    public void OnTriggerEnter( Collider other )
    {
        if ( !other.IsValid() )
            return;

        if ( other.Components.TryGet<DestructibleComponent>( out var d, FindMode.EverythingInSelfAndAncestors ) )
        {
            if ( d.Invulnerable )
                return;

            d.DoEffect();
            d.GameObject.Destroy();

            if ( NextSound )
            {
                NextSound = SoundCooldown;
                Sound.Play( BreakSound, d.GameObject.GetBounds().Center );
            }
        }
    }
}
