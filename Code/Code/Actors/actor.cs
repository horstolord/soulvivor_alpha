using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;
using Sandbox.Code.World;

namespace Sandbox.Code.Actors;

public class Actor : Component, Component.IDamageable
{
	/// <summary>
	/// Returns the mob preset ID to load from <see cref="MobRegistry"/>.
	/// Override in subclasses to pick a different preset.
	/// </summary>
	protected virtual string GetMobPresetId() => "goblin";
	

	[Property] public int InitialLevel { get; set; } = 1;
	[Property] public MobRank Rank { get; set; } = MobRank.Normal;

	public StatSheet      StatSheet { get; private set; }
	public LevelComponent Leveling  { get; private set; }
	public CombatComponent Combat;
	public EquipmentControl Equipment { get; private set; }
	public BuffComponent Buffs { get; private set; }
	public StatusControl Statuses { get; private set; }
	public ProcControl Procs { get; private set; }
	public FieldExposure Exposure {get; private set;}
	private IRagdollHandler _ragdoll;
	/// <summary>
	/// Raised the frame the actor dies (before any destruction/drops).
	/// Components can subscribe to react to death — e.g. <see cref="Sandbox.Code.Systems.PlayerRespawnManager"/>.
	/// </summary>
	public event Action OnDeath;
	/// <summary>Raised for a hit that was not evaded, after any poise/stun state changes.</summary>
	public event Action OnHitReceived;
	/// <summary>
	/// True once initialization and level setup have finished. Stat modifiers applied earlier are replaced;
	/// subscribe to OnReady before applying mods when this is false.
	/// </summary>
	public bool IsReady { get; private set; }
	public event Action OnReady;

	/// <summary>
	/// Whether the GameObject should be destroyed shortly after death. The player overrides this
	/// to false so it can be kept alive and respawned (teleported) instead.
	/// </summary>
	protected virtual bool ShouldDestroyOnDeath => true;

	// Cache the preset data so OnKilled can reference it without a dict lookup
	private MobData _mobData;
	public ActorStateComp StateComp { get; private set; }
	private const float POISE_REFILL_DELAY = 4f;
	private const float POISE_STAGGER_DURATION = 0.5f;
	private float _poiseStaggerTimer;

	// Heavy‑stun fields
	private float _staminaStunTimer;
	private const float STAMINA_ZERO_STUN_DURATION = 3f;
	private float _stunDuration = STAMINA_ZERO_STUN_DURATION;
	private bool _stunRagdolled; // true while the ragdoll was entered by a stun/death (Frozen stuns without one)

	
	public bool IsStaggered { get; private set; }

	

	// Helper to gate actions (movement, attacks, etc.)
	public bool CanAct()
	{
		return StateComp != null &&
		       StateComp.CurrentState != ActorStateType.Staggered &&
		       StateComp.CurrentState != ActorStateType.Stunned;
	}


	protected override void OnStart()
	{
		base.OnStart();

		// 1. Stat sheet — create if not already present
		StatSheet = Components.GetOrCreate<StatSheet>();
		Equipment = Components.GetOrCreate<EquipmentControl>();
		StateComp = Components.GetOrCreate<ActorStateComp>();
		Buffs     = Components.GetOrCreate<BuffComponent>();
		Statuses  = Components.GetOrCreate<StatusControl>();
		Procs = Components.GetOrCreate<ProcControl>();
		Exposure = Components.GetOrCreate<FieldExposure>();
		_ragdoll  = Components.GetInAncestorsOrSelf<IRagdollHandler>() ?? Components.GetInChildren<IRagdollHandler>();
		Log.Info( $"[Ragdoll] {GameObject.Name}: handler lookup => {(_ragdoll == null ? "NULL" : _ragdoll.GetType().Name)}" );
		_ = InitializeActorAsync();
	}

	private void Ragdoll()
	{
		Log.Info( $"[Ragdoll] {GameObject.Name}: EnterRagdoll requested; handler={(_ragdoll == null ? "NULL" : _ragdoll.GetType().Name)}, state={StateComp?.CurrentState}" );
		_ragdoll?.EnterRagdoll();
	}

