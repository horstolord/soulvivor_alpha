namespace Sandbox.Code.Data;

public class ModData
{
	public string StatName { get; set; }
	public float Value { get; set; }
	public ModifierType Type { get; set; }
	public ModData(string statName, float value, ModifierType type = ModifierType.Flat)
	{
		StatName = statName;
		Value = value;
		Type = type;
	}
}
public class ItemModifier
{
	public string StatName { get; set; }
	public float Value { get; set; }
	public ModifierType Type { get; set; }
	public ItemModifier( string statName, float value, ModifierType type = ModifierType.Flat )
	{
		StatName = statName;
		Value = value;
		Type = type;
	}
}
