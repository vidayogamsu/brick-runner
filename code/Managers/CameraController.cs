using System;
using System.Net.Sockets;
using Sandbox;
using Sandbox.Events;

namespace Vidya;


public partial class CameraController : Component, IGameEventHandler<CameraDisable>
{

    public CameraComponent Cam { get; set; }

    protected override void OnStart()
    {
        Cam = Scene.Camera;
    }

    protected override void OnUpdate()
    {
		var local = PlayerController.Local;

		if ( local.IsValid() && local.IsProxy )
			return;

        base.OnUpdate();

		if ( !Cam.IsValid() || !local.IsValid() )
			return;

        Transform.Position = new Vector3( Cam.Transform.Position.x, local.Transform.Position.y, 0f );
    }

	void IGameEventHandler<CameraDisable>.OnGameEvent( CameraDisable eventArgs )
	{
		Cam = Scene.Camera;

		if ( !Cam.IsValid() )
			return;

		Cam.Enabled = true;		
	}
}
