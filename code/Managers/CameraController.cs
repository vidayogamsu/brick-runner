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


    [RequireComponent]
    public CameraComponent Cam { get; set; }


    public PlayerController Player { get; set; }


    protected override async void OnUpdate()
    {
        base.OnUpdate();

        await Task.FrameEnd();

        if ( !Player.IsValid() )
            return;

        Transform.Position = new Vector3( 256f, Player.Transform.Position.y, 0f );
    }
}
