using System;
using System.Collections.Generic;
using Sandbox.Code.Systems;

namespace Sandbox.Code.World;

public enum RuneCategory { Force, Method, Modifier, Multicast, Trigger }

public enum RuneElementTag { Fire, Frost, Air, Earth, Light, Dark }

// Keep existing serialized values stable: SelfTouch was the old Nova behaviour.
public enum RuneDeliveryType { Projectile = 0, Beam = 1, Nova = 2, AoE = 3, Self = 4, Cone = 5, Imbue = 6, Blast = 7 }

public class RuneScalingDef
{
	public float WillToPower;
	public float AcuityToCastSpeed;
	public float WisdomToDuration;
	public float WillToEffectPotency;
	public float AcuityToRange;
}

/// <summary>How stored charge changes a spell sequence at full charge.</summary>
public class ChargeScalingDef
{
	public float BaseChargeSeconds = 1f;
	public float AcuityToChargeSpeed = 2f;
	public float DamageBonusAtMaxCharge = 0.5f;
	public float CostIncreaseAtMaxCharge = 0.5f;
	public float WillToDamageAtMaxCharge;

	public static ChargeScalingDef Default { get; } = new();
}

public class RuneDef : ICostable
{
	public string Id;
	public string DisplayName;
	public RuneCategory Category;
	public int RuneComplexity = 1; // Minimum Wisdom threshold to cast/equip

	public HashSet<AttackTag> SpellTags = new();

	// ICostable Implementation
	public float HealthCost { get; set; }
	public float StaminaCost { get; set; }
	public float EnergyCost { get; set; }

	public float CastDelay = 0.05f;
	public float RechargeDelayModifier = 0.0f;
	public float Range = 1000f;
	public float AoERadius = 150f;
	public float ConeAngle = 90f;
	public bool ConeRequiresLineOfSight = true;

	public RuneScalingDef Scaling;
	/// <summary>Optional charge scaling for method runes. The first configured method wins.</summary>
	public ChargeScalingDef ChargeScaling;

	// Force Runes: Base damage & Elemental metadata
	public float BasePower;
	public float StaminaDamage;
	public float KnockbackForce;
	public RuneElementTag? ElementTag;
	public Material VisualMaterial; // Element identity — applied to whatever the Method rune spawns

	// Method Runes: Delivery shape & Projectile Template
	public RuneDeliveryType DeliveryType = RuneDeliveryType.Projectile;
	public ProjectileTemplate ProjectileTemplate;
	public string ProjectilePrefabPath; // Self-contained prefab: mesh/particles + motion + Projectile

	public string BeamPrefabPath;
	/// <summary>Explicit prefab for any delivery shape. Leave empty to resolve per element (SpellVisualResolver).</summary>
	public string PrefabPath;
	/// <summary>Beam mesh length at scale 1; the mesh should extend along local X from its origin.</summary>
	public float BeamVisualLength = 100f;
	/// <summary>Beam hit radius in world units; author the mesh to approximately twice this width at scale 1.</summary>
	public float BeamRadius = 16f;
	/// <summary>Optional resolved effect carried by a Method rune; this is not a player-facing rune category.</summary>
	public SpellEffect Effect;
	/// <summary>Per-element replacements for Effect, picked by the Force rune's element (SpellContext.PrimaryElement).</summary>
	public Dictionary<RuneElementTag, SpellEffect> ElementEffects;
	// Modifier Runes: Mutator action for active SpellContext
	public Action<SpellContext> ModifierEffect;

	// Multicast Runes: Draw count for branching
	public int MulticastDrawCount = 1;

	// Trigger Runes: Nested payload to evaluate on hit/expire
	public List<RuneDef> TriggerNestedRunes = new();
}
