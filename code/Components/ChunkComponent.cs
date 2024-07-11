using Sandbox;
using Vidya;


public class ChunkComponent : TemporaryComponent
{
    /// <summary>
    /// Where next to place a chunk in front of this one from its origin.
    /// </summary>
    [Property] public float Width { get; set; }
}
