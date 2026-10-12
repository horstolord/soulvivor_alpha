using System;
using System.Collections.Generic;

namespace Sandbox.Code.Systems;
using System.Linq;
using Actors;

public sealed class CombatComponent : Component
{
	public AttackContext? CurrentAttack { get; private set; }

	/// <summary>
	/// True while the actor is actively in an attack (startup → recovery).
	/// </summary>
	public bool IsAttacking => CurrentAttack != null;
	
	/// <summary>
	/// True while the cooldown between attacks is ticking down.
	/// The actor can move but cannot start a new attack.
	/// </summary>
	public bool IsOnCooldown => _cooldownTimer > 0f;

	/// <summary>
	/// Convenience gate: both CurrentAttack and cooldown must be clear.
	/// </summary>
	public bool CanAttack => !IsAttacking && !IsOnCooldown;

	private float AttackElapsed;
	private float _cooldownTimer;
	private HashSet<GameObject> _hitObjects = new();
	private readonly Dictionary<HitPhaseDef, (Vector3 Origin, Rotation Facing)> _phasePoses = new();

	[Property] public GameObject ArrowPrefab { get; set; }
	[Property] public Vector3 ProjectileSpawnOffset { get; set; } = new Vector3( 0f, 0f, 60f );

	public bool TryStartAttack( AttackRequest request )
	{
		if ( !CanAttack )
			return false;
		if ( !ValidateRequest( request ) )
			return false;

		var attackerActor = ResolveActor( request.Attacker );
		if ( attackerActor?.StateComp != null && attackerActor.StateComp.CurrentState != ActorStateType.Idle )
			return false;

		var context = BuildContext( request );
		if ( !ValidateCost( context ) )
			return false;

		StartAttack( context );
		return true;
	}

	protected override void OnUpdate()
	{
		UpdateEchoQueue();
		UpdateCurrentAttack();
	}

	private AttackContext BuildContext( AttackRequest request )
	{
		var attack = request.Attack;
		var attackerActor = ResolveActor( request.Attacker );
		var attackerMight = attackerActor?.StatSheet?.Might.Value ?? 0f;
		var attackerSwiftness = attackerActor?.StatSheet?.Swiftness.Value ?? 0f;
		// Live off StatSheet.AttackSpeed (buffs, loot affixes, etc.) rather than baked into the
		// AttackDef at build time — mirrors how SpellComponent already reads CastSpeed live at cast time.
		var attackSpeedMultiplier = MathF.Max( 0.01f, (attackerActor?.StatSheet?.AttackSpeed.Value ?? 100f) / 100f );

		var baseDamage = new DamageProfileDef
		{
			HealthDamage = attack.Damage.HealthDamage + attackerMight * attack.Scaling.MightToHealthDamage
				+ attackerSwiftness * attack.Scaling.SwiftnessToHealthDamage
				+ (attackerActor?.StatSheet?.WeaponDamage.Value ?? 0f) * attack.WeaponDamageEffectiveness,
			StaminaDamage = attack.Damage.StaminaDamage,
			KnockbackForce = attack.Damage.KnockbackForce * attack.Scaling.MightToKnockbackForce,
			Tags = [..(attack.Tags ?? new HashSet<AttackTag>())]
		};
		// Timed weapon enchant (Ember Weapon etc.) only augments weapon attacks, not punches/kicks.
		if ( attack.WeaponDamageEffectiveness > 0f )
			baseDamage = attackerActor?.Components.Get<WeaponImbueControl>()?.Augment( baseDamage ) ?? baseDamage;
		// Charge01 is 0 for a normal tap attack, so this is a no-op unless the swing was charged.
		var chargeBonus = attackerMight * attack.Scaling.MightToChargeBonus;
		var chargeKnockbackBonus = attackerMight * attack.Scaling.MightToChargeKnockback;
		var damage = CombatMath.ApplyCharge( baseDamage, request.Charge01, chargeBonus, chargeKnockbackBonus );
		return new AttackContext
		{
			Request = request,
			Attack = attack,
			Attacker = request.Attacker,
			SourceItem = request.SourceItem,
			Origin = request.Origin,
			Facing = request.Facing,
			AimDirection = request.AimDirection,
			TargetPoint = request.TargetPoint,
			Charge01 = request.Charge01,
			AlternateUse = request.AlternateUse,
			TriggerType = request.TriggerType,
			StartupTime = attack.StartupTime / attackSpeedMultiplier,
			RecoveryTime = attack.RecoveryTime / attackSpeedMultiplier,
			CooldownTime = attack.CooldownTime / attackSpeedMultiplier,
			HealthCost = attack.HealthCost,
			StaminaCost = attack.StaminaCost,
			EnergyCost = attack.EnergyCost,
			Scaling = attack.Scaling,
			Damage = damage,
			HitPhases = (attack.HitPhases ?? new List<HitPhaseDef>())
				.Select( phase => new HitPhaseDef
				{
					StartTime = phase.StartTime / attackSpeedMultiplier,
					EndTime = phase.EndTime / attackSpeedMultiplier,
					StopAfterFirstHit = phase.StopAfterFirstHit,
					Shapes = (phase.Shapes ?? new List<HitShapeDef>())
						.Select( shape => new HitShapeDef
						{
							CastType = shape.CastType,
							LocalOffset = shape.LocalOffset,
							SweepOffset = shape.SweepOffset,
							BoxSize = shape.BoxSize
						} )
						.ToList()
				} )
				.ToList(),
			Tags = [..(attack.Tags ?? new HashSet<AttackTag>())],
			AnimationName = attack.AnimationName,
			LockFacing = attack.LockFacing,
			CanMoveDuringStartup = attack.CanMoveDuringStartup,
			CanMoveDuringRecovery = attack.CanMoveDuringRecovery
		};
	}

