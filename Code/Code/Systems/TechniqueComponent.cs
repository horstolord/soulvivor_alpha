using System;
using Sandbox;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>
/// Owns an actor's equipped techniques (the melee counterpart of SpellComponent): slots, per-technique cooldowns,
/// and the swing-reset rules. Sits on the Actor's GameObject next to CombatComponent.
///
/// Swing-reset rules (TechniqueDef.SwingReset = true):
///   - Idle / swing cooldown:   launches immediately (ignores the regular swing cooldown).
///   - Swing in Recovery:       clean cancel, the technique launches.
///   - Swing in Startup/Active: mistimed. The swing is cancelled and the player is locked out briefly;
///                              the technique does NOT fire (unless EarlyPressFiresTechnique is on).
/// </summary>
public sealed class TechniqueComponent : Component
{
	public const int SlotCount = 4;

	/// <summary>Seconds the actor cannot start a regular swing after a mistimed (too early) press.</summary>
	[Property] public float EarlyCancelLockout { get; set; } = 0.35f;

	/// <summary>Tuning switch: if true, a too-early press cancels the swing AND still fires the technique.</summary>
	[Property] public bool EarlyPressFiresTechnique { get; set; } = false;

	private readonly TechniqueDef[] _slots = new TechniqueDef[SlotCount];
	private readonly float[] _readyAt = new float[SlotCount];

	private Actor _actor;
	private CombatComponent _combat;
	private ChannelControl _channel;
	/// <summary>Slot currently being channeled, or -1.</summary>
	private int _channelSlot = -1;

	// Leap in flight. The airborne part is the *startup* of the slam attack, so the actor stays in
	// ActorStateType.Attacking (everything else is already gated on Idle) and landing hands over to the swing pipeline.
	private PlayerController _playerController;
	private int _leapSlot = -1;
	private TechniqueContext _leapCtx;
	private float _leapAirTime;
	private bool _leapLeftGround;
	private Vector3 _leapLastVelocity;

	protected override void OnStart()
	{
		CacheComponents();
	}

	protected override void OnUpdate()
	{
		UpdateLeap();

		// A channeled slot's cooldown starts when the channel ENDS (for any reason), not when it began.
		if ( _channelSlot < 0 ) return;

		_channel ??= _actor?.Components.Get<ChannelControl>();
		if ( _channel != null && _channel.IsChanneling ) return;

		var def = _slots[_channelSlot];
		_readyAt[_channelSlot] = Time.Now + (def?.Cooldown ?? 0f);
		_channelSlot = -1;
	}

	private void CacheComponents()
	{
		// Prefer the most-derived actor when scenes contain both Actor and Player components.
		_actor ??= CombatMath.ResolveActor( GameObject ) ?? Components.GetInAncestorsOrSelf<Actor>();
		_combat ??= _actor?.Combat ?? Components.GetInAncestorsOrSelf<CombatComponent>();
		_playerController ??= Components.GetInAncestorsOrSelf<PlayerController>();
	}

	// ============ SLOTS ============

	public TechniqueDef GetSlot( int slot ) => slot >= 0 && slot < SlotCount ? _slots[slot] : null;

	public void SetSlot( int slot, TechniqueDef technique )
	{
		if ( slot < 0 || slot >= SlotCount ) return;
		_slots[slot] = technique;
		_readyAt[slot] = 0f;
	}

	/// <summary>Seconds until the slot is ready again (0 = ready). For the future hotbar.</summary>
	public float GetCooldownRemaining( int slot )
	{
		if ( slot < 0 || slot >= SlotCount ) return 0f;
		return MathF.Max( 0f, _readyAt[slot] - Time.Now );
	}

	/// <summary>0..1 fraction of cooldown still remaining, for radial/sweep cooldown UI.</summary>
	public float GetCooldown01( int slot )
	{
		var def = GetSlot( slot );
		if ( def == null || def.Cooldown <= 0f ) return 0f;
		return Math.Clamp( GetCooldownRemaining( slot ) / def.Cooldown, 0f, 1f );
	}

	// ============ USE ============

