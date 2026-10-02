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
		_ => null
	};
}
