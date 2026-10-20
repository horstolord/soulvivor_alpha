using System;

namespace Sandbox.Code.Systems;
using Sandbox.Code.Data;

/// <summary>
/// Central hub for all character statistics.
/// Includes attributes, derived stats, runtime pools, and combat/movement stats.
/// All stats are modifiable and can be affected by buffs, items, abilities, etc.
/// </summary>
public class StatSheet : Component
{
	public MobData BaseStats { get; set; }
	// ============ ATTRIBUTES ============
	// Base 6 attributes that drive everything
	public Stat Might { get; private set; }
	public Stat Swiftness { get; private set; }
	public Stat Endurance { get; private set; }
	public Stat Will { get; private set; }
	public Stat Acuity { get; private set; }
	public Stat Wisdom { get; private set; }
	
	public Stat MightPerLevel { get; private set; }
	public Stat SwiftnessPerLevel { get; private set; }
	public Stat EndurancePerLevel { get; private set; }
	public Stat WillPerLevel { get; private set; }
	public Stat AcuityPerLevel { get; private set; }
	public Stat WisdomPerLevel { get; private set; }
	

	// ============ RESOURCE POOLS ============
	// Current and max values for health/stamina/energy
	public Stat MaxHealth { get; private set; }
	public Stat MaxStamina { get; private set; }
	public Stat MaxEnergy { get; private set; }
	public Stat MaxPoise { get; private set; }

	public float CurrentHealth { get; set; } // Runtime value, can be modified directly
	public float CurrentStamina { get; set; }
	public float CurrentEnergy { get; set; }
	public float CurrentPoise { get; set; }
	public float TimeSincePoiseDmg { get; set; }

	// Reset the poise damage timer – call whenever poise is damaged.
	public void ResetPoiseTimer()
	{
		TimeSincePoiseDmg = 0f;
	}

	// ============ REGENERATION ============
	public Stat HealthRegen { get; private set; }
	public Stat StaminaRegen { get; private set; }
	public Stat EnergyRegen { get; private set; }

	// ============ COMBAT STATS ============
	public Stat DamageMultiplier { get; private set; } // 100 = 1.0x, 150 = 1.5x
	public Stat WeaponDamage { get; private set; } // Flat bonus for attacks explicitly marked as weapon attacks
	public Stat CritChance { get; private set; } // Percent
	public Stat CritDamage { get; private set; } // Percent
	

	// ============ MOVEMENT STATS ============
	public Stat MoveSpeed { get; private set; }
	public Stat AccelerationSpeed { get; private set; }
	public Stat JumpPower { get; private set; }

	// ============ MELEE COMBAT ============
	public Stat PhysicalForce { get; private set; } // Knockback power
	public Stat AttackSpeed { get; private set; } // Need animation speed multiplier
	public Stat Range { get; private set; } // Attack reach
	
	
	

	// ============ DEFENSIVE STATS ============
	public Stat Evasion { get; private set; } // Percent chance to avoid attack
	public Stat Armor { get; private set; } // Damage reduction
	public Stat BlockReduction {get; private set;} // Percent
	public Stat ResistanceFire { get; private set; } // Percent
	public Stat ResistanceFrost { get; private set; }
	public Stat ResistanceAir { get; private set; }
	public Stat ResistanceEarth { get; private set; }

	// ============ UTILITY STATS ============
	public Stat CastSpeed {get; private set;} // scales spell wind-up duration
	public Stat CostMultiplier { get; private set; } // Health/stamina/energy cost 100 = 1.0x
	public Stat EffectDuration { get; private set; } // How long buffs/debuffs last (percent)
	public Stat EffectPotency { get; private set; } // How strong buffs/debuffs are (percent)

	// ============ ELEMENTAL DAMAGE (outgoing, applies to Physical-tagged hits) ============
	public Stat PhysicalAsFire { get; private set; } // percent of physical damage gained as extra fire
	public Stat PhysicalAsFrost { get; private set; }
	public Stat PhysicalAsAir { get; private set; }
	public Stat PhysicalAsEarth { get; private set; }
	public Stat AddedFireDamage { get; private set; } // flat extra fire damage per hit
	public Stat AddedFrostDamage { get; private set; }
	public Stat AddedAirDamage { get; private set; }
	public Stat AddedEarthDamage { get; private set; }

