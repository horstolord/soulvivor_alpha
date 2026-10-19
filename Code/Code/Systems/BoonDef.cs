using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// A run-scoped power-up. Stat modifiers and procs are authored per stack; BoonControl scales them
/// evenly by stack count (value x stacks, proc potency = stacks), so every stack is as good as the last.
/// </summary>
public class BoonDef
{
	public string Id;
	public string DisplayName;
	public string Description;
	public ItemRarity Rarity = ItemRarity.Common;
	public HashSet<string> Tags = new();

	/// <summary>Relative weight among boons of the same rarity when an offer is rolled.</summary>
	public float Weight = 1f;

	/// <summary>Stack cap. 0 or less = unlimited. Maxed boons are left out of offers.</summary>
	public int MaxStacks = 10;

	/// <summary>Stat modifiers granted PER STACK (same ModData the items use).</summary>
	public List<ModData> Modifiers = new();

	/// <summary>Procs granted by this boon; their potency equals the stack count.</summary>
	public List<ProcDef> Procs = new();
}

/// <summary>One pick-1-of-N choice waiting for the player.</summary>
public sealed class BoonOffer
{
	public List<BoonDef> Choices = new();

	/// <summary>Header text for the UI ("Level Up", "Elite Slain", ...).</summary>
	public string SourceLabel;
}
