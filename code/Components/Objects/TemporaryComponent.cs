using Sandbox;
using Sandbox.Events;

namespace Vidya;


/// <summary>
/// Objects with this component will be removed when a level is restarted.
/// </summary>
public class TemporaryComponent : Component, IGameEventHandler<RoundCleanup>
{
	void IGameEventHandler<RoundCleanup>.OnGameEvent( RoundCleanup eventArgs )
	{
		GameObject.Destroy();
	}
}
