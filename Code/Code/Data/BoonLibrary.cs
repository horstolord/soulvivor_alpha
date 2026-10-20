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
			Id = "test_ember_strikes", DisplayName = "Ember Strikes", Rarity = ItemRarity.Common,
			Description = "TEST: +25% of physical damage as extra fire per stack.",
			Modifiers = { new ModData( "PhysicalAsFire", 25f, ModifierType.Flat ) }
		},
		new BoonDef
		{
			Id = "test_flame_edge", DisplayName = "Flame Edge", Rarity = ItemRarity.Common,
			Description = "TEST: +5 flat fire damage per hit per stack.",
			Modifiers = { new ModData( "AddedFireDamage", 5f, ModifierType.Flat ) }
		},
		new BoonDef
		{
			Id = "test_twin_shot", DisplayName = "Twin Shot", Rarity = ItemRarity.Common,
			Description = "TEST: +1 projectile per stack (arrows and projectile spells).",
			Modifiers = { new ModData( "AdditionalProjectiles", 1f, ModifierType.Flat ) }
		},
		new BoonDef
		{
			Id = "test_long_reach", DisplayName = "Long Reach", Rarity = ItemRarity.Common,
			Description = "TEST: +25% melee reach per stack.",
			Modifiers = { new ModData( "Range", 25f, ModifierType.Percent ) }
		},
		new BoonDef
		{
			Id = "test_stone_skin", DisplayName = "Stone Skin", Rarity = ItemRarity.Common,
			Description = "TEST: take 15% less damage per stack.",
			Modifiers = { new ModData( "DamageTakenMultiplier", -15f, ModifierType.Flat ) }
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
