using System;

namespace Sandbox.Code.World;

/// <summary>
/// Pure measurement: how dark is it where this actor stands. Lives on every Actor (player included,
/// so UI / third eye can read it) and applies no gameplay effect by itself — see DarknessScaling.
///
/// Samples at SampleHz with jitter so actors never sample on the same frame. The published value is
/// quantized to 0.05 steps so downstream stat modifiers only change when the step changes.
/// </summary>
public sealed class FieldExposure : Component
{
	[Property] public float SampleHz { get; set; } = 2f;

	/// <summary>Sample above the feet so a floor sitting exactly on a cell border doesn't blend in the layer below.</summary>
	[Property] public float SampleHeight { get; set; } = 48f;

	/// <summary>0 = fully lit, 1 = pitch dark. World light only (FieldChannel.Light), quantized to 0.05.</summary>
	public float Darkness { get; private set; }

	private const float Step = 0.05f;

	// Default TimeUntil reads as elapsed, so the first update samples immediately.
	private TimeUntil _nextSample;

	protected override void OnUpdate()
	{
		if ( !_nextSample ) return;

		float interval = 1f / MathF.Max( 0.1f, SampleHz );
		_nextSample = interval * Random.Shared.Float( 0.75f, 1.25f );
		Refresh();
	}

	/// <summary>Forces a sample now. Single entry point, so a central budgeted tick can take over later.</summary>
	public void Refresh()
	{
		var grid = FieldSystem.Current?.Grid;
		if ( grid == null ) return;

		float light = grid.SampleSmooth( FieldChannel.Light, GameObject.WorldPosition + Vector3.Up * SampleHeight );
		float darkness = MathF.Round( (1f - light) / Step ) * Step;
		Darkness = Math.Clamp( darkness, 0f, 1f );
	}
}
