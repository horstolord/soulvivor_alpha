using System;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>
/// Per-actor proc host. Anything that grants procs (items, boons, elite affixes, talents) calls
/// AddProc with itself as the source and RemoveProcsFromSource when it goes away — same shape as
/// Stat.AddModifier / RemoveModifiersFromSource. Created by Actor.OnStart; no prefab wiring.
/// </summary>
public sealed class ProcControl : Component
{
	private sealed class ActiveProc
	{
		public ProcDef Def;
		public object Source;
		public float Potency = 1f;
		public bool Removed;
		public TimeSince SinceFired = 999f;
		public TimeUntil NextInterval;
	}

	/// <summary>Raised after a proc's effects ran. Hook for VFX / UI proc icons.</summary>
	public event Action<ProcDef, ProcEvent> OnProcFired;

	private readonly List<ActiveProc> _procs = new();
	private Actor _actor;
	private int _dispatchDepth;
	private bool _needsCompact;
	private int _intervalCount;

	// ============ PUBLIC API ============

	public void AddProc( ProcDef def, object source = null, float potency = 1f )
	{
		if ( def == null ) return;

		var proc = new ActiveProc { Def = def, Source = source, Potency = potency };
		if ( def.Trigger == ProcTrigger.Interval )
		{
			proc.NextInterval = MathF.Max( 0.05f, def.IntervalSeconds );
			_intervalCount++;
		}
		_procs.Add( proc ); // safe mid-dispatch: loops only walk the count captured when they started
	}

	/// <summary>Removes every proc granted by this source. Returns how many were removed.</summary>
	public int RemoveProcsFromSource( object source )
	{
		int removed = 0;
		foreach ( var proc in _procs )
		{
			if ( proc.Removed || proc.Source != source ) continue;
			proc.Removed = true;
			if ( proc.Def.Trigger == ProcTrigger.Interval ) _intervalCount--;
			removed++;
		}

		if ( removed > 0 )
		{
			_needsCompact = true;
			if ( _dispatchDepth == 0 ) Compact();
		}
		return removed;
	}
	/// <summary>Sets the potency of every proc granted by this source (boon stacks etc.) without resetting ICDs.</summary>
	public void SetPotencyForSource( object source, float potency )
	{
		foreach ( var proc in _procs )
		{
			if ( !proc.Removed && proc.Source == source )
				proc.Potency = potency;
		}
	}
	/// <summary>Feed an event to every matching proc on this actor.</summary>
	public void Raise( in ProcEvent ev )
	{
		if ( _procs.Count == 0 ) return;
		CacheActor();
		if ( _actor == null ) return;

		_dispatchDepth++;
		try
		{
			int count = _procs.Count;
			for ( int i = 0; i < count; i++ )
			{
				var proc = _procs[i];
				if ( proc.Removed || proc.Def.Trigger != ev.Trigger ) continue;
				TryFire( proc, ev );
			}
		}
		finally
		{
			EndDispatch();
		}
	}

	/// <summary>
	/// Single entry point for damage-derived events. Actor.ApplyDamage calls this once per hit with the
	/// same DamageEvent it hands to Actor.Damaged, so both stay in sync. ProcDepth is inherited from the
	/// damage, so hits caused by a proc's effects arrive as chained events.
	/// </summary>
	public static void RaiseDamageEvents( in DamageEvent ev )
	{
		var victim = ev.Victim;
		if ( !victim.IsValid() ) return;

		var attacker = CombatMath.ResolveActor( ev.Source );
		var position = victim.GameObject.WorldPosition;

		if ( ev.Evaded )
		{
			victim.Procs?.Raise( Build( ProcTrigger.Evaded, ev, attacker, position ) );
			return;
		}

		victim.Procs?.Raise( Build( ProcTrigger.HitTaken, ev, attacker, position ) );

		if ( ev.Blocked )
			victim.Procs?.Raise( Build( ProcTrigger.Blocked, ev, attacker, position ) );

		if ( attacker.IsValid() && attacker != victim )
		{
			attacker.Procs?.Raise( Build( ProcTrigger.HitDealt, ev, victim, position ) );

			if ( ev.IsCrit )
				attacker.Procs?.Raise( Build( ProcTrigger.Crit, ev, victim, position ) );

			if ( ev.Killed )
				attacker.Procs?.Raise( Build( ProcTrigger.Kill, ev, victim, position ) );
		}

		if ( ev.Killed )
			victim.Procs?.Raise( Build( ProcTrigger.Death, ev, attacker, position ) );
	}

	private static ProcEvent Build( ProcTrigger trigger, in DamageEvent ev, Actor other, Vector3 position )
	{
		return new ProcEvent
		{
			Trigger = trigger,
			Other = other,
			Position = position,
			Amount = ev.Amount,
			IsCrit = ev.IsCrit,
			Tags = ev.Tags,
			ProcDepth = ev.ProcDepth
		};
	}

	// ============ TICK (Interval procs) ============

	protected override void OnUpdate()
	{
		if ( _intervalCount <= 0 ) return;
		CacheActor();
		if ( _actor == null ) return;
		if ( _actor.StateComp?.CurrentState == ActorStateType.Dead ) return;

		_dispatchDepth++;
		try
		{
			int count = _procs.Count;
			for ( int i = 0; i < count; i++ )
			{
				var proc = _procs[i];
				if ( proc.Removed || proc.Def.Trigger != ProcTrigger.Interval ) continue;

				bool due = proc.NextInterval;
				if ( !due ) continue;

				proc.NextInterval = MathF.Max( 0.05f, proc.Def.IntervalSeconds );
				TryFire( proc, new ProcEvent { Trigger = ProcTrigger.Interval, Position = GameObject.WorldPosition } );
			}
		}
		finally
		{
			EndDispatch();
		}
	}

	// ============ INTERNALS ============

	private void TryFire( ActiveProc proc, in ProcEvent ev )
	{
		var def = proc.Def;

		if ( ev.ProcDepth > def.MaxProcDepth ) return;
		if ( !def.Condition.Matches( _actor, ev ) ) return;
		if ( proc.SinceFired < def.InternalCooldown ) return;

		float chance = def.Chance;
		if ( ev.ProcDepth > 0 && def.ChainFalloffExponent > 0f )
			chance /= MathF.Pow( 1f + ev.ProcDepth, def.ChainFalloffExponent );
		if ( chance < 100f && Random.Shared.NextSingle() * 100f >= chance ) return;

		proc.SinceFired = 0f;

		var ctx = new ProcContext { Def = def, Owner = _actor, Event = ev, Potency = proc.Potency };
		foreach ( var effect in def.Effects )
		{
			if ( effect == null ) continue;
			try
			{
				effect.Execute( ctx );
			}
			catch ( Exception e )
			{
				// A broken effect must never take ApplyDamage down with it.
				// If the s&box whitelist rejects Log here, swap in Console.WriteLine as before.
				Log.Error( $"[ProcControl] An effect on proc '{def.Id}' threw: {e.Message}" );
			}
		}

		OnProcFired?.Invoke( def, ev );
	}

	private void EndDispatch()
	{
		_dispatchDepth--;
		if ( _dispatchDepth == 0 && _needsCompact )
			Compact();
	}

	private void Compact()
	{
		_procs.RemoveAll( p => p.Removed );
		_needsCompact = false;
	}

	private void CacheActor()
	{
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
	}
}
