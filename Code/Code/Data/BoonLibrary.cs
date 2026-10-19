using Sandbox.Code.Systems;
using Sandbox.Code.World;

namespace Sandbox.Code.Data;

/// <summary>
/// All boons available to offers. PLACEHOLDER CONTENT: these exist to exercise the mechanics
/// (flat stat, percent stat, leech proc, buff proc, nova proc). Names, numbers and rarities are yours to replace.
/// </summary>
public static class BoonLibrary
{
	public static List<BoonDef> All { get; } = new()
	{
		new BoonDef
		{
			Id = "iron_will", DisplayName = "Iron Will", Rarity = ItemRarity.Common,
			Description = "+3 Endurance per stack.",
			Modifiers = { new ModData( "Endurance", 3f, ModifierType.Flat ) }
		},
		new BoonDef
		{
			Id = "keen_edge", DisplayName = "Keen Edge", Rarity = ItemRarity.Common,
			Description = "+3% critical chance per stack.",
			Modifiers = { new ModData( "CritChance", 3f, ModifierType.Flat ) }
		},
		new BoonDef
		{
			Id = "fleet_foot", DisplayName = "Fleet Foot", Rarity = ItemRarity.Common,
			Description = "+5% movement speed per stack.",
			Modifiers = { new ModData( "MoveSpeed", 5f, ModifierType.Percent ) }
		},
		new BoonDef
		{
			Id = "vampiric_touch", DisplayName = "Vampiric Touch", Rarity = ItemRarity.Magic,
			Description = "Heal for 10% of damage dealt, per stack.",
			Procs = { ProcExamples.Vampiric }
		},
		new BoonDef
		{
			Id = "bloodlust", DisplayName = "Bloodlust", Rarity = ItemRarity.Rare,
			Description = "Kills grant +30% damage for 12s, per stack.",
			Procs = { ProcExamples.Bloodlust }
		},
		new BoonDef
		{
			Id = "cinder_burst", DisplayName = "Cinder Burst", Rarity = ItemRarity.Epic,
			Description = "25% chance on crit to detonate a fire nova on the target. Damage scales per stack.",
			Procs = { CinderBurst() }
		},
	};

	public static BoonDef Get( string id ) => All.FirstOrDefault( b => b.Id == id );

	// Named method instead of an inline initializer — keeps the list above readable.
	private static ProcDef CinderBurst() => new ProcDef
	{
		Id = "cinder_burst",
		DisplayName = "Cinder Burst",
		Trigger = ProcTrigger.Crit,
		Chance = 25f,
		InternalCooldown = 1f,
		Effects = new List<IProcEffect>
		{
			new CastRunesProcEffect { Runes = RuneLibrary.GetPreset( "fire_nova" ), FromEventPosition = true, AimAtOther = false }
		}
	};
}
