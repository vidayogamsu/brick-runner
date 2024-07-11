using Sandbox;

namespace Vidya;


public class GibComponent : Component, Component.ICollisionListener
{
    [Property] public float Bounciness { get; set; } = 0.7f;
    [Property] public float MinSpeed { get; set; } = 200f;
    [Property] public float Lifetime { get; set; } = 10f;

    public TimeUntil SelfDestruct { get; set; }

    public Vector3 PreviousVelocity { get; set; } = Vector3.Zero;

    public Rigidbody RB { get; set; }


    protected override void OnStart()
    {
        base.OnStart();

        RB = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndAncestors );

        SelfDestruct = Lifetime;
    }

    protected override void OnUpdate()
    {
        base.OnUpdate();

        if ( SelfDestruct )
            GameObject.Destroy();
    }


    public void OnCollisionStart( Collision c )
    {
        var speed = PreviousVelocity;//c.Contact.Speed;
        var speedLen = PreviousVelocity.Length;//c.Contact.NormalSpeed;//speed.Length;
        var surfNormal = c.Contact.Normal;

        // Contact surface normals are fucked.
        // if ( c.Other.Collider is not SphereCollider )
        surfNormal *= -1;

        // Speed Requirement
        if ( (speed * surfNormal).Length > MinSpeed )
        {
            var dir = Vector3.Reflect( speed.Normal, surfNormal );
            var bounce = dir * speedLen * Bounciness;

            c.Self.Body.Velocity = bounce;
        }
    }

    protected override void OnFixedUpdate()
    {
        if ( RB.IsValid() )
            PreviousVelocity = RB.Velocity;
    }
}