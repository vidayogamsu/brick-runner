using System;
using System.Net.Sockets;
using Sandbox;
using Sandbox.Events;

namespace Vidya;


public partial class CameraController : Component, IGameEventHandler<CameraDisable>
{

    public CameraComponent Cam { get; set; }

	public bool Spectating { get; set; } = false;
	public PlayerController SpectateTarget { get; set; }

    protected override void OnStart()
    {
        Cam = Scene.GetAllComponents<CameraComponent>().FirstOrDefault();

		var local = PlayerController.Local;

		if ( local.IsValid() && !local.IsProxy )
			local.CameraController = this;
    }

    protected override void OnUpdate()
    {
		var local = PlayerController.Local;

		if ( local.IsValid() && local.IsProxy )
			return;

        base.OnUpdate();

		if ( !Cam.IsValid() || !local.IsValid() )
			return;

		if ( !local.Dead )
		{
			Spectating = false;
			Transform.Position = new Vector3( Cam.Transform.Position.x, local.Transform.Position.y, 0f );
		}
		else
		{
			Spectating = true;

			var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.Dead );
			
			if ( !player.IsValid() )
				return;

			SpectateTarget = player;
			Transform.Position = new Vector3( Cam.Transform.Position.x, player.Transform.Position.y, 0f );
		}
    }

	void IGameEventHandler<CameraDisable>.OnGameEvent( CameraDisable eventArgs )
	{
		if ( Cam.IsValid() )
			Cam.Enabled = true;
	}
}
