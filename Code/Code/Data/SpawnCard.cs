using Sandbox.Code.Actors;

namespace Sandbox.Code.Data;

/// <summary>
/// Defines a spawnable mob type and its cost to the SpawnDirector.
/// </summary>
public class SpawnCard
{
	/// <summary>Key into MobRegistry — also set as Enemy.PresetOverride on spawn.</summary>
	public string MobPresetId   { get; set; }
	/// <summary>
	/// Resource path to this archetype's prefab (e.g. "goblin_ranged.prefab"), resolved via
	/// ResourceLibrary.TryGet. Static data can't hold an
	/// Editor-dragged GameObject reference(I think), which is why this is a path, not a GameObject.
	/// </summary>
	public string PrefabPath    { get; set; }
	/// <summary>Director credit cost to spawn one of these.</summary>
	public float  Cost          { get; set; }
	/// <summary>Relative probability weight vs. other affordable cards.</summary>
	public float  Weight        { get; set; } = 1f;
	/// <summary>Minimum difficulty value before this card becomes available.</summary>
	public float  MinDifficulty { get; set; } = 0f;
	public string DisplayName   { get; set; }
	public MobRank Rank { get; set; } = MobRank.Normal;
}

/// <summary>
/// All cards available to the SpawnDirector, defined in code.
/// Add new mob types here as they are created.
/// </summary>
public static class SpawnCardRegistry
{
	public static List<SpawnCard> All { get; } = new()
	{
		new SpawnCard
		{
			MobPresetId   = "goblin",
			PrefabPath    = "goblin.prefab",
			DisplayName   = "Goblin Scout",
			Cost          = 10f,
			Weight        = 10f,
			MinDifficulty = 0f,
		},
		new SpawnCard
		{
			MobPresetId   = "orc",
			PrefabPath    = "orc.prefab",
			DisplayName   = "Orc Berserker",
			Cost          = 15f,
			Weight        = 1f,
			MinDifficulty = 0f,  // 
		},
		new SpawnCard
		{
			MobPresetId   = "ogre",
			PrefabPath    = "ogre.prefab",
			DisplayName   = "Ogre Brute",
			Cost          = 40f,
			Weight        = 0.5f,
			MinDifficulty = 0f,
			Rank          = MobRank.Boss,
		},
		// Example new archetype. Swap PrefabPath for  real ranged prefab
		// (Enemy + NavMeshAgent + CharacterController + CombatComponent with ArrowPrefab
		// set + RangedKiteBehavior attached). Reusing goblin stats 
		new SpawnCard
		{
			MobPresetId   = "goblin",
			PrefabPath    = "goblin_ranged.prefab",
			DisplayName   = "Goblin Slinger",
			Cost          = 12f,
			Weight        = 1f,
			MinDifficulty = 0f,
		},
	};
}
