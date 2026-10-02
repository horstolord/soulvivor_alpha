namespace Sandbox.Code.Systems;

public class BuffDef
{
	public string Id { get; set; }
	public string DisplayName { get; set; }
	public float Duration { get; set; } // In seconds
	public List<BuffModifier> Modifiers { get; set; } = new();

	public BuffDef( string id, string displayName, float duration )
	{
		Id = id;
		DisplayName = displayName;
		Duration = duration;
	}

	public BuffDef Clone()
	{
		var clone = new BuffDef( Id, DisplayName, Duration );
		foreach ( var modifier in Modifiers )
			clone.Modifiers.Add( new BuffModifier( modifier.StatName, modifier.Value, modifier.Type ) );
		return clone;
	}
}