	private bool ValidateRequest( AttackRequest request )
	{
		if ( request == null )
			return false;
		if ( request.Attacker == null )
			return false;
		if ( request.Attack == null )
			return false;
		if ( request.Attack.Damage == null )
			return false;
		if ( request.Attack.Scaling == null )
			return false;
		return true;
	}

	private bool ValidateCost( AttackContext context )
	{
		if ( context.Attacker == null )
			return false;
		if ( context.Attack == null )
			return false;
		var attacker = ResolveActor( context.Attacker );
		if ( attacker != null && !attacker.CanPayCost( context.Attack ) )
		{
			Log.Info( $"Not enough resources for {context.Attack.DisplayName}. Health {attacker.StatSheet.CurrentHealth}/{context.Attack.HealthCost}, stamina {attacker.StatSheet.CurrentStamina}/{context.Attack.StaminaCost}, energy {attacker.StatSheet.CurrentEnergy}/{context.Attack.EnergyCost}" );
			return false;
		}
		return true;
	}

	private void StartAttack( AttackContext context )
	{
		CurrentAttack = context;
		AttackElapsed = 0f;
		_hitObjects.Clear();
		_phasePoses.Clear();

		var attacker = ResolveActor( context.Attacker );
		attacker?.PayCost( context.Attack );
		if ( attacker?.StateComp != null )
			attacker.StateComp.CurrentState = ActorStateType.Attacking;

		Log.Info( $"Starting attack: {context.Attack.DisplayName} (startup={context.StartupTime:F2}s, recovery={context.RecoveryTime:F2}s, cooldown={context.CooldownTime:F2}s)" );
	}

	/// <summary>Echo shots waiting to fire; they outlive the attack that rolled them.</summary>
	private readonly List<(float Time, AttackContext Context)> _echoQueue = new();