	public TechniqueUseResult TryUse( int slot )
	{
		CacheComponents();

		var def = GetSlot( slot );
		if ( def == null ) return TechniqueUseResult.NoTechnique;
		if ( _actor == null || _combat == null ) return TechniqueUseResult.NoCaster;
		if ( Time.Now < _readyAt[slot] ) return TechniqueUseResult.OnCooldown;

		var state = _actor.StateComp?.CurrentState ?? ActorStateType.Idle;
		bool swinging = _combat.IsAttacking;

		// Only Idle, or mid-swing (state Attacking). Block/Cast/Dodge/Stagger/Charge etc. refuse.
		if ( state != ActorStateType.Idle && !(swinging && state == ActorStateType.Attacking) )
			return TechniqueUseResult.Blocked;

		if ( !MeetsWeaponRequirement( def ) ) return TechniqueUseResult.WrongWeapon;

		var executor = TechniqueExecutors.For( def.Form );
		// Leap has no executor: its flight is tracked in this component (see StartLeap / UpdateLeap).
		if ( executor == null && def.Form != TechniqueForm.Leap )
		{
			Log.Warning( $"[Technique] Form '{def.Form}' of '{def.DisplayName}' is not implemented yet." );
			return TechniqueUseResult.Failed;
		}

		var (origin, facing) = ResolveAim();
		var ctx = TechniqueContext.Create( def, _actor, origin, facing );

		// Preflight BEFORE cancelling anything: failing to pay must not cost the player their swing.
		if ( !_actor.CanPayCost( ctx ) ) return TechniqueUseResult.NoResources;

		if ( swinging )
		{
			if ( !def.SwingReset ) return TechniqueUseResult.Blocked;

			bool cleanCancel = _combat.CurrentPhase == AttackPhase.Recovery;
			bool fires = cleanCancel || EarlyPressFiresTechnique;

			_combat.ResetSwing( fires ? 0f : EarlyCancelLockout );
			if ( !fires )
			{
				Log.Info( $"[Technique] '{def.DisplayName}' pressed too early — swing cancelled." );
				return TechniqueUseResult.EarlyCancel;
			}
		}
		else if ( _combat.IsOnCooldown )
		{
			// Techniques are not gated by the regular swing cooldown (swing reset); other forms still are.
			if ( !def.SwingReset ) return TechniqueUseResult.Blocked;
			_combat.ResetSwing( 0f );
		}

		bool started = def.Form == TechniqueForm.Leap
			? StartLeap( slot, ctx )
			: executor.TryExecute( ctx, _combat );
		if ( !started )
			return TechniqueUseResult.Failed;

		if ( def.Form == TechniqueForm.Channel )
		{
			// Locked until the channel ends; OnUpdate then starts the real cooldown.
			// No b_attack here: nothing would clear it again, and there is no spin animation yet.
			_channelSlot = slot;
			_readyAt[slot] = float.MaxValue;
		}
		else
		{
			_readyAt[slot] = Time.Now + def.Cooldown;
			// Only swings drive b_attack (CombatComponent clears it at swing end). A leap would leave it stuck on.
			if ( def.Form == TechniqueForm.Swing )
				PlayAnimation();
		}
		Log.Info( $"[Technique] Started '{def.DisplayName}' (slot {slot})." );
		return TechniqueUseResult.Started;
	}

	/// <summary>
	/// Input layer calls this whenever a slot's button is NOT held; it only does anything if that slot is the
	/// one being channeled. Keeps this component input-agnostic (test keys now, hotbar later).
	/// </summary>
	public void ReleaseSlot( int slot )
	{
		if ( slot != _channelSlot ) return;
		_channel ??= _actor?.Components.Get<ChannelControl>();
		_channel?.Release();
	}

	// ============ LEAP ============

	/// <summary>
	/// Launch: an impulse up and forwards (same path as the Launch spell), cost paid now, then UpdateLeap watches for
	/// touchdown. Player-only for now (needs PlayerController ground/velocity) — extract if a second airborne
	/// technique (blink, glide) ever needs the same tracking.
	/// </summary>
	private bool StartLeap( int slot, TechniqueContext ctx )
	{
		var spec = ctx.Technique.Leap;
		if ( spec == null || _playerController == null || !_playerController.IsOnGround ) return false;

		// Use the same look direction supplied to normal player attacks and the Launch spell.
		var flat = ctx.AimDirection.WithZ( 0f );
		if ( flat.LengthSquared < 0.001f )
			flat = Rotation.From( 0f, ctx.Facing.Yaw(), 0f ).Forward;
		if ( flat.LengthSquared < 0.001f )
		{
			Log.Warning( "[Technique] Leap could not resolve a horizontal launch direction." );
			return false;
		}
		flat = flat.Normal;

		float angle = Math.Clamp( spec.LaunchAngleDegrees, 0f, 89f ) * MathF.PI / 180f;
		var direction = flat * MathF.Cos( angle ) + Vector3.Up * MathF.Sin( angle );

		if ( _actor.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Attacking;

		_actor.PayCost( ctx ); // the slam itself is built with CostMultiplier 0 so it isn't paid twice
		_playerController.PreventGrounding( 0.25f ); // ground snapping must not swallow the impulse
		CombatMath.ApplyKnockback( _actor.GameObject, direction, spec.LaunchStrength, addUpwardBias: false, preventGrounding: true );

		_leapSlot = slot;
		_leapCtx = ctx;
		_leapAirTime = 0f;
		_leapLeftGround = false;
		_leapLastVelocity = _playerController.Velocity;
		return true;
	}

	private void UpdateLeap()
	{
		if ( _leapSlot < 0 ) return;

		// Anything that took the state away (Stagger/Stun/Dead, respawn reset) cancels the leap: no slam.
		if ( _actor?.StateComp?.CurrentState != ActorStateType.Attacking )
		{
			_leapSlot = -1;
			_leapCtx = null;
			return;
		}

		var spec = _leapCtx.Technique.Leap;
		_leapAirTime += Time.Delta;

		if ( !_playerController.IsOnGround )
		{
			_leapLeftGround = true;
			_leapLastVelocity = _playerController.Velocity; // the last airborne frame is the impact speed
		}
		else if ( _leapLeftGround )
		{
			Slam();
			return;
		}
		else if ( _leapAirTime > spec.LaunchGraceTime )
		{
			CancelLeap( "the impulse never lifted the actor off the ground" );
			return;
		}

		if ( _leapAirTime > spec.MaxAirTime )
			CancelLeap( "airborne too long" );
	}

	private void CancelLeap( string reason )
	{
		Log.Info( $"[Technique] Leap cancelled: {reason}." );
		_leapSlot = -1;
		_leapCtx = null;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Attacking )
			_actor.StateComp.CurrentState = ActorStateType.Idle;
	}