	private void RestoreFromRagdoll()
	{
		Log.Info( $"[Ragdoll] {GameObject.Name}: ExitRagdoll requested; handler={(_ragdoll == null ? "NULL" : _ragdoll.GetType().Name)}, state={StateComp?.CurrentState}" );
		_ragdoll?.ExitRagdoll();
	}

	/// <summary>
	/// Brings a dead/stunned actor fully back: leaves the ragdoll, clears every stun flag and timer,
	/// refills the pools. Call this (not hand-rolled state resets) from respawn/revive code, because
	/// IsStaggered is private and a death mid-stun would otherwise leave it stuck true — which silently
	/// disables the heavy stun AND the death ragdoll on every later death.
	/// </summary>
	public void Revive()
	{
		IsStaggered = false;
		_stunRagdolled = false;
		_staminaStunTimer = 0f;
		_poiseStaggerTimer = 0f;
		RestoreFromRagdoll();

		if ( StateComp != null )
			StateComp.CurrentState = ActorStateType.Idle;

		if ( StatSheet?.MaxHealth != null )
		{
			StatSheet.RecalculateDerivedStats();
			StatSheet.FillCurrentPoolsToMax();
			StatSheet.TimeSincePoiseDmg = 0f;
		}
	}

	private async System.Threading.Tasks.Task InitializeActorAsync()
	{
		var presetId = GetMobPresetId();
		int retries = 0;

		// Warte bis zu 5 Sekunden (50 Ticks * 100ms), falls die Registry beim 1. Start noch lädt
		while ( !MobRegistry.Library.TryGetValue( presetId, out _mobData ) && retries < 50 )
		{
			await System.Threading.Tasks.Task.Delay( 100 );
			retries++;
		}

		// Falls es selbst nach dem Warten nicht existiert
		if ( _mobData == null )
		{
			Log.Error( $"{GameObject.Name}: Preset '{presetId}' absolut nicht in MobRegistry gefunden." );
			return;
		}

		// 2. Jetzt sicher laden und Werte initialisieren
		StatSheet.InitializeFromRegistry( presetId );
		if ( this is Player )
			MetaProgression.ApplyAttributeBonuses( StatSheet );

		// InitializeFromRegistry replaces Stat instances — re-bind any already-equipped gear
		Equipment?.ReapplyAll();
		Components.GetInAncestorsOrSelf<SkillControl>()?.ApplyAll();

		// 3. Level component — wire up level-up callback and apply initial level
		Leveling = Components.GetOrCreate<LevelComponent>();
		Leveling.OnLevelUp += HandleLevelUp;
		Leveling.Initialize( _mobData, InitialLevel );

		Log.Info( $"{GameObject.Name} ({_mobData.Name}) ready — HP={StatSheet.CurrentHealth:F0}, Lvl={Leveling.Level}" );
		IsReady = true;
		OnReady?.Invoke();
	}


	// ============ LEVELING ============
	private void HandleLevelUp( int newLevel )
	{
		if ( _mobData == null ) return;

		// Apply flat per-level attribute growth
		StatSheet.Might.BaseValue     += _mobData.MightPerLevel;
		StatSheet.Swiftness.BaseValue += _mobData.SwiftnessPerLevel;
		StatSheet.Endurance.BaseValue += _mobData.EndurancePerLevel;
		StatSheet.Will.BaseValue      += _mobData.WillPerLevel;
		StatSheet.Acuity.BaseValue    += _mobData.AcuityPerLevel;
		StatSheet.Wisdom.BaseValue    += _mobData.WisdomPerLevel;
		StatSheet.Armor.BaseValue     += _mobData.ArmorPerLevel;
		StatSheet.CritChance.BaseValue += _mobData.CritChancePerLevel;
		StatSheet.StatusChance.BaseValue += _mobData.CritChancePerLevel;

		// Recalculate HP, stamina, regen, etc. from new attribute totals
		StatSheet.RecalculateDerivedStats();

		// Restore pools to max on level-up (feels rewarding)
		StatSheet.FillCurrentPoolsToMax();

		Log.Info( $"{GameObject.Name} → Lvl {newLevel} | HP={StatSheet.MaxHealth.Value:F0} Might={StatSheet.Might.Value:F1}" );
		Procs?.Raise( new ProcEvent { Trigger = ProcTrigger.LevelUp, Position = GameObject.WorldPosition, Amount = newLevel } );
	}

