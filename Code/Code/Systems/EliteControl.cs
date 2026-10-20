using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Makes an actor an elite: applies its affixes' stat modifiers and procs, the baseline elite HP bonus, the
/// reach that comes with a bigger body, and the visuals (outline + tint). Created disabled-state by MobSpawner with
/// the affix list already set; waits for Actor.OnReady because InitializeFromRegistry replaces the Stat objects
/// (and with them any modifier added earlier).
/// </summary>
public sealed class EliteControl : Component
{
	/// <summary>Max health bonus per affix, added together (n affixes = +n x this percent). Placeholder.</summary>
	[Property] public float HealthPercentPerAffix { get; set; } = 50f;

	[Property] public float OutlineWidth { get; set; } = 1f;

	/// <summary>How strongly the body is tinted toward the primary affix colour (0..1).</summary>
	[Property] public float TintStrength { get; set; } = 0.35f;

	/// <summary>Affixes in roll order. The first one drives the outline/tint colour. Splitters read this to pass them on.</summary>
	public IReadOnlyList<EliteAffixDef> Affixes => _affixes;

	private readonly List<EliteAffixDef> _affixes = new();
	private readonly List<(Stat Stat, StatModifier Modifier)> _applied = new();
	private Actor _actor;
	private bool _statsApplied;

	public void SetAffixes( IEnumerable<EliteAffixDef> affixes )
	{
		_affixes.Clear();
		if ( affixes != null ) _affixes.AddRange( affixes );
	}

	protected override void OnStart()
	{
		_actor = Components.GetInAncestorsOrSelf<Actor>();
		if ( _actor == null || _affixes.Count == 0 )
		{
			Enabled = false;
			return;
		}

		ApplyName();
		ApplyVisuals();

		if ( _actor.IsReady )
			ApplyStats();
		else
			_actor.OnReady += ApplyStats;
	}

	protected override void OnDestroy()
	{
		if ( _actor != null )
			_actor.OnReady -= ApplyStats;
	}

	private void ApplyStats()
	{
		if ( _statsApplied ) return;
		_statsApplied = true;
		_actor.OnReady -= ApplyStats;

		var sheet = _actor.StatSheet;
		if ( sheet == null || sheet.Armor == null || _actor.Procs == null )
		{
			Log.Warning( $"[EliteControl] {GameObject.Name} isn't initialized — can't apply affixes." );
			return;
		}

		foreach ( var affix in _affixes )
		{
			StatModApplier.Apply( sheet, affix.Modifiers, 1f, this, _applied, affix.DisplayName );
			foreach ( var proc in affix.Procs )
				_actor.Procs.AddProc( proc, source: this );
		}

		// Elite baseline: more HP per affix, and reach that follows the bigger body (the clone was scaled by
		// MobSpawner; Range scales melee hit shapes and the AI's stop/attack distances).
		var baseline = new List<ModData>
		{
			new ModData( "MaxHealth", HealthPercentPerAffix * _affixes.Count, ModifierType.Percent )
		};

		float bodyScale = (_actor as Enemy)?.SpawnScale ?? 1f;
		if ( MathF.Abs( bodyScale - 1f ) > 0.001f )
			baseline.Add( new ModData( "Range", (bodyScale - 1f) * 100f, ModifierType.Percent ) );

		StatModApplier.Apply( sheet, baseline, 1f, this, _applied, "Elite" );

		sheet.RecalculateDerivedStats();
		sheet.FillCurrentPoolsToMax();

		Log.Info( $"[EliteControl] {GameObject.Name} is elite: {string.Join( ", ", _affixes.Select( a => a.DisplayName ) )} " +
		          $"(HP={sheet.MaxHealth.Value:F0}, scale x{bodyScale:F2})" );
	}

	private void ApplyName()
	{
		GameObject.Name = $"{string.Join( " ", _affixes.Select( a => a.DisplayName ) )} {GameObject.Name}";
	}

	private void ApplyVisuals()
	{
		var color = _affixes[0].OutlineColor;
		var renderer = Components.GetInChildren<SkinnedModelRenderer>();

		// Host the outline on the renderer's own GameObject: it may only collect renderers at/below its own object.
		var host = renderer.IsValid() ? renderer.GameObject : GameObject;
		var outline = host.Components.GetOrCreate<HighlightOutline>();
		outline.Color = color;
		outline.ObscuredColor = color.WithAlpha( 0.4f ); // hidden behind walls, on purpose
		outline.Width = OutlineWidth;
		Log.Info( $"[EliteControl] Outline hosted on '{host.Name}' (width {OutlineWidth})." );

		if ( renderer.IsValid() )
			renderer.Tint = Color.Lerp( Color.White, color, TintStrength );
	}
}
		