	/// <summary>
	/// One shot: 1 + ProjectileCount projectiles fanned around the aim, then an Echo roll.
	/// An echo is itself never echoed, so a high EchoChance can't chain.
	/// </summary>
	private void FireVolley( AttackContext context, bool isEcho = false )
	{
		var sheet = ResolveActor( context.Attacker )?.StatSheet;
		int count = 1 + Math.Max( 0, (int)MathF.Round( sheet?.ProjectileCount?.Value ?? 0f ) );
		float damageMultiplier = isEcho ? ProjectileTuning.EchoDamageMultiplier : 1f;

		for ( int i = 0; i < count; i++ )
			SpawnProjectile( context, count > 1 ? ProjectileTuning.FanYaw( i, count ) : 0f, damageMultiplier );

		if ( !isEcho && ProjectileTuning.Roll( sheet?.EchoChance?.Value ?? 0f ) )
			_echoQueue.Add( (Time.Now + ProjectileTuning.EchoDelay, context) );
	}

	private void UpdateEchoQueue()
	{
		for ( int i = _echoQueue.Count - 1; i >= 0; i-- )
		{
			var (time, context) = _echoQueue[i];
			if ( Time.Now < time ) continue;
			_echoQueue.RemoveAt( i );

			var attacker = context.Attacker;
			if ( !attacker.IsValid() ) continue;
			if ( ResolveActor( attacker )?.StateComp?.CurrentState == ActorStateType.Dead ) continue;

			// Player echoes follow the live camera (see SpawnProjectile); AI echoes fire from where the shooter stands now.
			if ( context.TriggerType != AttackTriggerType.PlayerInput )
			{
				context.Origin = attacker.WorldPosition;
				context.Facing = attacker.WorldRotation;
			}
			FireVolley( context, isEcho: true );
		}
	}

	private void SpawnProjectile( AttackContext context, float yawOffset = 0f, float damageMultiplier = 1f )
	{
		var camera = context.TriggerType == AttackTriggerType.PlayerInput ? Scene.Camera : null;
		var facing = camera?.WorldRotation ?? context.Facing;
		var origin = camera != null
			? camera.WorldPosition + facing.Forward * 20f
			: context.Origin + ProjectileSpawnOffset;
		// Extra projectiles fan out around the aim; every one of them spawns at the same point.
		facing *= Rotation.From( 0f, yawOffset, 0f );
		var spawnTransform = new Transform( origin, facing );
		var config = new CloneConfig( spawnTransform, null, true );

		GameObject projGO;
		if ( !string.IsNullOrWhiteSpace( context.Attack.ProjectilePrefabPath ) )
		{
			if ( !ResourceLibrary.TryGet<PrefabFile>( context.Attack.ProjectilePrefabPath, out var prefabFile ) )
			{
				Log.Warning( $"[CombatComponent] Could not find projectile prefab '{context.Attack.ProjectilePrefabPath}'." );
				return;
			}

			projGO = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
		}
		else
		{
			if ( ArrowPrefab == null || !ArrowPrefab.IsValid() )
			{
				Log.Warning( "[CombatComponent] No projectile prefab path or ArrowPrefab assigned." );
				return;
			}

			projGO = ArrowPrefab.Clone( config );
		}

		// Set explicitly as well: CloneConfig's rotation isn't reliable for a prefab that is already live,
		// and the motion component reads WorldRotation on its first tick.
		projGO.WorldPosition = origin;
		projGO.WorldRotation = facing;

		Log.Info( $"Cloned: {projGO.Name}, valid={projGO.IsValid()}" );
		var projectile = projGO.Components.Get<Projectile>( FindMode.EnabledInSelfAndDescendants );
		if ( projectile == null )
		{
			Log.Warning( "[CombatComponent] Projectile prefab has no Projectile component." );
			projGO.Destroy();
			return;
		}

		var template = context.Attack.ProjectileTemplate.Clone();
		var attacker = ResolveActor( context.Attacker );
		if ( attacker != null )
		{
			template.Lifetime *= attacker.StatSheet.EffectDuration.Value / 100f;
			template.AddPierce( (int)MathF.Round( attacker.StatSheet.ProjectilePierce?.Value ?? 0f ) );
		}

		var damage = context.Damage;
		if ( MathF.Abs( damageMultiplier - 1f ) > 0.001f )
		{
			damage = new DamageProfileDef
			{
				HealthDamage = damage.HealthDamage * damageMultiplier,
				StaminaDamage = damage.StaminaDamage,
				KnockbackForce = damage.KnockbackForce,
				Tags = damage.Tags,
				IsCrit = damage.IsCrit
			};
		}

		projectile.Template = template;
		projectile.Payload = new ProjectilePayload
		{
			Caster = context.Attacker,
			// Base profile (pre-crit, pre-PhysicalForce). HitResolver.Apply resolves outgoing mods per hit,
			// so a piercing arrow rolls crit independently per target — matching spell projectile behavior.
			Damage = damage,
			SourceContext = context
		};

		projGO.Enabled = true;
	}

