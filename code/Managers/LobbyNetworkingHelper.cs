using System.Data;
using Sandbox;
using Sandbox.Network;

public sealed class LobbyNetworkingHelper : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }

	protected override void OnStart()
	{
		if ( !GameNetworkSystem.IsActive )
		{
			GameNetworkSystem.CreateLobby();
		}
	}

	public void OnActive( Connection connection )
	{
		if ( !PlayerPrefab.IsValid() )
			return;
		
		var player = PlayerPrefab.Clone( Transform.World );

		player.NetworkSpawn( connection );

		if ( Connection.All.Count() > 1 )
		{
			Scene.LoadFromFile( "scenes/networking.scene" );
		}
	}
}
