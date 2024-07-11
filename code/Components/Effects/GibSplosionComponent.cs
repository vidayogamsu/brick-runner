using Sandbox;

namespace Vidya;


public class GibSplosionComponent : Component
{
    [Property] public GameObject GibPrefab { get; set; }
    [Property] public uint GibCount { get; set; } = 10;

    [Property] public float ForceMin { get; set; } = 500;
    [Property] public float ForceMax { get; set; } = 1000;

    [Property] public float ScaleMin { get; set; } = 0.65f;
    [Property] public float ScaleMax { get; set; } = 1.1f;


    protected override void OnStart()
    {
        base.OnStart();

        GameObject.Destroy();

        if ( !GibPrefab.IsValid() )
            return;

        for ( var i = 0; i < GibCount; i++ )
            SpawnGib();
    }

    public void SpawnGib()
    {
        var gib = GibPrefab.Clone( Transform.Position );
        gib.Transform.Scale = Vector3.One * Game.Random.Float( ScaleMin, ScaleMax );

        if ( gib.Components.TryGet<Rigidbody>( out var rb, FindMode.EnabledInSelfAndDescendants ) )
            rb.Velocity = Vector3.Random.WithX( 0f ).Normal * Game.Random.Float( ForceMin, ForceMax );
    }
}
