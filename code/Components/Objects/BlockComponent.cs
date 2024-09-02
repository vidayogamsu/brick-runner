using System;
using Sandbox;

namespace Vidya;


public sealed class BlockComponent : Component
{
    /// <summary>
    /// Play an animation when player hits this from below?
    /// </summary>
    [Property] public bool AllowNudge { get; set; } = true;

    public bool Nudging { get; set; } = false;
    public TimeUntil NudgeEnd { get; set; }
    public Vector3 StartPos { get; set; }

    public ModelRenderer Model { get; set; }

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
        if ( Nudging && Model.IsValid() )
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
    }

	public void Break( DestructibleComponent destructibleComponent )
	{
		destructibleComponent.DoEffect();
		OnBreak?.Invoke();
		destructibleComponent.GameObject.Destroy();
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
}
