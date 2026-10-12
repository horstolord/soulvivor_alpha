using System;
using System.Collections.Generic;
using Sandbox.Code.Systems;
using Sandbox.Code.Data;
 
namespace Sandbox.Code.World;
 
public static class RuneLibrary
{
	public static readonly RuneDef FireForce = new RuneDef
	{
		Id = "fire_force",
		DisplayName = "Fire Force",
		Category = RuneCategory.Force,
		BasePower = 25f,
		EnergyCost = 5f,
		KnockbackForce = 2400f, // placeholder — tune to taste
		ElementTag = RuneElementTag.Fire,
		SpellTags = new() { AttackTag.Fire },
		Scaling = new RuneScalingDef { WillToPower = 2f }
		// VisualMaterial = Material.Load( "materials/fx/fire_orb.vmat" ), // <- swap for real asset path
	};
 
	public static readonly RuneDef FrostForce = new RuneDef
	{
		Id = "frost_force",
		DisplayName = "Frost Force",
		Category = RuneCategory.Force,
		BasePower = 18f,
		EnergyCost = 4f,
		KnockbackForce = 2400f, // placeholder — tune to taste
		ElementTag = RuneElementTag.Frost,
		SpellTags = new() { AttackTag.Frost },
		Scaling = new RuneScalingDef { WillToPower = 1f }
		// VisualMaterial = Material.Load( "materials/fx/frost_orb.vmat" ), // <- swap for real asset path
	};
	
	public static readonly RuneDef AirForce = new RuneDef
	{
		Id = "air_force",
		DisplayName = "Air Force",
		Category = RuneCategory.Force,
		BasePower = 20f,
		EnergyCost = 4f,
		KnockbackForce = 4000f, // placeholder — tune to taste
		ElementTag = RuneElementTag.Air,
		SpellTags = new() { AttackTag.Air },
		Scaling = new RuneScalingDef { WillToPower = 1.5f }
		// VisualMaterial = Material.Load( "materials/fx/frost_orb.vmat" ), // <- swap for real asset path
	};

	public static readonly RuneDef EarthForce = new RuneDef
	{
		Id = "earth_force",
		DisplayName = "Earth Force",
		Category = RuneCategory.Force,
		BasePower = 28f,
		EnergyCost = 5f,
		KnockbackForce = 3400f, // placeholder — tune to taste
		ElementTag = RuneElementTag.Earth,
		SpellTags = new() { AttackTag.Earth },
		Scaling = new RuneScalingDef { WillToPower = 2f }
	};
 
	// Method runes are element-agnostic: the Force rune in the sequence picks the element, and
	// SpellVisualResolver / ElementEffects resolve prefabs and variants from it. Force runes must
	// come BEFORE the Method rune (payload contexts are cloned when the Method rune is walked).
	public static readonly RuneDef ProjectileMethod = new RuneDef
	{
		Id = "method_projectile",
		DisplayName = "Projectile Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Projectile,
		CastDelay = 0.15f,
		EnergyCost = 2f,
		ProjectileTemplate = new ProjectileTemplate { Speed = 1200f, Lifetime = 4f },
		Scaling = new RuneScalingDef { AcuityToCastSpeed = 2f }
	};

	public static readonly RuneDef BlastMethod = new RuneDef
	{
		Id = "method_blast",
		DisplayName = "Blast Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Blast,
		CastDelay = 0.25f,
		EnergyCost = 8f,
		ProjectileTemplate = new ProjectileTemplate { Speed = 1000f, Lifetime = 8f },
		Scaling = new RuneScalingDef { AcuityToCastSpeed = 2f }
	};

	public static readonly RuneDef BeamMethod = new RuneDef
	{
		Id = "method_beam",
		DisplayName = "Beam Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Beam,
		Range = 1500f,
		CastDelay = 0.10f,
		EnergyCost = 3f,
		Scaling = new RuneScalingDef { AcuityToCastSpeed = 2f }
	};

	public static readonly RuneDef NovaMethod = new RuneDef
	{
		Id = "method_nova",
		DisplayName = "Nova Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Nova,
		AoERadius = 750f,
		CastDelay = 0.5f,
		EnergyCost = 20f,
		Scaling = new RuneScalingDef { AcuityToRange = 1f }
	};

