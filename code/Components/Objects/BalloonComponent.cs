using Sandbox;

namespace Vidya;


public sealed class BalloonComponent : Component, Component.ITriggerListener
{
    [Property] public ModelRenderer Model { get; set; }

    public float BounceForce { get; set; } = 400f;

    public bool Faded { get; set; }
    public TimeUntil Unfade { get; set; }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        if ( Faded && Unfade )
        {
            Faded = false;

            if ( Model.IsValid() )
                Model.Tint = Model.Tint.WithAlpha( 1.0f );
        }
    }

    public void OnTriggerEnter( Collider other )
    {
        if ( !other.IsValid() || Faded )
            return;

        // Log.Info( "Balloon touched other: " + other.GameObject.Name );

        if ( other.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndAncestors ) )
        {
			if ( p.IsProxy )
				return;
			
            var up = Transform.Rotation.Up;
            p.Velocity = p.Velocity.SubtractDirection( -up );
            p.Velocity += up * BounceForce;

            Faded = true;
            Unfade = 0.4f;

            if ( Model.IsValid() )
                Model.Tint = Model.Tint.WithAlpha( 0.3f );
        }
    }
}
