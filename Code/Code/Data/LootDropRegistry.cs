using System.Collections.Generic;

namespace Sandbox.Code.Data;

public class LootDropEntry
{
	public string ItemId;
	public float Weight = 1f;
	public int MinLevel = 1;
}

public static class LootDropRegistry
{
	public static List<LootDropEntry> All { get; } = new()
	{
		// Weapons
		new LootDropEntry { ItemId = "rusty_sword", Weight = 3f, MinLevel = 1 },
		new LootDropEntry { ItemId = "shortsword", Weight = 5f, MinLevel = 1 },
		new LootDropEntry { ItemId = "longsword", Weight = 4f, MinLevel = 5 },
		new LootDropEntry { ItemId = "dagger", Weight = 3f, MinLevel = 1 },
		
		// Armor
		new LootDropEntry { ItemId = "tattered_hood", Weight = 3f, MinLevel = 1 },
		new LootDropEntry { ItemId = "leather_cap", Weight = 5f, MinLevel = 1 },
		new LootDropEntry { ItemId = "leather_armor", Weight = 4f, MinLevel = 3 },
		new LootDropEntry { ItemId = "leather_boot", Weight = 4f, MinLevel = 3 },
		
		// Flasks
		new LootDropEntry { ItemId = "healing_flask", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "mana_flask", Weight = 4f, MinLevel = 1 },
		
		// Potions
		new LootDropEntry { ItemId = "strength_potion", Weight = 2f, MinLevel = 5 },
		new LootDropEntry { ItemId = "haste_potion", Weight = 2f, MinLevel = 5 },
		new LootDropEntry { ItemId = "ironskin_potion", Weight = 2f, MinLevel = 5 },
		
		// Materials
		new LootDropEntry { ItemId = "iron_ore", Weight = 6f, MinLevel = 1 },

		// Loot Pool 1.0 armor
		new LootDropEntry { ItemId = "tattered_robes", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "disciple_robes", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "doomsayer_robes", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "prophet_robes", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "mage_robes", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "tainted_leather", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "fur_armor", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "chain_armor", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "assassin_armor", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "gladiator_armor", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "tarnished_plate", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "knight_plate", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "elite_plate", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "death_plate", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "chaos_plate", Weight = 2f, MinLevel = 3 },

		// Melee weapons
		new LootDropEntry { ItemId = "worn_longsword", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "crude_machete", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "flint_cleaver", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "bastard_sword", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "zweihaender", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "streitaxt", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "shoddy_sledge", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "maul", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "morning_star", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "blacksmith_hammer", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "splintered_spear", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "soldier_spear", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "halberd", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "cross_spear", Weight = 2f, MinLevel = 3 },

		// Bows
		new LootDropEntry { ItemId = "shortbow", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "simple_bow", Weight = 4f, MinLevel = 1 },
		new LootDropEntry { ItemId = "long_bow", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "composite_bow", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "hunter_bow", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "war_bow", Weight = 2f, MinLevel = 3 },

		// Accessories
		new LootDropEntry { ItemId = "iron_ring", Weight = 3f, MinLevel = 1 },
		new LootDropEntry { ItemId = "floral_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "wedding_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "opal_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "ruby_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "sapphire_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "emerald_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "diamond_ring", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "crystal_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "curious_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "bone_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "scale_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "tooth_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "soldiers_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "church_amulet", Weight = 2f, MinLevel = 3 },
		new LootDropEntry { ItemId = "prisms_amulet", Weight = 2f, MinLevel = 3 },
	};

	public static ItemDef Resolve( string itemId ) => itemId switch
	{
		"rusty_sword" => ItemData.rustySword,
		"shortsword" => ItemData.shortsword,
		"longsword" => ItemData.longsword,
		"dagger" => ItemData.dagger,
		"tattered_hood" => ItemData.tatteredHood,
		"leather_cap" => ItemData.leatherCap,
		"leather_armor" => ItemData.leatherArmor,
		"leather_boot" => ItemData.leatherBoot,
		"healing_flask" => ItemData.healingFlask,
		"mana_flask" => ItemData.manaFlask,
		"strength_potion" => ItemData.strengthPotion,
		"haste_potion" => ItemData.hastePotion,
		"ironskin_potion" => ItemData.ironSkinPotion,
		"iron_ore" => ItemData.ironOre,
		"tattered_robes" => ItemData.tatteredRobes,
		"disciple_robes" => ItemData.discipleRobes,
		"doomsayer_robes" => ItemData.doomsayerRobes,
		"prophet_robes" => ItemData.prophetRobes,
		"mage_robes" => ItemData.mageRobes,
		"tainted_leather" => ItemData.taintedLeather,
		"fur_armor" => ItemData.furArmor,
		"chain_armor" => ItemData.chainArmor,
		"assassin_armor" => ItemData.assassinArmor,
		"gladiator_armor" => ItemData.gladiatorArmor,
		"tarnished_plate" => ItemData.tarnishedPlate,
		"knight_plate" => ItemData.knightPlate,
		"elite_plate" => ItemData.elitePlate,
		"death_plate" => ItemData.deathPlate,
		"chaos_plate" => ItemData.chaosPlate,
		"worn_longsword" => ItemData.wornLongsword,
		"crude_machete" => ItemData.crudeMachete,
		"flint_cleaver" => ItemData.flintCleaver,
		"bastard_sword" => ItemData.bastardSword,
		"zweihaender" => ItemData.zweihaender,
		"streitaxt" => ItemData.streitaxt,
		"shoddy_sledge" => ItemData.shoddySledge,
		"maul" => ItemData.maul,
		"morning_star" => ItemData.morningStar,
		"blacksmith_hammer" => ItemData.blacksmithHammer,
		"splintered_spear" => ItemData.splinteredSpear,
		"soldier_spear" => ItemData.soldierSpear,
		"halberd" => ItemData.halberd,
		"cross_spear" => ItemData.crossSpear,
		"shortbow" => ItemData.shortbow,
		"simple_bow" => ItemData.simpleBow,
		"long_bow" => ItemData.longBow,
		"composite_bow" => ItemData.compositeBow,
		"hunter_bow" => ItemData.hunterBow,
		"war_bow" => ItemData.warBow,
		"iron_ring" => ItemData.ironRing,
		"floral_ring" => ItemData.floralRing,
		"wedding_ring" => ItemData.weddingRing,
		"opal_ring" => ItemData.opalRing,
		"ruby_ring" => ItemData.rubyRing,
		"sapphire_ring" => ItemData.sapphireRing,
		"emerald_ring" => ItemData.emeraldRing,
		"diamond_ring" => ItemData.diamondRing,
		"crystal_amulet" => ItemData.crystalAmulet,
		"curious_amulet" => ItemData.curiousAmulet,
		"bone_amulet" => ItemData.boneAmulet,
		"scale_amulet" => ItemData.scaleAmulet,
		"tooth_amulet" => ItemData.toothAmulet,
		"soldiers_amulet" => ItemData.soldiersAmulet,
		"church_amulet" => ItemData.churchAmulet,
		"prisms_amulet" => ItemData.prismsAmulet,
		_ => null
	};
}