	private void UpdateCurrentAttack()
	{
		// Tick cooldown independently — actor is unlocked but can't start a new attack yet.
		if ( _cooldownTimer > 0f )
			_cooldownTimer = MathF.Max( 0f, _cooldownTimer - Time.Delta );
 
		if ( CurrentAttack == null )
			return;
 
		AttackElapsed += Time.Delta;

		if ( CurrentAttack.Attack.ProjectileTemplate != null
			&& !CurrentAttack.ProjectileSpawned
			&& AttackElapsed >= CurrentAttack.StartupTime )
		{
			CurrentAttack.ProjectileSpawned = true;
			FireVolley( CurrentAttack );
		}
 
		// Hit phases are authored relative to the end of startup, so offset by StartupTime.
		// A phase with StartTime=0.18 on a StartupTime=0.4 attack fires at t=0.58 from input.
		float activeElapsed = AttackElapsed - CurrentAttack.StartupTime;
 
		if ( activeElapsed >= 0f )
		{
			foreach ( var phase in CurrentAttack.HitPhases )
			{
				if ( activeElapsed >= phase.StartTime && activeElapsed <= phase.EndTime )
				{
					if ( !_phasePoses.TryGetValue( phase, out var pose ) )
					{
						pose = CaptureHitPhasePose( CurrentAttack );
						_phasePoses.Add( phase, pose );
					}

					ExecuteHitPhase( CurrentAttack, phase, pose );
				}
			}
		}
 
		// Attack ends after startup + latest phase end + recovery.
		var endTime = GetAttackEndTime( CurrentAttack );
		if ( AttackElapsed >= endTime )
		{
			// Start cooldown before clearing so callers can check IsOnCooldown immediately.
			_cooldownTimer = CurrentAttack.CooldownTime;
			Log.Info( $"Attack finished: {CurrentAttack.Attack.DisplayName} — cooldown {_cooldownTimer:F2}s" );
 
			var attacker = ResolveActor( CurrentAttack.Attacker );
			if ( attacker?.StateComp != null && attacker.StateComp.CurrentState == ActorStateType.Attacking )
				attacker.StateComp.CurrentState = ActorStateType.Idle;
 
			CurrentAttack = null;
			var renderer = Components.GetInParentOrSelf<SkinnedModelRenderer>() ?? Components.GetInChildren<SkinnedModelRenderer>();
			renderer?.Set( "b_attack", false );
			_hitObjects.Clear();
			_phasePoses.Clear();
		}
	}

	private (Vector3 Origin, Rotation Facing) CaptureHitPhasePose( AttackContext context )
	{
		var attacker = context.Attacker;
		var origin = attacker.IsValid() ? attacker.WorldPosition : context.Origin;
		var facing = context.Facing;

		if ( attacker.IsValid() )
		{
			facing = context.TriggerType == AttackTriggerType.PlayerInput && Scene.Camera != null
				? Scene.Camera.WorldRotation
				: attacker.WorldRotation;
		}

		facing = Rotation.From( facing.Pitch(), facing.Yaw(), 0f );
		return (origin, facing);
	}

