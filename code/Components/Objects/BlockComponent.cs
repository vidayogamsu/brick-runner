using System;
using Sandbox;

namespace Vidya;


public sealed class BlockComponent : Component
{
	/// <summary>
	/// Play an animation when player hits this from below?
	/// </summary>
	[Property] public bool AllowNudge { get; set; } = true;

	[Sync] public bool Nudging { get; set; } = false;
	[Sync] public TimeUntil NudgeEnd { get; set; }
	[Sync] public Vector3 StartPos { get; set; }

	[Sync] public ModelRenderer Model { get; set; }

	[Property] public bool StartGameAfterNudge { get; set; } = false;

	[Property] public bool IsBreakable { get; set; } = false;

	[Property] public Action OnBreak { get; set; }


	protected override void OnStart()
	{
		base.OnStart();

		Model = Components.Get<ModelRenderer>( FindMode.EverythingInChildren );

		if ( StartGameAfterNudge && !Networking.IsHost )
			GameObject.Destroy();
	}

	protected override void OnUpdate()
	{
		if ( Nudging && Model.IsValid() && GameObject.NetworkMode == NetworkMode.Object && Networking.IsHost )
		{
			NudgeBlockBroadcast();
		}
		else if ( Nudging && Model.IsValid() && GameObject.NetworkMode != NetworkMode.Object )
		{
			NudgeBlock();
		}
	}

	[Broadcast( NetPermission.HostOnly )]
	public void NudgeBlockBroadcast()
	{
		NudgeBlock();
	}

	public void NudgeBlock()
	{
		if ( NudgeEnd.Fraction < 1f )
		{
			var frac = 1f - NudgeEnd.Fraction;
			Model.Transform.Position = StartPos.WithZ( StartPos.z + (16f * frac) );

			if ( Components.TryGet<DestructibleComponent>( out var destructibleComponent ) && !destructibleComponent.Invulnerable && IsBreakable )
			{
				Break( destructibleComponent );
			}

			if ( StartGameAfterNudge && Networking.IsHost )
				Scene?.LoadFromFile( "scenes/networking.scene" );
		}
		else
		{
			Model.Transform.Position = StartPos;
			Nudging = false;
		}
	}

	public void Break( DestructibleComponent destructibleComponent )
	{
		if ( !destructibleComponent.IsValid() )
			return;

		destructibleComponent.DoEffect();
		OnBreak?.Invoke();

		GameObject.Destroy();
	}

	public void StartNudge()
	{
		if ( GameObject.NetworkMode == NetworkMode.Object )
			BroadcastNudge();
		else
			Nudge();
	}
	
	public void Nudge()
	{
		if ( !AllowNudge || !Model.IsValid() )
			return;

		if ( !Nudging )
			StartPos = Model.Transform.Position;

		Nudging = true;
		NudgeEnd = 0.2f;
	}

	[Broadcast]
	public void BroadcastNudge()
	{
		Nudge();
	}
}
