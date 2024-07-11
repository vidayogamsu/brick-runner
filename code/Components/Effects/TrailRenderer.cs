using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sandbox;

namespace Helichan;


/// <summary>
/// A component that creates a LineRenderer and automatically adds points to it.
/// </summary>
public class TrailRenderer : Component
{
    [Property, Range( 0, 2048, 1 )] public int MaxLines { get; set; } = 120;
    [Property, Range( 0f, 1f )] public float Frequency { get; set; } = 0.015f;

    [Property] public Color ColorStart { get; set; } = Color.White;
    [Property] public Color ColorEnd { get; set; } = Color.White;
    [Property] public float LineWidth { get; set; } = 5f;

    private bool Fading { get; set; } = false;
    [Property] public float FadeTime { get; set; } = 4f;

    public RealTimeUntil NextLine { get; set; } = -1;

    public GameObject FirstPoint { get; set; }

    public LineRenderer LineRenderer { get; set; }


    protected override void OnStart()
    {
        LineRenderer ??= Components.Create<LineRenderer>();
        LineRenderer.Points ??= new();
        var colorStart = new Gradient.ColorFrame( 0f, ColorStart );
        var colorEnd = new Gradient.ColorFrame( 1f, ColorEnd );

        LineRenderer ??= Components.Create<LineRenderer>();
        LineRenderer.Color = new( colorStart, colorEnd );
        LineRenderer.Width = LineWidth;
        LineRenderer.Opaque = false;

        FirstPoint = Scene.CreateObject();
        FirstPoint.SetParent( GameObject, false );
        FirstPoint.Transform.LocalPosition = Vector3.Zero;

        LineRenderer.Points.Add( FirstPoint );
    }

    protected override void OnUpdate()
    {
        if ( Fading )
        {
            if ( !LineRenderer.IsValid() )
            {
                Destroy();
                return;
            }

            // Copy colors with reduced alpha.
            var hasAlpha = false;
            var alphaSubtract = 1f / MathF.Max( 0.01f, FadeTime ) * Time.Delta;

            var newColors = new List<Gradient.ColorFrame>();

            foreach ( var color in LineRenderer.Color.Colors )
            {
                var a = MathF.Max( 0f, color.Value.a - alphaSubtract );
                var newColor = new Gradient.ColorFrame( color.Time, color.Value.WithAlpha( a ) );

                newColors.Add( newColor );

                // Don't self-destruct if a color still has alpha.
                if ( a > 0 ) hasAlpha = true;
            }

            if ( hasAlpha )
            {
                // Some keyframes are still visible.
                LineRenderer.Color = LineRenderer.Color.WithFrames( newColors.ToImmutableList() );
            }
            else
            {
                // No visible keyframes left.
                Destroy();
            }
        }
        else
        {
            if ( NextLine )
                AddPoint();
        }
    }

    protected override void OnDestroy()
    {
        if ( FirstPoint.IsValid() )
            FirstPoint.Destroy();

        if ( LineRenderer is not null )
        {
            if ( LineRenderer.Points is not null )
            {
                foreach ( var point in LineRenderer.Points )
                    if ( point.IsValid() ) point.Destroy();
            }

            if ( LineRenderer.IsValid() )
                LineRenderer.Destroy();
        }
    }


    public void FadeAway( float duration = 1f, bool stopFollowing = true )
    {
        if ( stopFollowing && FirstPoint.IsValid() )
            FirstPoint.SetParent( null, true );

        if ( !LineRenderer.IsValid() )
        {
            Destroy();
            return;
        }

        FadeTime = duration;
        Fading = true;
    }


    public void AddPoint()
    {
        NextLine = Frequency;

        if ( !LineRenderer.IsValid() || LineRenderer.Points is null )
            return;

        var point = Scene.CreateObject( true );
        point.Transform.Position = Transform.Position;
        point.Name = "Line Point";

        LineRenderer.Points.Insert( 1, point );

        // First in first out.
        var count = LineRenderer.Points.Count;

        if ( count > 1 && count > MaxLines + 1 )
        {
            var toRemove = LineRenderer.Points.ElementAtOrDefault( count - 1 );

            if ( toRemove.IsValid() )
            {
                toRemove.Destroy();
                LineRenderer.Points.RemoveAt( count - 1 );
            }
        }
    }
}
