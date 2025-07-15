using Sandbox.Utility;

namespace Vidya;


public sealed class BalloonComponent : Component, Component.ITriggerListener
{
    [Property] public ModelRenderer Model { get; set; }

    public float BounceForce { get; set; } = 400f;

    public bool Faded { get; set; }
    public TimeUntil Unfade { get; set; }

    public TimeUntil SizeTime { get; set; }

    public float TargetSize { get; set; } = 1.0f;

    protected override void OnStart()
    {
        Model.Tint = Color.Random;
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        if ( Faded && Unfade )
        {
            Faded = false;

            SizeTime = 0.1f;
            TargetSize = 1.0f;

            if ( Model.IsValid() )
                Model.Tint = Model.Tint.WithAlpha( 1.0f );
        }

        if ( !SizeTime )
            Model.WorldScale = Model.WorldScale.LerpTo( Vector3.One * TargetSize, Easing.BounceOut( SizeTime.Fraction ) );
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

            var up = WorldRotation.Up;
            p.Velocity = p.Velocity.SubtractDirection( -up );
            p.Velocity += up * BounceForce;

            Faded = true;
            Unfade = 0.4f;

            Sound.Play( "balloon.pop", WorldPosition );

            if ( Model.IsValid() )
                Model.Tint = Model.Tint.WithAlpha( 0.3f );

            SizeTime = 0.1f;
            TargetSize = 0.25f;
        }
    }
}
