using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// All status definitions. Every number is a placeholder to tune.
/// Plain static fields, no lambdas (hotreload).
/// </summary>
public static class StatusLibrary
{
	// Bleed: stacking DoT that ignores armor (tick damage carries no Physical tag).
	public static readonly StatusDef Bleed = new()
	{
		Type = StatusType.Bleed,
		DisplayName = "Bleed",
		RequiredTag = AttackTag.Physical,
		Duration = 5f,
		MaxStacks = 10,
		TickInterval = 0.5f,
		DamageFraction = 0.6f,
		DamageTags = new() { AttackTag.Bleed }
	};

	// Burn: stacking DoT, fire resistance applies to the ticks.
	public static readonly StatusDef Burn = new()
	{
		Type = StatusType.Burn,
		DisplayName = "Burn",
		RequiredTag = AttackTag.Fire,
		Duration = 4f,
		MaxStacks = 8,
		TickInterval = 0.5f,
		DamageFraction = 0.8f,
		DamageTags = new() { AttackTag.Fire }
	};

	// Chill: stacking slow that also drains poise. Pass 2: poise reaching 0 while chilled becomes Frozen.
	public static readonly StatusDef Chill = new()
	{
		Type = StatusType.Chill,
		DisplayName = "Chill",
		RequiredTag = AttackTag.Frost,
		Duration = 6f,
		MaxStacks = 8,
		PoiseDrainFraction = 0.12f,
		Modifiers = new()
		{
			new BuffModifier( "MoveSpeed", -8f, ModifierType.Percent ),
			new BuffModifier( "AttackSpeed", -6f, ModifierType.Percent ),
			new BuffModifier( "CastSpeed", -6f, ModifierType.Percent )
		}
	};

	// Shatter: stacking attrition on armor, max health and max poise.
	public static readonly StatusDef Shatter = new()
	{
		Type = StatusType.Shatter,
		DisplayName = "Shatter",
		RequiredTag = AttackTag.Earth,
		Duration = 8f,
		MaxStacks = 5,
		Modifiers = new()
		{
			new BuffModifier( "Armor", -10f, ModifierType.Percent ),
			new BuffModifier( "MaxHealth", -4f, ModifierType.Percent ),
			new BuffModifier( "MaxPoise", -6f, ModifierType.Percent )
		}
	};

	// Hypothermia: attrition on stamina. Zero stamina already triggers the existing heavy stun, so no extra CC here.
	public static readonly StatusDef Hypothermia = new()
	{
		Type = StatusType.Hypothermia,
		DisplayName = "Hypothermia",
		RequiredTag = AttackTag.Frost,
		ChanceScale = 0.8f,
		Duration = 6f,
		MaxStacks = 5,
		Modifiers = new()
		{
			new BuffModifier( "MaxStamina", -8f, ModifierType.Percent ),
			new BuffModifier( "StaminaRegen", -12f, ModifierType.Percent )
		}
	};

	// Frozen: not rolled. StatusControl applies it when poise breaks while chilled (consumes the Chill stacks).
	public static readonly StatusDef Frozen = new()
	{
		Type = StatusType.Frozen,
		DisplayName = "Frozen",
		Duration = 2.5f,
		IsCrowdControl = true,
		EnemyOnly = true,
		Behavior = new FrozenBehavior()
	};

	// Fear: enemies flee; the player only gets a damage debuff.
	public static readonly StatusDef Fear = new()
	{
		Type = StatusType.Fear,
		DisplayName = "Fear",
		RequiredTag = AttackTag.Fire,
		ChanceScale = 0.6f,
		Duration = 3f,
		IsCrowdControl = true,
		PlayerModifiers = new() { new BuffModifier( "DamageMultiplier", -8f, ModifierType.Percent ) },
		Behavior = new FearBehavior()
	};

	// KnockUp: ragdoll launch. Physics does the rest.
	public static readonly StatusDef KnockUp = new()
	{
		Type = StatusType.KnockUp,
		DisplayName = "Knock Up",
		RequiredTag = AttackTag.Air,
		ChanceScale = 0.5f,
		Duration = 1.5f,
		IsCrowdControl = true,
		EnemyOnly = true,
		Behavior = new KnockUpBehavior()
	};

	// Shock: drains energy, delays casts (enemies), arcs to a neighbour. Instant effect; the instance is a short mark.
	public static readonly StatusDef Shock = new()
	{
		Type = StatusType.Shock,
		DisplayName = "Shock",
		RequiredTag = AttackTag.Air,
		ChanceScale = 0.8f,
		Duration = 2f,
		Behavior = new ShockBehavior()
	};

	// KnockDown: ragdoll stun, no launch.
	public static readonly StatusDef KnockDown = new()
	{
		Type = StatusType.KnockDown,
		DisplayName = "Knock Down",
		RequiredTag = AttackTag.Earth,
		ChanceScale = 0.5f,
		Duration = 2.5f,
		IsCrowdControl = true,
		EnemyOnly = true,
		Behavior = new KnockDownBehavior()
	};

	/// <summary>Everything that can be rolled from a hit tag. Frozen is deliberately absent.</summary>
	public static readonly StatusDef[] All =
		{ Bleed, Burn, Chill, Shatter, Hypothermia, Fear, KnockUp, Shock, KnockDown };
}
