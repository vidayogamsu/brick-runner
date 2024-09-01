using System;
using System.Net.Sockets;
using Sandbox;
using Sandbox.Events;

namespace Vidya;


public partial class CameraController : Component, IGameEventHandler<CameraDisable>
{

	[Sync] public bool Spectating { get; set; } = false;
	[Sync] public PlayerController SpectateTarget { get; set; }

    protected override void OnUpdate()
    {
		var local = PlayerController.Local;

		var Cam = Scene?.Camera;

		if ( !Cam.IsValid() || !local.IsValid() )
			return;

		if ( !local.Dead )
		{
			Spectating = false;
			Cam.Transform.Position = new Vector3( Cam.Transform.Position.x, local.Transform.Position.y, 0f );
		}
		else
		{
			Spectating = true;

			var player = Scene.GetAllComponents<PlayerController>().FirstOrDefault( x => !x.Dead );

			if ( !player.IsValid() )
				return;

			SpectateTarget = player;
			Cam.Transform.Position = new Vector3( Cam.Transform.Position.x, player.Transform.Position.y, 0f );
		}
    }

	void IGameEventHandler<CameraDisable>.OnGameEvent( CameraDisable eventArgs )
	{
		//if ( Scene.Camera.IsValid() )
			//Scene.Camera.Enabled = eventArgs.Enabled;
	}
}
