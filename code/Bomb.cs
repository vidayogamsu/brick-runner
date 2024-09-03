using Sandbox;
using Sandbox.Citizen;
using Sandbox.Events;
namespace Vidya;

public sealed class Bomb : Component
{
	public TimeUntil ExplodeTime { get; set; } = 3f;

	[Property] public float FuseTime { get; set; } = 1.5f;

	[Property] public GameObject ExplodeEffect { get; set; }

	[Property] public bool BlinkModel { get; set; } = false;

	[Property] public int BlinkCount { get; set; } = 3;

	[Property] public ModelRenderer Model { get; set; }

	public TimeUntil NextBlink { get; set; } = -1;
	public TimeUntil BlinkingEnd { get; set; } = -1;

	[Property] public float Radius { get; set; } = 128f;
	
	[Sync] public bool IsCarried { get; set; } = false;

	protected override void OnStart()
	{
		if ( Networking.IsHost )
		{
			ExplodeTime = FuseTime;
			StartBlinking();
		}
	}

	protected override void OnUpdate()
	{
		if ( !Networking.IsHost )
			return;

		UpdateBlink();

		if ( ExplodeTime )
		{
			Explode();

			if ( ExplodeEffect.IsValid() )
			{
				var effect = ExplodeEffect.Clone( Transform.Position );
				effect.NetworkSpawn( null );
			}
			
			GameObject.Destroy();
		}
	}

	protected override void DrawGizmos()
	{
		base.DrawGizmos();

		Gizmo.Draw.LineSphere( Vector3.Zero, Radius );
	}

	[Broadcast]
	public void Explode()
	{
		var tr = Scene.Trace.Sphere( Radius, Transform.Position, Transform.Position )
		.IgnoreGameObjectHierarchy( GameObject )
		.HitTriggers()
		.RunAll();

		if ( !tr.Any() )
			return;
		
		foreach ( var t in tr )
		{
			if ( !t.Hit || !t.GameObject.IsValid() )
				return;

			var gb = t.GameObject;

			if ( gb.Components.TryGet<BlockComponent>( out var b, FindMode.EverythingInSelfAndParent ) && gb.Components.TryGet<DestructibleComponent>( out var d, FindMode.EverythingInSelfAndParent ) )
			{
				b.Break( d );
			}

			if ( t.GameObject.Components.TryGet<PlayerController>( out var p, FindMode.EverythingInSelfAndParent ) )
			{
				p.GameObject.Dispatch( new DamageEvent( 1 ) );
			}
		}
	}

	
	[Broadcast]
	public void StartBlinking()
	{
		if ( FuseTime > 0 )
		{
			BlinkModel = true;
			BlinkingEnd = FuseTime;
			NextBlink = FuseTime / BlinkCount;
		}
	}

	public void UpdateBlink()
	{
		if ( !Model.IsValid() )
			return;

		if ( BlinkingEnd )
		{
			BlinkModel = false;
		}
		else if ( NextBlink )
		{
			BlinkModel = !BlinkModel;
			NextBlink = FuseTime / BlinkCount;
		}

		Model.Tint = BlinkModel ? Color.White.WithAlpha( 0.3f ) : Color.White;

		foreach ( var renderer in Model.Components.GetAll<ModelRenderer>( FindMode.InDescendants ) )
		{
			renderer.Tint = BlinkModel ? Color.White.WithAlpha( 0.3f ) : Color.White;
		}
	}
}