	// ============ COMBAT COSTS ============
	public bool CanPayCost( ICostable cost )
	{
		var staminaCost = cost.StaminaCost * (StatSheet.CostMultiplier.Value / 100f);
		return StatSheet.CurrentHealth > cost.HealthCost
		       && StatSheet.CurrentStamina >= staminaCost
		       && StatSheet.CurrentEnergy >= cost.EnergyCost;
	}

	public void PayCost( ICostable cost )
	{
		var staminaCost = cost.StaminaCost * (StatSheet.CostMultiplier.Value / 100f);

		if ( cost.HealthCost > 0f )
			StatSheet.CurrentHealth = MathF.Max( 1f, StatSheet.CurrentHealth - cost.HealthCost );
		StatSheet.CurrentStamina = MathF.Max( 0f, StatSheet.CurrentStamina - staminaCost );
		StatSheet.CurrentEnergy  = MathF.Max( 0f, StatSheet.CurrentEnergy  - cost.EnergyCost );
	}

	// ============ DAMAGE ============
	/// <summary>Raised once per hit with the final, post-mitigation result. Handlers must not throw.</summary>
	public static event Action<DamageEvent> Damaged;

	private static float ExtraElementDamage( float amount, Stat resistance, AttackTag tag, HashSet<AttackTag> tags )
	{
		if ( amount <= 0f ) return 0f;
		tags.Add( tag );
		return amount * MathF.Max( 0.05f, 1f - resistance.Value / 100f );
	}