	private void ExecuteHitPhase( AttackContext context, HitPhaseDef phase, (Vector3 Origin, Rotation Facing) pose )
	{
		bool hitLanded = false;
		foreach ( var shape in phase.Shapes )
		{
			if ( phase.StopAfterFirstHit && hitLanded )
				break;

			hitLanded |= ExecuteHitShape( context, shape, pose );
		}
	}

	/// <summary>Returns true if at least one new target was hit this call.</summary>
	private bool ExecuteHitShape( AttackContext context, HitShapeDef shape, (Vector3 Origin, Rotation Facing) pose )
	{
		var rotation = pose.Facing;
		var worldOffset = rotation * shape.LocalOffset;
		var center = pose.Origin + worldOffset;
		var sweep = rotation * shape.SweepOffset;
		var start = shape.CastType == HitShapeCastType.Sweep ? center - sweep * 0.5f : center;
		var end = shape.CastType == HitShapeCastType.Sweep ? center + sweep * 0.5f : center;

		// Draw the oriented box at each end of the sweep.
		DebugOverlay.Box( Vector3.Zero, shape.BoxSize, Color.Cyan, 2.0f, new Transform( start, rotation ) );

		if ( shape.CastType == HitShapeCastType.Sweep )
		{
			DebugOverlay.Box( Vector3.Zero, shape.BoxSize, Color.Red, 2.0f, new Transform( end, rotation ) );
			// Connect the sweep path with a line
			DebugOverlay.Line( start, end, Color.Yellow, duration: 2.0f );
		}

		var hits = Scene.Trace
			.Rotated( rotation )
			.Box( shape.BoxSize, start, end )
			.IgnoreGameObjectHierarchy( context.Attacker )
			.RunAll()
			.ToList();

		bool hitLanded = false;
		foreach ( var hit in hits )
		{
			var target = hit.GameObject;
			if ( target == null )
				continue;

			// Resolve to actor root so child colliders don't bypass deduplication.
			var actorRoot = ResolveActorRoot( target );
			var dedupKey = actorRoot ?? target;

			if ( _hitObjects.Contains( dedupKey ) )
				continue;

			_hitObjects.Add( dedupKey );
			ApplyHit( context, target, rotation.Forward, hit.HitPosition );
			hitLanded = true;
		}

		return hitLanded;
	}

	private void ApplyHit( AttackContext context, GameObject target, Vector3 facing, Vector3 hitPoint )
	{
		// Dedup by actor-root is already guaranteed before this call (ExecuteHitShape checks _hitObjects).
		// Pass null for alreadyHit — HitResolver handles crit, PhysicalForce, damage, and knockback.
		HitResolver.Apply( context.Attacker, target, context.Damage, facing,
			alreadyHit: null, hitPoint: hitPoint );
	}

	// Thin wrappers so the rest of this class is unaffected — real logic lives in CombatMath.
	private Actor ResolveActor( GameObject gameObject ) => CombatMath.ResolveActor( gameObject );

	/// <summary>
	/// Returns the root GameObject that owns the Actor component, used for hit deduplication.
	/// Returns null if no Actor is found in the hierarchy.
	/// </summary>
	private GameObject ResolveActorRoot( GameObject gameObject ) => CombatMath.ResolveActorRoot( gameObject );

	/// <summary>
	/// Total locked time of the attack: startup + latest phase end + recovery.
	/// Cooldown is tracked separately via _cooldownTimer.
	/// </summary>
	private float GetAttackEndTime( AttackContext context )
	{
		float latestPhaseEnd = 0f;
		foreach ( var phase in context.HitPhases )
		{
			if ( phase.EndTime > latestPhaseEnd )
				latestPhaseEnd = phase.EndTime;
		}
		// StartupTime offsets when phases fire, so it must also offset the recovery window.
		return context.StartupTime + latestPhaseEnd + context.RecoveryTime;
	}
}
