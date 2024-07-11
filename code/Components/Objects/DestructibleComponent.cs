using Sandbox;

namespace Vidya;


public sealed class DestructibleComponent : TemporaryComponent
{
    /// <summary>
    /// The prefab to spawn when destroyed.
    /// </summary>
    [Property] public GameObject DestroyEffect { get; set; }

    [Property] public bool Invulnerable { get; set; } = false;


    public void DoEffect()
    {
        if ( !DestroyEffect.IsValid() )
            return;

        DestroyEffect.Clone( GameObject.GetBounds().Center );
    }
}