	/// <param name="source">Attacker, if known. Optional so existing call sites keep compiling.</param>
	public DamageEvent ApplyDamage( DamageProfileDef damage, GameObject source = null )
	{
		if (StatSheet == null) return default;

		// Corpses can still be hit (arrows in flight, ranged AI still shooting). Re-running the damage
		// path would call OnKilled again: duplicate OnDeath, soul orbs and loot drops.
		if ( StateComp?.CurrentState == ActorStateType.Dead ) return default;
		
		// Check evasion
		float evasion = StatSheet.Evasion.Value;
		// Periodic ticks (IsDot) can't be dodged: i-frames and Evasion only avoid hits.
		if ( !damage.IsDot && evasion > 0f && Random.Shared.NextSingle() * 100f < evasion )
		{
			Log.Info( $"{GameObject.Name} EVADED the attack!" );
			var evadeEvent = new DamageEvent { Victim = this, Source = source, Evaded = true, ProcDepth = damage.ProcDepth};
			Damaged?.Invoke( evadeEvent );
			ProcControl.RaiseDamageEvents( evadeEvent );
			return evadeEvent;
		}
		
		// DamageMultiplier belongs on the attacker side (applied in CombatMath.BuildOutgoing).
		// Reading it here on the target would make DamageBoost/Berserk raise incoming damage instead.
		float finalHealthDamage = damage.HealthDamage;
		
		if ( damage.Tags != null )
		{
			if ( damage.Tags.Contains( AttackTag.Fire  ) ) {finalHealthDamage *= MathF.Max(0.05f, 1f - (StatSheet.ResistanceFire.Value/100f));}
			if ( damage.Tags.Contains( AttackTag.Frost ) ) {finalHealthDamage *= MathF.Max(0.05f, 1f - (StatSheet.ResistanceFrost.Value/100f));}
			if ( damage.Tags.Contains( AttackTag.Air ) ) {finalHealthDamage *= MathF.Max(0.05f, 1f - (StatSheet.ResistanceAir.Value/100f));}
			if ( damage.Tags.Contains( AttackTag.Earth ) ) {finalHealthDamage *= MathF.Max(0.05f, 1f - (StatSheet.ResistanceEarth.Value/100f));}
		}
		float armor = StatSheet.Armor.Value;
		if ( armor > 0f && damage.Tags != null && damage.Tags.Contains( AttackTag.Physical ))
		{
			float armorReduction = armor / (armor + 100f);
			finalHealthDamage *= (1f-armorReduction);
		}

		// Elemental extras use their own resistance; armor only mitigates the physical part.
		var eventTags = damage.Tags;
		if ( damage.HasExtras )
		{
			eventTags = new HashSet<AttackTag>( damage.Tags ?? new HashSet<AttackTag>() );
			finalHealthDamage += ExtraElementDamage( damage.ExtraFire, StatSheet.ResistanceFire, AttackTag.Fire, eventTags )
			                     + ExtraElementDamage( damage.ExtraFrost, StatSheet.ResistanceFrost, AttackTag.Frost, eventTags )
			                     + ExtraElementDamage( damage.ExtraAir, StatSheet.ResistanceAir, AttackTag.Air, eventTags )
			                     + ExtraElementDamage( damage.ExtraEarth, StatSheet.ResistanceEarth, AttackTag.Earth, eventTags );
		}

		// Block mitigation — only Health is reduced; Stagger/Stamina still land through a guard.
		bool blocked = false;
		if ( !damage.IsDot && StateComp != null && StateComp.CurrentState == ActorStateType.Blocking )
		{
			float blockReduction = StatSheet.BlockReduction.Value;
			if ( blockReduction > 0f )
			{
				finalHealthDamage *= MathF.Max( 0f, 1f - blockReduction / 100f );
				blocked = true;
			}
			// Tankiness stat (darkness, buffs, items). Health only, like block.
			finalHealthDamage *= MathF.Max( 0f, (StatSheet.DamageTaken?.Value ?? 100f) / 100f );
		}

		// Generic damage-taken multiplier applies after armor, resistance, and block mitigation.
		finalHealthDamage *= MathF.Max( 0f, StatSheet.DamageTakenMultiplier.Value / 100f );

		// Apply health damage. Periodic ticks (IsDot) only ever touch health: no stamina/poise damage, no poise-timer
		// reset, and no stun checks (a tick on a sprinter at 0 stamina must not ragdoll them).
		StatSheet.CurrentHealth  = MathF.Max( 0f, StatSheet.CurrentHealth  - finalHealthDamage );
		if ( !damage.IsDot )
		{
			StatSheet.CurrentStamina = MathF.Max( 0f, StatSheet.CurrentStamina - damage.StaminaDamage );
			StatSheet.CurrentPoise   = MathF.Max( 0f, StatSheet.CurrentPoise   - damage.StaminaDamage );
			StatSheet.ResetPoiseTimer();

			// Heavy stun when stamina reaches zero
			if ( StatSheet.CurrentStamina <= 0f && !IsStaggered )
			{
				ApplyStun( STAMINA_ZERO_STUN_DURATION );
				Log.Info( $"{GameObject.Name} stamina depleted: entering heavy stun (ragdoll)." );
			}

			// Short poise stagger stun
			if ( StatSheet.CurrentPoise <= 0f && StateComp.CurrentState != ActorStateType.Stunned )
			{
				// A poise break on a chilled enemy becomes Frozen (heavy stun) instead of a short stagger.
				if ( Statuses != null && Statuses.TryFreezeOnPoiseBreak( source ) )
					Log.Info( $"{GameObject.Name} poise broken while chilled: frozen." );
				else
				{
					StateComp.CurrentState = ActorStateType.Staggered;
					_poiseStaggerTimer = 0f;
					Log.Info( $"{GameObject.Name} poise broken: entering stagger." );
				}
			}
		}

		Log.Info( $"{GameObject.Name} took {finalHealthDamage:F1} dmg — HP={StatSheet.CurrentHealth:F1}/{StatSheet.MaxHealth.Value:F1}" );
		bool killed = StatSheet.CurrentHealth <= 0f;
		var hitEvent = new DamageEvent
		{
			Victim = this,
			Source = source,
			Amount = finalHealthDamage,
			Tags = eventTags,
			IsCrit = damage.IsCrit,
			Blocked = blocked,
			Killed = killed,
			IsDot = damage.IsDot,
			ProcDepth = damage.ProcDepth
		};
		Damaged?.Invoke( hitEvent );
		// Ticks don't raise hit procs (HitTaken/HitDealt/Crit would fire every 0.5s per DoT). Remove the guard to allow it.
		if ( !damage.IsDot ) ProcControl.RaiseDamageEvents( hitEvent );
		if ( killed )
		{
			OnKilled();
			if ( !_stunRagdolled ) { _stunRagdolled = true; Ragdoll(); }
		}
		// Ticks must not count as hits: SpellComponent.HandleCasterHit adds cast time/cost per OnHitReceived.
		if ( !damage.IsDot ) OnHitReceived?.Invoke();
		return hitEvent;
	}
	/// <summary>
	/// Engine built-in damage system lead in - collision.
	/// </summary>
	/// <param name="info"></param>
	public void OnDamage( in DamageInfo info )
	{
		if (  StateComp?.CurrentState == ActorStateType.Dead ) return;

		Log.Info( $"[ImpactDmg] {GameObject.Name} <- {info.Damage:F1} from {info.Attacker?.Name ?? "none"} tags=[{info.Tags}]" ); // temp

		ApplyDamage( new DamageProfileDef
		{
			HealthDamage = info.Damage,
			StaminaDamage = info.Damage * 1f, // poise uses this value
			Tags = new() { AttackTag.Physical }
		}, info.Attacker );
	}

