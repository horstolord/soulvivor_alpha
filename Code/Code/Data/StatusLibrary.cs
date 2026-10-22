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

	public static readonly StatusDef[] All = { Bleed, Burn, Chill, Shatter };
}