	// Named helpers instead of lambdas — see the hotreload note further down.
	private static SpellEffect FleetnessEffect( string id, string name, float moveSpeedPercent, float jumpPercent = 0f, float flatPoise = 0f )
	{
		var buff = new BuffDef( id, name, 20f );
		buff.Modifiers.Add( new BuffModifier( "MoveSpeed", moveSpeedPercent, ModifierType.Percent ) );
		if ( jumpPercent != 0f ) buff.Modifiers.Add( new BuffModifier( "JumpPower", jumpPercent, ModifierType.Percent ) );
		if ( flatPoise != 0f ) buff.Modifiers.Add( new BuffModifier( "MaxPoise", flatPoise ) );
		return new SpellEffect { Type = SpellEffectType.Buff, Buff = buff, Duration = buff.Duration };
	}

	private static SpellEffect LaunchEffect( SpellImpulseDirection direction, float strength )
		=> new SpellEffect { Type = SpellEffectType.Impulse, Direction = direction, Strength = strength };

	// Base Effect = neutral version (no Force rune). All numbers are placeholders to tune.
	public static readonly RuneDef Fleetness = new()
	{
		Id = "cantrip_fleetness",
		DisplayName = "Fleetness",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Self,
		CastDelay = 1f,
		EnergyCost = 30f,
		Effect = FleetnessEffect( "cantrip_fleetness", "Fleetness", 35f ),
		ElementEffects = new()
		{
			[RuneElementTag.Air] = FleetnessEffect( "fleetness_air", "Gale Step", 40f, jumpPercent: 30f ),
			[RuneElementTag.Fire] = FleetnessEffect( "fleetness_fire", "Ember Rush", 45f ),
			[RuneElementTag.Frost] = FleetnessEffect( "fleetness_frost", "Ice Skate", 30f ), // TODO longer slide: needs a SlideControl hook
			[RuneElementTag.Earth] = FleetnessEffect( "fleetness_earth", "Stone Stride", 20f, flatPoise: 20f )
		},
		Scaling = new RuneScalingDef { WillToEffectPotency = 1f, WisdomToDuration = 1f }
	};

	public static readonly RuneDef Launch = new()
	{
		Id = "cantrip_launch",
		DisplayName = "Launch",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Self,
		CastDelay = 0.05f,
		EnergyCost = 30f,
		Effect = LaunchEffect( SpellImpulseDirection.WorldUp, 9000f ),
		ElementEffects = new()
		{
			[RuneElementTag.Air] = LaunchEffect( SpellImpulseDirection.WorldUp, 11000f ), // highest jump
			[RuneElementTag.Fire] = LaunchEffect( SpellImpulseDirection.Aim, 8000f ),     // blast dash
			[RuneElementTag.Frost] = LaunchEffect( SpellImpulseDirection.Aim, 6000f ),    // ice skid
			[RuneElementTag.Earth] = LaunchEffect( SpellImpulseDirection.WorldUp, 7000f ) // TODO slam on landing
		},
		Scaling = new RuneScalingDef { WillToEffectPotency = 1f }
	};

	// Imbue: the Force rune supplies element + damage; each weapon hit gets Strength x that damage
	// (and the element tag) for Duration seconds. Replaces the old flat WeaponDamage "Ember Weapon" buff.
	public static readonly RuneDef ImbueMethod = new()
	{
		Id = "method_imbue",
		DisplayName = "Imbue Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Imbue,
		CastDelay = 0.25f,
		EnergyCost = 4f,
		Effect = new SpellEffect { Type = SpellEffectType.Imbue, Strength = 0.3f, Duration = 20f },
		// No WillToEffectPotency: Will already scales the Force damage the imbue is a ratio of.
		Scaling = new RuneScalingDef { WisdomToDuration = 1f }
	};

	public static readonly RuneDef ConeMethod = new()
	{
		Id = "method_cone",
		DisplayName = "Cone Method",
		Category = RuneCategory.Method,
		DeliveryType = RuneDeliveryType.Cone,
		Range = 1500f,
		ConeAngle = 90f,
		ConeRequiresLineOfSight = true,
		CastDelay = 0.25f,
		EnergyCost = 20f,
		Scaling = new RuneScalingDef { AcuityToRange = 1f }
	};
 