	/// <summary>Touchdown: apply the speed/weight bonus, then run the slam as a normal swing (hit phase, recovery, animation).</summary>
	private void Slam()
	{
		var ctx = _leapCtx;
		var spec = ctx.Technique.Leap;
		_leapSlot = -1;
		_leapCtx = null;

		float impactSpeed = _leapLastVelocity.Length;
		float speedBonus = Math.Clamp( impactSpeed / 100f * spec.SpeedBonusPer100Units, 0f, spec.MaxSpeedBonus );
		float weight = ctx.EquippedWeight;
		float weightBonus = Math.Clamp( weight * spec.WeightBonusPerUnit, 0f, spec.MaxWeightBonus );
		float bonus = speedBonus + weightBonus;

		ctx.DamageMultiplier *= 1f + bonus;
		ctx.KnockbackMultiplier *= 1f + bonus * 0.5f; // knockback follows damage at half strength
		ctx.CostMultiplier = 0f;
		ctx.Origin = _actor.GameObject.WorldPosition;
		ctx.Facing = Rotation.From( 0f, ctx.Facing.Yaw(), 0f ); // yaw only: camera pitch must not tilt the slam box

		// Hand over to the swing pipeline: it needs Idle + no cooldown, then takes the state back as Attacking.
		_actor.StateComp.CurrentState = ActorStateType.Idle;
		_combat.ResetSwing( 0f );

		bool slammed = new SwingExecutor().TryExecute( ctx, _combat );
		if ( slammed ) PlayAnimation();

		Log.Info( $"[Technique] '{ctx.Technique.DisplayName}' slam={slammed}: speed={impactSpeed:F0} (+{speedBonus:P0}), " +
		          $"weight={weight:F1} (+{weightBonus:P0}), total x{1f + bonus:F2}." );
	}

	// ============ HELPERS ============

	private bool MeetsWeaponRequirement( TechniqueDef def )
	{
		if ( def.RequiredWeaponTags == null || def.RequiredWeaponTags.Count == 0 ) return true;

		var tags = _actor.Equipment?.GetEquippedWeapon()?.Definition?.Tags;
		if ( tags == null ) return false;

		foreach ( var required in def.RequiredWeaponTags )
		{
			foreach ( var tag in tags )
			{
				if ( tag == required ) return true;
			}
		}
		return false;
	}

	/// <summary>Same aim rule as Player.TryPerformAttack: camera yaw/pitch for the player, body rotation for everyone else.</summary>
	private (Vector3 origin, Rotation facing) ResolveAim()
	{
		var origin = _actor.GameObject.WorldPosition;
		if ( _playerController != null && _actor is Player )
		{
			var eyes = _playerController.EyeAngles;
			return (origin, Rotation.From( eyes.pitch, eyes.yaw, 0f ));
		}
		if ( _actor is Player && Scene.Camera != null )
		{
			var cam = Scene.Camera.WorldRotation;
			return (origin, Rotation.From( cam.Pitch(), cam.Yaw(), 0f ));
		}
		return (origin, _actor.GameObject.WorldRotation);
	}

	private void PlayAnimation()
	{
		var renderer = (_actor as Player)?.BodyRenderer ?? Components.GetInChildren<SkinnedModelRenderer>();
		renderer?.Set( "b_attack", true );
		(_actor as Player)?.Held?.EnterUnarmedStance();
	}
}
