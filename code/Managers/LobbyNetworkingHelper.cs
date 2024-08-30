using System.Data;
using Sandbox;
using Sandbox.Network;
using System.Threading.Tasks;

public sealed class LobbyNetworkingHelper : Component, Component.INetworkListener
{
	[Property] public GameObject PlayerPrefab { get; set; }

	protected override async Task OnLoad()
	{
		if ( !GameNetworkSystem.IsActive )
		{
			GameNetworkSystem.CreateLobby();
			await Task.MainThread();
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