	public static readonly RuneDef EmpowerModifier = new RuneDef
	{
		Id = "mod_empower",
		DisplayName = "Empower",
		Category = RuneCategory.Modifier,
		EnergyCost = 5f,
		ModifierEffect = ApplyEmpower
	};
 
	public static readonly RuneDef DualCast = new RuneDef
	{
		Id = "mod_dualcast",
		DisplayName = "Dual Cast",
		Category = RuneCategory.Multicast,
		MulticastDrawCount = 2,
		MulticastDelay = 0.15f,
		EnergyCost = 8f,
		ModifierEffect = ApplyDualCastSpread
	};

	// Named methods instead of lambdas — static lambdas break after s&box hotreload
	// ("Unable to find matching substitution for a lambda method").
	static void ApplyEmpower( SpellContext ctx ) => ctx.DamageMultiplier *= 1.5f;
	static void ApplyDualCastSpread( SpellContext ctx ) => ctx.SpreadAngle += 15f;
 
	public static readonly RuneDef ClusterTrigger = new RuneDef
	{
		Id = "trigger_cluster",
		DisplayName = "Explosive Trigger",
		Category = RuneCategory.Trigger,
		EnergyCost = 10f,
		TriggerNestedRunes = new List<RuneDef>
		{
			FireForce,
			new RuneDef
			{
				Id = "sub_nova",
				Category = RuneCategory.Method,
				DeliveryType = RuneDeliveryType.Nova,
				AoERadius = 200f
			}
		}
	};
 
	private static RuneDef ForceFor( string element ) => element switch
	{
		"fire" => FireForce,
		"frost" => FrostForce,
		"air" => AirForce,
		"earth" => EarthForce,
		_ => null
	};

	private static RuneDef MethodFor( string kind ) => kind switch
	{
		"ball" => ProjectileMethod,
		"blast" => BlastMethod,
		"beam" => BeamMethod,
		"nova" => NovaMethod,
		"cone" => ConeMethod,
		"fleetness" => Fleetness,
		"launch" => Launch,
		"weapon" => ImbueMethod,
		_ => null
	};

	/// <summary>"fire_nova", "air_launch", "frost_weapon" ... = that element's Force rune + that Method rune.</summary>
	private static bool TryBuildElementPreset( string key, out List<RuneDef> runes )
	{
		runes = null;
		var parts = key.Split( '_' );
		if ( parts.Length != 2 ) return false;
		var force = ForceFor( parts[0] );
		var method = MethodFor( parts[1] );
		if ( force == null || method == null ) return false;
		runes = new List<RuneDef> { force, method };
		return true;
	}

	public static List<RuneDef> GetPreset( string presetName )
	{
		var key = presetName.ToLower();
		if ( TryBuildElementPreset( key, out var elemental ) ) return elemental;

		return key switch
		{
			"fireball" => new List<RuneDef> { FireForce, ProjectileMethod },
			"empowered_fireball" => new List<RuneDef> { EmpowerModifier, FireForce, ProjectileMethod },
			"dual_frost_beam" => new List<RuneDef> { DualCast, FrostForce, BeamMethod },
			"cluster_bomb" => new List<RuneDef> { DualCast, ClusterTrigger, FrostForce, ProjectileMethod },
			"raw_force" => new List<RuneDef> { FrostForce, NovaMethod },
			"airball" => new List<RuneDef> { AirForce, ProjectileMethod },
			"frostball" => new List<RuneDef> { FrostForce, ProjectileMethod },
			"earthball" => new List<RuneDef> { EarthForce, ProjectileMethod },
			"blast" => new List<RuneDef> { FireForce, BlastMethod },
			"fleetness" => new List<RuneDef> { FrostForce, Fleetness },
			"launch" => new List<RuneDef> { FrostForce, Launch },
			"ember_weapon" => new List<RuneDef> { FrostForce, ImbueMethod },
			"shockwave" => new List<RuneDef> { FrostForce, ConeMethod },
			_ => new List<RuneDef> { FireForce, ProjectileMethod }
		};
	}
}
