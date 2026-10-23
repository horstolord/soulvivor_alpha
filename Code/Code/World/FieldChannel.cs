namespace Sandbox.Code.World;

/// <summary>
/// One float per cell per channel. To add a channel, add a value above <see cref="Count"/>;
/// storage, sampling and emitters pick it up automatically.
/// </summary>
public enum FieldChannel
{
	/// <summary>
	/// World light: ambient + level-authored emitters. This is the ONLY light channel that
	/// threat / reward scaling may read.
	/// </summary>
	Light = 0,

	/// <summary>
	/// Transient / player-made light (spell lights, fireballs). For visuals, third eye, stealth —
	/// never for difficulty.
	/// </summary>
	LightDynamic = 1,

	// Mana, Heat, Flammability ... go here

	/// <summary>Sentinel — keep last.</summary>
	Count
}
