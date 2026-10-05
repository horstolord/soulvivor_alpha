using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Systems;

namespace Sandbox.Code.Presentation;

/// <summary>
/// Floating damage numbers. One instance per scene (put it on your HUD / manager object).
///
/// Cost model: no GameObjects, no Razor panels, no layout. A fixed ring buffer of structs is projected
/// to screen and drawn immediate-mode through Camera.Hud each frame. Zero allocations per frame; one
/// small string per hit. Hits on the same victim inside MergeWindow collapse into one number, so
/// novas / beams / multicast don't spam.
/// </summary>
public sealed class DamageNumbers : Component
{
	[Property] public float Lifetime { get; set; } = 0.9f;
	[Property] public float RiseSpeed { get; set; } = 90f;      // world units/sec at spawn, decays to 0
	[Property] public float HeadHeight { get; set; } = 72f;     // spawn height above the victim's origin
	[Property] public float MergeWindow { get; set; } = 0.15f;
	[Property] public float MaxDistance { get; set; } = 2500f;
	[Property] public float BaseFontSize { get; set; } = 22f;
	[Property] public float MinAmount { get; set; } = 0.5f;     // below this, no number (except block/evade)
	[Property] public string FontName { get; set; } = "Poppins";

	private const int Capacity = 64;
	private const float PopTime = 0.12f;

	private struct Entry
	{
		public string Text;          // null = slot unused
		public Actor Victim;
		public Vector3 Pos, Vel;
		public float Born, LastHit, Amount;
		public bool Crit, Evaded, Blocked;
		public Color Color;
	}

	private readonly Entry[] _pool = new Entry[Capacity];
	private int _head;

	protected override void OnEnabled() => Actor.Damaged += OnDamaged;
	protected override void OnDisabled() => Actor.Damaged -= OnDamaged;

	// ============ SPAWN ============
	private void OnDamaged( DamageEvent ev )
	{
		if ( !ev.Victim.IsValid() ) return;
		if ( !ev.Evaded && !ev.Blocked && ev.Amount < MinAmount ) return;

		float now = Time.Now;
		var color = ColorFor( ev );

		// Merge into a live number on the same victim (same kind of hit) instead of stacking a new one.
		if ( !ev.Evaded )
		{
			for ( int i = 0; i < Capacity; i++ )
			{
				ref var e = ref _pool[i];
				if ( e.Text == null || e.Victim != ev.Victim || e.Evaded ) continue;
				if ( e.Crit != ev.IsCrit || e.Blocked != ev.Blocked ) continue;
				if ( now - e.LastHit > MergeWindow ) continue;

				e.Amount += ev.Amount;
				e.Text = Format( e.Amount, e.Crit, e.Blocked );
				e.LastHit = now;
				e.Born = now;          // keep it alive while hits keep landing
				e.Vel = e.Vel.WithZ( RiseSpeed * 0.5f );
				return;
			}
		}

		// Small horizontal scatter so simultaneous numbers on different hits don't overlap exactly.
		var jitter = new Vector3( Random.Shared.Float( -14f, 14f ), Random.Shared.Float( -14f, 14f ), 0f );

		_pool[_head] = new Entry
		{
			Text = ev.Evaded ? "Evaded" : Format( ev.Amount, ev.IsCrit, ev.Blocked ),
			Victim = ev.Victim,
			Pos = ev.Victim.WorldPosition + Vector3.Up * HeadHeight + jitter,
			Vel = new Vector3( 0f, 0f, RiseSpeed ),
			Born = now,
			LastHit = now,
			Amount = ev.Amount,
			Crit = ev.IsCrit,
			Evaded = ev.Evaded,
			Blocked = ev.Blocked,
			Color = color
		};
		_head = (_head + 1) % Capacity; // overwrites the oldest when full
	}

	private static string Format( float amount, bool crit, bool blocked )
	{
		var text = ((int)MathF.Round( amount )).ToString();
		if ( crit ) text += "!";
		return blocked ? $"({text})" : text;
	}

	private static Color ColorFor( DamageEvent ev )
	{
		if ( ev.Evaded || ev.Blocked ) return new Color( 0.75f, 0.75f, 0.78f );
		if ( ev.Victim is Player ) return new Color( 1f, 0.3f, 0.3f );

		var tags = ev.Tags;
		if ( tags != null )
		{
			if ( tags.Contains( AttackTag.Fire ) ) return new Color( 1f, 0.55f, 0.15f );
			if ( tags.Contains( AttackTag.Frost ) ) return new Color( 0.55f, 0.85f, 1f );
			if ( tags.Contains( AttackTag.Air ) ) return new Color( 0.8f, 1f, 0.9f );
			if ( tags.Contains( AttackTag.Earth ) ) return new Color( 0.75f, 0.6f, 0.35f );
		}
		return ev.IsCrit ? new Color( 1f, 0.85f, 0.2f ) : Color.White;
	}

	// ============ DRAW ============
	protected override void OnUpdate()
	{
		var cam = Scene.Camera;
		if ( cam == null ) return;

		float now = Time.Now;
		float dt = Time.Delta;
		var camPos = cam.WorldPosition;
		float maxDistSq = MaxDistance * MaxDistance;

		for ( int i = 0; i < Capacity; i++ )
		{
			ref var e = ref _pool[i];
			if ( e.Text == null ) continue;

			float age = now - e.Born;
			if ( age >= Lifetime ) { e.Text = null; continue; }

			// Eased rise: fast at spawn, settles near the end.
			e.Pos += e.Vel * (1f - age / Lifetime) * dt;

			float distSq = (e.Pos - camPos).LengthSquared;
			if ( distSq > maxDistSq ) continue;

			var screen = cam.PointToScreenPixels( e.Pos, out bool behind );
			if ( behind ) continue;

			// Pop on spawn / on every merged hit, fade over the last 40% of life.
			float sinceHit = now - e.LastHit;
			float pop = sinceHit < PopTime ? 1f + 0.35f * (1f - sinceHit / PopTime) : 1f;
			float fade = age > Lifetime * 0.6f ? 1f - (age - Lifetime * 0.6f) / (Lifetime * 0.4f) : 1f;
			float perspective = Math.Clamp( 800f / MathF.Sqrt( distSq ), 0.6f, 1.25f );

			// Quantised so the text cache sees a handful of variants, not a new one every frame.
			float size = MathF.Round( BaseFontSize * (e.Crit ? 1.5f : 1f) * pop * perspective / 2f ) * 2f;
			float alpha = MathF.Round( fade * 8f ) / 8f;

			DrawLabel( cam, e.Text, screen, e.Color.WithAlpha( alpha ), size );
		}
	}

	/// <summary>The only place that touches the Hud API, so swapping the renderer later is a one-method change.</summary>
	private void DrawLabel( CameraComponent cam, string text, Vector2 pos, Color color, float size )
	{
		var shadow = new TextRendering.Scope( text, Color.Black.WithAlpha( color.a * 0.8f ), size, FontName, 700 );
		cam.Hud.DrawText( shadow, pos + new Vector2( 1.5f, 1.5f ) );
		cam.Hud.DrawText( new TextRendering.Scope( text, color, size, FontName, 700 ), pos );
	}
}