	// ============ DEATH ============
	protected virtual async void OnKilled()
	{
		StateComp.CurrentState = ActorStateType.Dead;
		Log.Info( $"{GameObject.Name} killed." );
		OnDeath?.Invoke();

		var aicomponent = GameObject.Components.GetInAncestorsOrSelf<Enemy>();
		if ( aicomponent != null )
		{
			aicomponent.Enabled = false;
			Player.Local?.OnEnemyKilled( this );
		}
		await Task.DelaySeconds(4.0f);
		if (!this.IsValid()) return;

		if ( ShouldDestroyOnDeath )
		{
			GameObject.Destroy();
			SpawnSoulOrb();
			SpawnLootDrop();
			SpawnBoonDrop();
			
		}
		else
		{
			// Player death: keep the object alive — a respawn component will
			// teleport it back and restore it. Souls are still dropped here.
			SpawnSoulOrb();
			SpawnLootDrop();
		}
	}

	private void SpawnSoulOrb()
	{
		if ( _mobData == null ) return;

		float totalSouls = _mobData.BaseSoulValue + (Leveling?.CurrentSouls ?? 0f);
		if ( totalSouls <= 0f ) return;

		var orbGO = Scene.CreateObject();
		orbGO.WorldPosition = GameObject.WorldPosition + Vector3.Up * 20f;
		orbGO.Name = "SoulOrb";

		var orb = orbGO.Components.Create<SoulOrb>();
		orb.SoulValue = totalSouls;
	}

	private void SpawnLootDrop()
	{
		if ( _mobData == null ) return;

		int level = Leveling?.Level ?? 1;
		var instance = LootGenerator.RollDrop( level, _mobData.BaseSoulValue );
		if ( instance == null ) return; // drop-chance roll failed, nothing spawns

		var dropGO = Scene.CreateObject();
		dropGO.WorldPosition = GameObject.WorldPosition;
		dropGO.Name = $"LootDrop_{instance.Definition?.Id}";

		var drop = dropGO.Components.Create<LootDrop>();
		drop.Item = instance;
	}
	private void SpawnBoonDrop()
	{
		if ( this is not Enemy || _mobData == null ) return; // the player never drops boons

		float chance = BoonGenerator.DropChanceFor( Rank, _mobData.BoonDropChance );
		if ( Random.Shared.NextSingle() * 100f >= chance ) return;

		BoonPickup.Spawn( Scene, GameObject.WorldPosition + Vector3.Up * 30f, BoonGenerator.RequestForDrop( Rank ) );
	}


