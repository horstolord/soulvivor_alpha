using System;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Shared "hold to charge, release to spend" primitive. One charge in flight at a time per actor,
/// gated through ActorStateType.Charging exactly like Dodge/Block gate their own states.
///
/// Deliberately domain-agnostic: ramp rate and cost are supplied by the caller per-call rather than
/// hardcoded here, so the same component drives charged attacks (Swiftness/Might) now and force-action
/// (Acuity/Will) later without needing to know which domain it's charging for.
///
/// Cost is paid incrementally as Charge01 rises (via direct StatSheet mutation, same pattern
/// DodgeControl already uses for its stamina cost) rather than lump-sum, so an interrupted charge
/// only ever costs what was actually gathered.
/// </summary>
public sealed class ChargeControl : Component
{
	[Property] public float MoveSpeedPenaltyPercent { get; set; } = -25f;

	[Property] public bool IsCharging { get; private set; } = false;
	public float Charge01 { get; private set; }

	private Actor _actor;
	private StatSheet _statSheet;

	private float _rampRate;      // 1 / seconds-to-reach-Charge01=1
	private float _totalStaminaCost;
	private float _totalEnergyCost;
	private float _paidStamina;
	private float _paidEnergy;
	private StatModifier _moveSpeedModifier;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		_statSheet ??= _actor?.StatSheet ?? Components.GetInAncestorsOrSelf<StatSheet>();
	}

	protected override void OnUpdate()
	{
		if ( _actor == null )
		{
			CacheComponents();
			if ( _actor == null ) return;
		}

		// A banked charge clears on the same interrupt as an active one, whether or not we're
		// mid-ramp right now — checked independently so it still fires while just standing around
		// carrying a banked charge with nothing actively charging.
		if ( HasBankedCharge && IsActorInterrupted() )
			ClearBankedCharge();

		if ( !IsCharging ) return;

		// Staggered/Stunned/Dead mid-charge — lose it outright, keep whatever was already paid.
		if ( IsActorInterrupted() )
		{
			CancelCharge();
			return;
		}

		Charge01 = Math.Clamp( Charge01 + _rampRate * Time.Delta, 0f, 1f );
		PayIncrementalCost();
	}

	private bool IsActorInterrupted()
	{
		var state = _actor?.StateComp?.CurrentState;
		return state is ActorStateType.Staggered or ActorStateType.Stunned or ActorStateType.Dead;
	}

	public bool CanStartCharge()
	{
		if ( IsCharging ) return false;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState != ActorStateType.Idle ) return false;
		return true;
	}

	/// <summary>
	/// Starts a new charge. rampRate is 1/seconds-to-max — already attribute-resolved by the caller
	/// (e.g. Swiftness for physical attacks, Acuity for force-action later).
	/// totalStaminaCost/totalEnergyCost are this charge's OWN cost, paid gradually over the hold —
	/// separate from and additive to whatever cost the spent action itself pays on release.
	/// </summary>
	public bool StartCharge( float rampRate, float totalStaminaCost = 0f, float totalEnergyCost = 0f )
	{
		if ( !CanStartCharge() ) return false;

		IsCharging = true;
		Charge01 = 0f;
		_rampRate = MathF.Max( 0.01f, rampRate );
		_totalStaminaCost = totalStaminaCost;
		_totalEnergyCost = totalEnergyCost;
		_paidStamina = 0f;
		_paidEnergy = 0f;

		if ( _actor?.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Charging;

		if ( _statSheet?.MoveSpeed != null )
		{
			_moveSpeedModifier = new StatModifier( MoveSpeedPenaltyPercent, ModifierType.Percent, source: this );
			_statSheet.MoveSpeed.AddModifier( _moveSpeedModifier );
		}

		return true;
	}

	private void PayIncrementalCost()
	{
		if ( _statSheet == null ) return;

		float targetStamina = _totalStaminaCost * Charge01;
		float staminaDelta = targetStamina - _paidStamina;
		if ( staminaDelta > 0f )
		{
			_statSheet.CurrentStamina = MathF.Max( 0f, _statSheet.CurrentStamina - staminaDelta );
			_paidStamina = targetStamina;
		}

		float targetEnergy = _totalEnergyCost * Charge01;
		float energyDelta = targetEnergy - _paidEnergy;
		if ( energyDelta > 0f )
		{
			_statSheet.CurrentEnergy = MathF.Max( 0f, _statSheet.CurrentEnergy - energyDelta );
			_paidEnergy = targetEnergy;
		}
	}

	/// <summary>
	/// Ends the hold and returns whatever Charge01 was reached (0 if released instantly — the
	/// existing "tap = normal attack" case falls out of this for free). Resets state to Idle
	/// transiently; the caller's own action-start (CombatComponent.TryStartAttack, etc.) re-locks
	/// it a moment later in the same synchronous call, so nothing else ever observes the gap.
	/// </summary>
	public float ReleaseCharge()
	{
		float result = Charge01;
		EndChargeInternal();
		return result;
	}

	private void CancelCharge()
	{
		EndChargeInternal();
		Log.Info( "[ChargeControl] Charge interrupted." );
	}

	private void EndChargeInternal()
	{
		IsCharging = false;
		Charge01 = 0f;

		if ( _statSheet?.MoveSpeed != null && _moveSpeedModifier != null )
		{
			_statSheet.MoveSpeed.RemoveModifier( _moveSpeedModifier );
			_moveSpeedModifier = null;
		}

		// Guarded like DodgeControl/BlockControl — only reclaim Idle if we still own the state.
		// If something else (Stagger/Stun/Death) already overwrote it, leave that alone.
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Charging )
			_actor.StateComp.CurrentState = ActorStateType.Idle;
	}

	// ============ BANKING (shared by any action) ============
	// Force action will use "gather in advance, infuse the next valid action" instead of
	// hold-and-release. Fields are here so that pass doesn't need to touch this component's
	// shape again; the actual Bank()/consume wiring waits until Force Action itself is scoped.
	public bool HasBankedCharge { get; private set; }
	public float BankedCharge01 { get; private set; }
	/// <summary>Seconds a banked charge survives unspent. -1 = infinite (unused for now).</summary>
	[Property] public float BankedChargeLifetime { get; set; } = -1f;

	/// <summary>Banks the current charge instead of spending it immediately.</summary>
	public void Bank()
	{
		if ( !IsCharging ) return;

		BankedCharge01 = Charge01;
		HasBankedCharge = true;
		EndChargeInternal();
	}

	/// <summary>Reads the bank without consuming it, so callers can validate an action first.</summary>
	public bool TryGetBankedCharge( out float charge01 )
	{
		charge01 = BankedCharge01;
		return HasBankedCharge;
	}

	/// <summary>Consumes banked charge after the caller has successfully started its action.</summary>
	public bool TryConsumeBankedCharge( out float charge01 )
	{
		if ( !HasBankedCharge )
		{
			charge01 = 0f;
			return false;
		}

		charge01 = BankedCharge01;
		ClearBankedCharge();
		return true;
	}

	/// <summary>Clears any banked charge — called on the same Stagger/Stun/Death interrupt as an active charge.</summary>
	public void ClearBankedCharge()
	{
		HasBankedCharge = false;
		BankedCharge01 = 0f;
	}
}
