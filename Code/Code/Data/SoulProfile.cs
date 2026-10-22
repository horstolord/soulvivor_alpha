using System.Collections.Generic;

namespace Sandbox.Code.Data;

/// <summary>
/// Persistent, vessel-independent meta state of the player's soul. Serialized as JSON by MetaProgression.
/// Pure data: all rules (costs, validation, derived values) live in MetaProgression.
/// SoulLevel is the PERMANENT level and is unrelated to LevelComponent.Level (the in-run level).
/// </summary>
public sealed class SoulProfile
{
	/// <summary>Bump when the shape changes in a way that needs migration on load.</summary>
	public int Version { get; set; } = 1;

	/// <summary>Banked souls, the meta currency. Only changes via deposit (run end) or purchase.</summary>
	public float Souls { get; set; }

	/// <summary>Permanent soul level. Also the total number of talent points.</summary>
	public int SoulLevel { get; set; } = MetaProgressionConfig.StartingSoulLevel;

	/// <summary>Attribute name (matches StatSheet.GetStat, e.g. "Might") to points allocated.</summary>
	public Dictionary<string, int> AttributeAllocations { get; set; } = new();

	/// <summary>Talent id to rank. Definitions and the tree itself come later; only the shape is fixed now.</summary>
	public Dictionary<string, int> TalentRanks { get; set; } = new();
}