	// ============ MISC COMBAT ============
	public Stat AdditionalProjectiles { get; private set; } // extra projectiles per projectile attack / spell (base 0)
	public Stat DamageTakenMultiplier { get; private set; } // 100 = 1.0x, 50 = takes half

	// ============ PROJECTILE MODS (bows & staves) ============
	public Stat ProjectileCount { get; private set; } // Extra projectiles per shot/cast (flat count)
	public Stat ProjectilePierce { get; private set; } // Extra targets a projectile passes through (flat count)
	public Stat EchoChance { get; private set; } // Percent chance a shot/cast repeats itself once, for free

	public void InitializeFromRegistry( string mobId )
	{
		// 1. Safely pull the master template from the dictionary
		if ( !MobRegistry.Library.TryGetValue( mobId, out var templateData ) )
		{
			Log.Error( $"StatSheet failed to initialize! ID '{mobId}' does not exist in MobRegistry." );
			return;
		}

		// 2. Fill this actor's own copies with the template's numbers
		Might = new Stat( templateData.Might );
		Swiftness = new Stat( templateData.Swiftness);
		Endurance = new Stat( templateData.Endurance );
		Will = new Stat( templateData.Will);
		Acuity = new Stat( templateData.Acuity );
		Wisdom = new Stat( templateData.Wisdom );
		MightPerLevel = new Stat( templateData.MightPerLevel );
		SwiftnessPerLevel = new Stat( templateData.SwiftnessPerLevel );
		EndurancePerLevel = new Stat( templateData.EndurancePerLevel );
		WillPerLevel = new Stat( templateData.WillPerLevel );
		AcuityPerLevel = new Stat( templateData.AcuityPerLevel );
		WisdomPerLevel = new Stat( templateData.WisdomPerLevel );
		CastSpeed = new Stat( 100f );

		Log.Info( $"{GameObject.Name} stats initialized successfully as a unique instance of '{templateData.Name}'." );
		
		MaxHealth = new Stat();
		MaxStamina = new Stat();
		MaxEnergy = new Stat();
		MaxPoise = new Stat(templateData.Poise);

		// Initialize regeneration rates
		HealthRegen = new Stat();
		StaminaRegen = new Stat();
		EnergyRegen = new Stat();

		// Initialize combat stats
		DamageMultiplier = new Stat( 100f ); // Default 1.0x
		WeaponDamage = new Stat( 0f );
		CritChance = new Stat( templateData.CritChance );
		CritDamage = new Stat( 50f ); // Default 1.5x = 150%
		Armor = new Stat( templateData.Armor );
		BlockReduction = new Stat(0f);
		ResistanceFire = new Stat( 0f );
		ResistanceFrost = new Stat( 0f );
		ResistanceAir = new Stat( 0f );
		ResistanceEarth = new Stat( 0f );

		// Initialize movement stats
		MoveSpeed = new Stat( 100f + Swiftness.Value * 2 ); // Default units per second or percentage
		AccelerationSpeed = new Stat( 100f + Swiftness.Value );
		JumpPower = new Stat( 100f + Might.Value );

		// Initialize  combat stats
		PhysicalForce = new Stat( 100f ); // Default 1.0x knockback
		AttackSpeed = new Stat( 100f ); // Default 1.0x
		Range = new Stat( 100f );
		
		ProjectileCount = new Stat( 1f );
		ProjectilePierce = new Stat( 0f );
		EchoChance = new Stat( 0f );

		// Initialize defensive stats
		Evasion = new Stat( 0f );

		// Initialize utility stats
		CostMultiplier = new Stat( 100f ); // Default 1.0x cost
		EffectDuration = new Stat( 100f ); // Default 1.0x duration
		EffectPotency = new Stat( 100f ); // Default 1.0x potency
		PhysicalAsFire = new Stat( 0f );
		PhysicalAsFrost = new Stat( 0f );
		PhysicalAsAir = new Stat( 0f );
		PhysicalAsEarth = new Stat( 0f );
		AddedFireDamage = new Stat( 0f );
		AddedFrostDamage = new Stat( 0f );
		AddedAirDamage = new Stat( 0f );
		AddedEarthDamage = new Stat( 0f );
		AdditionalProjectiles = new Stat( 0f );
		DamageTakenMultiplier = new Stat( 100f );

		// Projectile mods: everything starts at zero, gear and buffs add to it
		ProjectileCount = new Stat( 0f );
		ProjectilePierce = new Stat( 0f );
		EchoChance = new Stat( 0f );

		// Fill current pools to max
		RecalculateDerivedStats();
		FillCurrentPoolsToMax();

		Log.Info( $"StatSheet initialized: Swiftness={Swiftness.Value}, StaminaRegen={StaminaRegen.Value}, MaxStamina={MaxStamina.Value}" );
	}
	/// Recalculates derived stats based on current attributes.
	/// Call this after attribute modifiers 
	