	// ============ TICK ============
	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( StatSheet == null || _mobData == null ) return;
		Regenerate( Time.Delta );
	}

	private void Regenerate( float dt )
	{
		// No health/stamina regen on corpses — it also kept running while waiting to respawn.
		if ( StateComp?.CurrentState == ActorStateType.Dead ) return;

		StatSheet.CurrentHealth = MathF.Min( StatSheet.CurrentHealth + StatSheet.HealthRegen.Value * dt,
			StatSheet.MaxHealth.Value );
		if ( ShouldRegenerateStamina )
			StatSheet.CurrentStamina = MathF.Min( StatSheet.CurrentStamina + StatSheet.StaminaRegen.Value * dt,
				StatSheet.MaxStamina.Value );
		StatSheet.CurrentEnergy = MathF.Min( StatSheet.CurrentEnergy + StatSheet.EnergyRegen.Value * dt,
			StatSheet.MaxEnergy.Value );

		// Poise refill timer
		StatSheet.TimeSincePoiseDmg += dt;
		if ( StatSheet.TimeSincePoiseDmg >= POISE_REFILL_DELAY )
		{
			StatSheet.CurrentPoise = StatSheet.MaxPoise.Value;
			StatSheet.TimeSincePoiseDmg = 0f;
		}

		// Short poise hit‑stun handling
		if ( StateComp.CurrentState == ActorStateType.Staggered )
		{
			_poiseStaggerTimer += dt;
			if ( _poiseStaggerTimer >= POISE_STAGGER_DURATION )
				StateComp.CurrentState = ActorStateType.Idle;
		}

		// Heavy stamina‑zero stun handling
		if ( StateComp.CurrentState == ActorStateType.Stunned )
		{
			_staminaStunTimer += dt;
			if ( _staminaStunTimer >= _stunDuration )
			{
				StateComp.CurrentState = ActorStateType.Idle;
				IsStaggered = false;
				if ( _stunRagdolled ) { _stunRagdolled = false; RestoreFromRagdoll(); }
			}
		}
	}
	/// <summary>
	/// Heavy stun for <paramref name="duration"/> seconds (stamina-zero, KnockDown, Frozen). If already stunned the
	/// longer of remaining/new time wins. ragdoll=false stuns in place (Frozen).
	/// </summary>
	public void ApplyStun( float duration, bool ragdoll = true )
	{
		if ( StateComp == null || StateComp.CurrentState == ActorStateType.Dead ) return;

		// IsStaggered also covers the separate short poise-stagger state. Only preserve a
		// remaining timer when this actor is already in the heavy Stunned state.
		float remaining = StateComp.CurrentState == ActorStateType.Stunned
			? MathF.Max( 0f, _stunDuration - _staminaStunTimer )
			: 0f;
		_stunDuration = MathF.Max( remaining, duration );
		_staminaStunTimer = 0f;
		IsStaggered = true;
		StateComp.CurrentState = ActorStateType.Stunned;
		if ( ragdoll && !_stunRagdolled ) { _stunRagdolled = true; Ragdoll(); }
	}

	public void KnockDown( float duration ) => ApplyStun( duration );

	/// <summary>Lets status effects (Shock) trigger the same reaction as a landed hit, e.g. SpellComponent.HandleCasterHit.</summary>
	public void NotifyHitReceived() => OnHitReceived?.Invoke();

	/// Clears all knockdown/stun/death state. Call before teleporting on respawn.
	public void ResetForRespawn()
	{
		IsStaggered = false;
		_stunRagdolled = false;
		_staminaStunTimer = 0f;
		_poiseStaggerTimer = 0f;
		RestoreFromRagdoll();
		if ( StateComp != null ) StateComp.CurrentState = ActorStateType.Idle;
	}

	protected virtual bool ShouldRegenerateStamina => true;
}
