using System;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public enum ForceActionType { Slide, Jump, Imbue, Kick }

/// <summary>
/// Context-sensitive quick casts. Gathers charge (hold key -> bank), tracks the actor's active
/// element (last element cast), and spends the banked charge on the next Force Action as
/// [Force(element) + Method(action)].
/// </summary>
public sealed class ForceActionControl : Component, PlayerController.IEvents
{
	[Property] public string GatherKey { get; set; } = "G";
	[Property] public RuneElementTag DefaultElement { get; set; } = RuneElementTag.Fire;
	/// <summary>Below this, a released gather is treated as a cancelled tap, not banked.</summary>
	[Property] public float MinBankCharge { get; set; } = 0.05f;

	public RuneElementTag? ActiveElement { get; private set; }
	public RuneElementTag Element => ActiveElement ?? DefaultElement;

	private ChargeControl _charge;
	private SpellComponent _spells;
	private CombatComponent _combat;

	protected override void OnStart() => Cache();

	protected override void OnDestroy()
	{
		if ( _spells != null ) _spells.OnSpellCast -= HandleSpellCast;
		if ( _combat != null ) _combat.OnHitPhaseStarted -= HandleHitPhaseStarted;
	}

	private void Cache()
	{
		if ( _combat == null )
		{
			_combat = Components.GetInAncestorsOrSelf<CombatComponent>();
			if ( _combat != null ) _combat.OnHitPhaseStarted += HandleHitPhaseStarted;
		}
		_charge ??= Components.GetInAncestorsOrSelf<ChargeControl>();
		if ( _spells == null )
		{
			_spells = Components.GetInAncestorsOrSelf<SpellComponent>() ?? Components.GetOrCreate<SpellComponent>();
			if ( _spells != null ) _spells.OnSpellCast += HandleSpellCast;
		}
	}

	private void HandleSpellCast( SpellContext ctx )
	{
		if ( ctx.PrimaryElement.HasValue ) ActiveElement = ctx.PrimaryElement;
	}
	private void HandleHitPhaseStarted( AttackContext ctx, HitPhaseDef phase )
	{
		if ( !ctx.Tags.Contains( AttackTag.Kick ) ) return;

		// Spawn where the kick's hitbox is, so the blast and the hit read as one event.
		var shape = phase.Shapes.FirstOrDefault();
		var origin = ctx.Origin + (shape != null ? ctx.Facing * shape.LocalOffset : Vector3.Zero);
		TryTrigger( ForceActionType.Kick, origin, ctx.AimDirection );
	}
	
	protected override void OnUpdate()
	{
		Cache();
		if ( _charge == null || _spells == null ) return;

		bool held = Input.Keyboard.Down( GatherKey );

		if ( _charge.IsCharging )
		{
			if ( held ) return;
			if ( _charge.Charge01 >= MinBankCharge ) _charge.Bank();
			else _charge.ReleaseCharge();
			return;
		}

		if ( held && !_charge.HasBankedCharge && _charge.CanStartCharge() )
			_charge.StartCharge( _spells.GetChargeRampRate() );
	}

	void PlayerController.IEvents.OnJumped() => TryTrigger( ForceActionType.Jump );

	/// <summary>Spends the banked charge on this action's spell. No-op (false) without a bank.</summary>
	public bool TryTrigger( ForceActionType action, Vector3? origin = null, Vector3? aim = null )
	{
		Cache();
		if ( _charge == null || _spells == null ) return false;
		if ( !_charge.TryGetBankedCharge( out var charge01 ) ) return false;

		var runes = new List<RuneDef>();
		var force = RuneLibrary.ForceFor( Element );
		if ( force != null ) runes.Add( force );
		runes.Add( MethodFor( action ) );

		var aimDir = aim ?? Scene.Camera?.WorldRotation.Forward ?? GameObject.WorldRotation.Forward;
		var flat = aimDir.WithZ( 0f );
		flat = flat.LengthSquared > 0.001f ? flat.Normal : GameObject.WorldRotation.Forward;
		var spawn = origin ?? GameObject.WorldPosition + Vector3.Up * 60f + flat * 40f;

		if ( !_spells.CastInstant( runes, spawn, aimDir, charge01 ) ) return false;

		_charge.TryConsumeBankedCharge( out _ );
		return true;
	}

	// Switch, not a dictionary/lambda: static state breaks on hotreload.
	private static RuneDef MethodFor( ForceActionType action ) => action switch
	{
		ForceActionType.Slide => RuneLibrary.Fleetness,
		ForceActionType.Jump => RuneLibrary.Launch,
		ForceActionType.Imbue => RuneLibrary.ImbueMethod,
		ForceActionType.Kick => RuneLibrary.BlastMethod,
		_ => null
	};
}