	public void RecalculateDerivedStats()
	{
		// Resource pools (based on attributes)
		MaxHealth.BaseValue = Endurance.Value * 10f;
		MaxStamina.BaseValue = Swiftness.Value * 5f + Endurance.Value * 5f ;
		MaxEnergy.BaseValue = Wisdom.Value * 10f;

		// Regeneration rates
		HealthRegen.BaseValue = Endurance.Value * 0.1f;
		StaminaRegen.BaseValue = Swiftness.Value * 0.3f;
		EnergyRegen.BaseValue = Acuity.Value * 0.2f;

		// Movement stats (based on attributes)
		MoveSpeed.BaseValue = 100f + Swiftness.Value * 2f;
		AccelerationSpeed.BaseValue = 100f + Swiftness.Value;
		JumpPower.BaseValue = 100f + Might.Value;

		// Clamp current pools to max
		CurrentHealth = MathF.Min( CurrentHealth, MaxHealth.Value );
		CurrentStamina = MathF.Min( CurrentStamina, MaxStamina.Value );
		CurrentEnergy = MathF.Min( CurrentEnergy, MaxEnergy.Value );
		CurrentPoise = MathF.Min( CurrentPoise, MaxPoise.Value );
	}

	/// <summary>
	/// Fills all current resource pools to their max values.
	/// Typically called on initialization or full heal.
	/// </summary>
	public void FillCurrentPoolsToMax()
	{
		CurrentHealth = MaxHealth.Value;
		CurrentStamina = MaxStamina.Value;
		CurrentEnergy = MaxEnergy.Value;
		CurrentPoise = MaxPoise.Value;
	}
	/// Get a stat by name. Useful for dynamic buff application.
	public Stat GetStat( string statName )
	{
		return statName switch
		{
			// Attributes
			"Might" => Might,
			"Swiftness" => Swiftness,
			"Endurance" => Endurance,
			"Will" => Will,
			"Acuity" => Acuity,
			"Wisdom" => Wisdom,

			// Resource Pools
			"MaxHealth" => MaxHealth,
			"MaxStamina" => MaxStamina,
			"MaxEnergy" => MaxEnergy,
			"MaxPoise" => MaxPoise,

			// Regen
			"HealthRegen" => HealthRegen,
			"StaminaRegen" => StaminaRegen,
			"EnergyRegen" => EnergyRegen,

			// Combat
			"DamageMultiplier" => DamageMultiplier,
			"WeaponDamage" => WeaponDamage,
			"CritChance" => CritChance,
			"CritDamage" => CritDamage,
			"BlockReduction" => BlockReduction,
			"ResistanceFire" => ResistanceFire,
			"ResistanceFrost" => ResistanceFrost,
			"ResistanceAir" => ResistanceAir,
			"ResistanceEarth" => ResistanceEarth,

			// Movement
			"MoveSpeed" => MoveSpeed,
			"AccelerationSpeed" => AccelerationSpeed,
			"JumpPower" => JumpPower,

			// Melee
			"PhysicalForce" => PhysicalForce,
			"AttackSpeed" => AttackSpeed,
			"Range" => Range,
			
			
			// Defense
			"Evasion" => Evasion,
			"Armor" => Armor,

			// Utility
			"CastSpeed" => CastSpeed,
			"CostMultiplier" => CostMultiplier,
			"EffectDuration" => EffectDuration,
			"EffectPotency" => EffectPotency,

			// Elemental damage
			"PhysicalAsFire" => PhysicalAsFire,
			"PhysicalAsFrost" => PhysicalAsFrost,
			"PhysicalAsAir" => PhysicalAsAir,
			"PhysicalAsEarth" => PhysicalAsEarth,
			"AddedFireDamage" => AddedFireDamage,
			"AddedFrostDamage" => AddedFrostDamage,
			"AddedAirDamage" => AddedAirDamage,
			"AddedEarthDamage" => AddedEarthDamage,

			// Misc combat
			"AdditionalProjectiles" => AdditionalProjectiles,
			"DamageTakenMultiplier" => DamageTakenMultiplier,

			// Projectile mods
			"ProjectileCount" => ProjectileCount,
			"ProjectilePierce" => ProjectilePierce,
			"EchoChance" => EchoChance,

			_ => null
		};
	}
	/// Get all modifiable stats as a collection. Useful for UI or debugging.
	public IEnumerable<(string Name, Stat Stat)> GetAllStats()
	{
		yield return ( "Might", Might );
		yield return ( "Swiftness", Swiftness);
		yield return ( "Endurance", Endurance );
		yield return ( "Willpower", Will );
		yield return ( "Acuity", Acuity );
		yield return ( "Wisdom", Wisdom );
		yield return ( "MaxHealth", MaxHealth );
		yield return ( "MaxStamina", MaxStamina );
		yield return ( "MaxEnergy", MaxEnergy );
		yield return ( "MaxPoise", MaxPoise );
		yield return ( "HealthRegen", HealthRegen );
		yield return ( "StaminaRegen", StaminaRegen );
		yield return ( "EnergyRegen", EnergyRegen );
		yield return ( "DamageMultiplier", DamageMultiplier );
		yield return ( "WeaponDamage", WeaponDamage );
		yield return ( "CritChance", CritChance );
		yield return ( "CritDamage", CritDamage );
		yield return ( "Armor", Armor );
		yield return ( "BlockReduction", BlockReduction );
		yield return ( "ResistanceFire", ResistanceFire );
		yield return ( "ResistanceFrost", ResistanceFrost );
		yield return ( "ResistanceAir", ResistanceAir );
		yield return ( "ResistanceEarth", ResistanceEarth );
		yield return ( "MoveSpeed", MoveSpeed );
		yield return ( "AccelerationSpeed", AccelerationSpeed );
		yield return ( "JumpPower", JumpPower );
		yield return ( "PhysicalForce", PhysicalForce );
		yield return ( "AttackSpeed", AttackSpeed );
		yield return ( "Range", Range );
		yield return ( "Evasion", Evasion );
		yield return ( "CastSpeed", CastSpeed );
		yield return ( "CostMultiplier", CostMultiplier );
		yield return ( "EffectDuration", EffectDuration );
		yield return ( "EffectPotency", EffectPotency );
		yield return ( "PhysicalAsFire", PhysicalAsFire );
		yield return ( "PhysicalAsFrost", PhysicalAsFrost );
		yield return ( "PhysicalAsAir", PhysicalAsAir );
		yield return ( "PhysicalAsEarth", PhysicalAsEarth );
		yield return ( "AddedFireDamage", AddedFireDamage );
		yield return ( "AddedFrostDamage", AddedFrostDamage );
		yield return ( "AddedAirDamage", AddedAirDamage );
		yield return ( "AddedEarthDamage", AddedEarthDamage );
		yield return ( "AdditionalProjectiles", AdditionalProjectiles );
		yield return ( "DamageTakenMultiplier", DamageTakenMultiplier );
		yield return ( "ProjectileCount", ProjectileCount );
		yield return ( "ProjectilePierce", ProjectilePierce );
		yield return ( "EchoChance", EchoChance );
	}
}
