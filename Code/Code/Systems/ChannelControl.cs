using System;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public enum ChannelEndReason
{
	Released,
	Interrupted,
	OutOfResources,
	Expired,
	Cancelled
}

/// <summary>Tuning for one channel. Costs are per second; they are paid in per-tick chunks.</summary>
public sealed class ChannelSpec
{
	/// <summary>Seconds between ticks at 1.0x speed (callers pass the speed-scaled interval to Begin).</summary>
	public float TickInterval = 0.3f;
	public float HealthPerSecond;
	public float StaminaPerSecond;
	public float EnergyPerSecond;
	/// <summary>Percent modifier on MoveSpeed while channeling (mirrors ChargeControl/BlockControl).</summary>
	public float MoveSpeedPenaltyPercent = -40f;
	/// <summary>Hard cap in seconds. 0 or less = until released / out of resources.</summary>
	public float MaxDuration;
	/// <summary>Regular-swing lockout applied when the channel ends.</summary>
	public float EndLockout;
}

/// <summary>What a channel does. Implemented by the executor of whatever is being channeled (technique now, spell later).</summary>
public interface IChannelHandler
{
	void OnChannelStart();
	/// <summary>Called once immediately on Begin (tick 0), then every interval while held.</summary>
	void OnChannelTick( int tickIndex );
	void OnChannelEnd( ChannelEndReason reason );
}

/// <summary>
/// Shared "hold to keep it running" primitive — sibling of ChargeControl (which ramps and releases once).
/// One channel at a time per actor, gated through ActorStateType.Channeling like Dodge/Block/Charge gate theirs.
/// Domain-agnostic: the caller supplies the spec, the speed-scaled tick interval and a handler, so techniques
/// use it now and spells (held beams, a Channel modifier rune) can use it later without changes here.
///
/// Input is NOT read here — whoever owns the input calls Release() when the button comes up.
/// Ends on: Release, interrupt (Stagger/Stun/Death or the state being overwritten), can't pay a tick, MaxDuration.
/// </summary>
public sealed class ChannelControl : Component
{
	public bool IsChanneling { get; private set; }
	public float Elapsed { get; private set; }
	public int TickCount => _tickIndex;

	private sealed class ChannelCost : ICostable
	{
		public float HealthCost { get; set; }
		public float StaminaCost { get; set; }
		public float EnergyCost { get; set; }
	}

	private Actor _actor;
	private StatSheet _statSheet;
	private ChannelSpec _spec;
	private IChannelHandler _handler;
	private readonly ChannelCost _tickCost = new();
	private StatModifier _moveSpeedModifier;
	private float _interval;
	private float _nextTick;
	private int _tickIndex;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	protected override void OnDestroy()
	{
		End( ChannelEndReason.Cancelled );
	}

	private void CacheComponents()
	{
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		_statSheet ??= _actor?.StatSheet ?? Components.GetInAncestorsOrSelf<StatSheet>();
	}

	protected override void OnUpdate()
	{
		if ( !IsChanneling ) return;

		if ( IsInterrupted() )
		{
			End( ChannelEndReason.Interrupted );
			return;
		}

		Elapsed += Time.Delta;

		if ( _spec.MaxDuration > 0f && Elapsed >= _spec.MaxDuration )
		{
			End( ChannelEndReason.Expired );
			return;
		}

		// A long frame can span several ticks; cap the catch-up so a hitch can't fire a burst.
		int guard = 0;
		while ( IsChanneling && Elapsed >= _nextTick && guard++ < 4 )
		{
			if ( !_actor.CanPayCost( _tickCost ) )
			{
				End( ChannelEndReason.OutOfResources );
				return;
			}

			_actor.PayCost( _tickCost );
			_handler.OnChannelTick( _tickIndex++ );
			_nextTick += _interval;
		}

		// If a hitch left us more than a tick behind, drop the backlog instead of fast-ticking for several frames.
		if ( IsChanneling && Elapsed - _nextTick > _interval )
			_nextTick = Elapsed;
	}

	private bool IsInterrupted()
	{
		var state = _actor?.StateComp?.CurrentState;
		return state is ActorStateType.Staggered or ActorStateType.Stunned or ActorStateType.Dead
			|| (state.HasValue && state.Value != ActorStateType.Channeling);
	}

	/// <summary>
	/// Starts a channel. tickInterval is already speed-scaled by the caller. The first tick fires immediately,
	/// so a quick tap still produces one hit. Returns false (nothing changed) if the actor isn't Idle or can't pay a tick.
	/// </summary>
	public bool Begin( ChannelSpec spec, IChannelHandler handler, float tickInterval, float costMultiplier = 1f )
	{
		CacheComponents();
		if ( IsChanneling || spec == null || handler == null || _actor == null ) return false;
		if ( _actor.StateComp != null && _actor.StateComp.CurrentState != ActorStateType.Idle ) return false;

		float interval = MathF.Max( 0.05f, tickInterval );
		_tickCost.HealthCost = spec.HealthPerSecond * interval * costMultiplier;
		_tickCost.StaminaCost = spec.StaminaPerSecond * interval * costMultiplier;
		_tickCost.EnergyCost = spec.EnergyPerSecond * interval * costMultiplier;
		if ( !_actor.CanPayCost( _tickCost ) ) return false;

		_spec = spec;
		_handler = handler;
		_interval = interval;
		_nextTick = interval;
		_tickIndex = 0;
		Elapsed = 0f;
		IsChanneling = true;

		if ( _actor.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Channeling;

		if ( _statSheet?.MoveSpeed != null && spec.MoveSpeedPenaltyPercent != 0f )
		{
			_moveSpeedModifier = new StatModifier( spec.MoveSpeedPenaltyPercent, ModifierType.Percent, source: this );
			_statSheet.MoveSpeed.AddModifier( _moveSpeedModifier );
		}

		_handler.OnChannelStart();
		_actor.PayCost( _tickCost );
		_handler.OnChannelTick( _tickIndex++ );
		return true;
	}

	/// <summary>Input layer calls this when the hold ends.</summary>
	public void Release() => End( ChannelEndReason.Released );

	public void Cancel() => End( ChannelEndReason.Cancelled );

	private void End( ChannelEndReason reason )
	{
		if ( !IsChanneling ) return;
		IsChanneling = false;

		if ( _statSheet?.MoveSpeed != null && _moveSpeedModifier != null )
		{
			_statSheet.MoveSpeed.RemoveModifier( _moveSpeedModifier );
			_moveSpeedModifier = null;
		}

		// Guarded like ChargeControl: only reclaim Idle if we still own the state.
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Channeling )
			_actor.StateComp.CurrentState = ActorStateType.Idle;

		var handler = _handler;
		var spec = _spec;
		_handler = null;
		_spec = null;

		if ( spec != null && spec.EndLockout > 0f )
			_actor?.Combat?.ResetSwing( spec.EndLockout );

		handler?.OnChannelEnd( reason );
	}
}
