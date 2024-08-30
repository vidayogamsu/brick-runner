using System;
using System.Net.Sockets;
using Sandbox;

namespace Vidya;


public partial class CameraController : Component
{
    public static CameraController Instance { get; set; }

    public CameraController()
    {
        Instance = this;
    }

    public CameraComponent Cam { get; set; }

    protected override void OnStart()
    {
		Instance = this;
        Cam = Scene.Camera;
    }

    protected override async void OnUpdate()
    {
		if ( PlayerController.Local.IsValid() && PlayerController.Local.IsProxy )
			return;

        base.OnUpdate();

        await Task.FrameEnd();

		var local = PlayerController.Local;

        if ( !local.IsValid() )
            return;

        Transform.Position = new Vector3( Cam.Transform.Position.x, local.Transform.Position.y, 0f );
    }
}
