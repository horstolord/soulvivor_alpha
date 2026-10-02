using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Defines a single stat modification that a buff applies.
/// </summary>
public class BuffModifier
{
	/// <summary>
	/// The name of the stat to modify (must match ComprehensiveStatSheet property names)
	/// </summary>
	public string StatName { get; set; }

	/// <summary>
	/// The value to apply. For Flat: +5, -10. For Percent: +10 (means 10%), -20 (means -20%)
	/// </summary>
	public float Value { get; set; }

	/// <summary>
	/// How to apply the modification
	/// </summary>
	public ModifierType Type { get; set; }

	public BuffModifier( string statName, float value, ModifierType type = ModifierType.Flat )
	{
		StatName = statName;
		Value = value;
		Type = type;
	}
}
