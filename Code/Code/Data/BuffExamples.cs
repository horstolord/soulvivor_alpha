namespace Sandbox.Code.Data;
using Sandbox.Code.Systems;

/// <summary>
/// Example buff definitions for testing and as templates.
/// You can add more here or create them dynamically at runtime.
/// </summary>
public static class BuffExamples
{
	/// <summary>
	/// A strength buff that increases Might by 5 points (flat) for 10 seconds.
	/// This will cause derived stats to recalculate, increasing max stagger.
	/// </summary>
	public static BuffDef StrengthBoost => new BuffDef( "strength_boost", "Strength Boost", 10f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "Might", 5f, ModifierType.Flat )
		}
	};

	/// <summary>
	/// A haste buff that increases AttackSpeed and MoveSpeed by 20% for 8 seconds.
	/// </summary>
	public static BuffDef Haste => new BuffDef( "haste", "Haste", 20f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "AttackSpeed", 100f, ModifierType.Percent ),
			new BuffModifier( "MoveSpeed", 100f, ModifierType.Percent )
		}
	};

	/// <summary>
	/// A damage buff that increases DamageMultiplier by 30% for 12 seconds.
	/// </summary>
	public static BuffDef DamageBoost => new BuffDef( "damage_boost", "Damage Boost", 12f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "DamageMultiplier", 30f, ModifierType.Percent )
		}
	};

	/// <summary>
	/// A tough skin buff that increases Armor by 10 flat points for 15 seconds.
	/// </summary>
	public static BuffDef ToughSkin => new BuffDef( "tough_skin", "Tough Skin", 15f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "Armor", 10f, ModifierType.Flat )
		}
	};

	/// <summary>
	/// A regeneration buff that increases HealthRegen by 2.0 flat points for 20 seconds.
	/// </summary>
	public static BuffDef Regeneration => new BuffDef( "regen", "Regeneration", 20f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "HealthRegen", 5f, ModifierType.Flat )
		}
	};

	/// <summary>
	/// A complex buff that combines multiple effects for 10 seconds.
	/// Increases Agility by 3, AttackSpeed by 15%, and MoveSpeed by 15%.
	/// </summary>
	public static BuffDef BerserkMode => new BuffDef( "berserk", "Berserk", 10f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "Swiftness", 3f, ModifierType.Flat ),
			new BuffModifier( "AttackSpeed", 15f, ModifierType.Percent ),
			new BuffModifier( "MoveSpeed", 15f, ModifierType.Percent ),
			new BuffModifier( "DamageMultiplier", 25f, ModifierType.Percent )
		}
	};

	/// <summary>
	/// A short speed boost for the slide traversal action (0.45s).
	/// </summary>
	public static BuffDef Slide => new BuffDef( "slide_speed", "Slide", 0.45f )
	{
		Modifiers = new List<BuffModifier>
		{
			new BuffModifier( "MoveSpeed", 500f, ModifierType.Flat )
		}
	};
}
