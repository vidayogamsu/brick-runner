using System;
using Sandbox;

namespace Vidya;


public sealed class DeathWallComponent : TemporaryComponent
{
    public float Speed { get; set; } = 50f;
    public float MaxDist { get; set; } = 400f;
    public float AccelFactor { get; set; } = 0.7f;

	[Sync] public bool Moving { get; set; } = true;


    protected override void OnUpdate()
    {
        base.OnUpdate();

		if ( !Networking.IsHost || !Moving )
			return;
		
		if ( Scene.GetAllComponents<PlayerController>().Where( x => !x.Dead ).Count() == 0 )
			return;

        var plObj = Scene.GetAllComponents<PlayerController>().Where( x => !x.Dead ).OrderBy( x => x.Transform.Position.Distance( Transform.Position ) ).FirstOrDefault().GameObject;

        if ( !plObj.IsValid() )
            return;

        if ( !plObj.Components.TryGet<PlayerController>( out var pl, FindMode.EverythingInSelf ) )
            return;

        // Catch up if too far away.
        var speed = Speed;

        var dist = MathF.Max( 0f, plObj.GetBounds().Center.y - Transform.Position.y );

        if ( dist > MaxDist )
            speed += (dist - MaxDist) * AccelFactor;

        // Move to the right. It's reversed because -y being right is annoying.
        var newPos = Transform.Position + (Vector3.Left * speed * Time.Delta);
        
        // Prevent moving out of engine bounds.
        if ( !float.IsNaN( newPos.y ) )
            Transform.Position = newPos;
    }
}
