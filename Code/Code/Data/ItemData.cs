namespace Sandbox.Code.Data;

public enum ItemCategory
{
	Equipment,
	Consumable,
	Material,
	KeyItem
}
public enum ItemRarity
{
	Common,
	Magic,
	Rare,
	Epic,
	Legendary,
	Mythical,
	Divine
}
public enum EquipmentSlot
{
	None,
	Head,
	Chest,
	Hands,
	Belt,
	Feet,
	MainHand1,
	OffHand1,
	MainHand2,
	OffHand2,
	MainHand3,
	OffHand3,
	Ring1,
	Ring2,
	Amulet,
	Flask1,
	Flask2
}

public static class ItemData
{
	public static ItemDef rustySword = new ItemDef
	{
		Id = "rusty_sword",
		Name = "Rusty Sword",
		Description = "An old blade, worn but still sharp.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "starter-gear", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.MainHand1,
			Stats = new EquipmentStatBlock
			{
				BaseDamage = 14,
				BaseAttackSpeed = 2.0f,
				PoiseDamage = 6,
				Weight = 4
			},
			WeaponVisual = new WeaponVisualDef{ PrefabPath = "swordtest.prefab"}
		},
		Mods = new()
		{
			new ModData("Might", 1, ModifierType.Flat)
		}
	};
	public static ItemDef tatteredHood = new ItemDef
	{
		Id = "tattered_hood",
		Name = "Tattered Hood",
		Description = "A frayed hood for concealing one's misery.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "armor", "cloth", "light-armor", "starter-gear", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Head,
			Stats = new EquipmentStatBlock
			{
				Weight = 1,
				Armor = 75f
			}
		},
		Mods = new()
		{
			new ModData("Will", 1, ModifierType.Flat),
			new ModData("MaxEnergy", 10, ModifierType.Flat)
		}
	};
	public static ItemDef healingFlask = new ItemDef
	{
		Id = "healing_flask",
		Name = "Vitality Flask",
		Description = "Restores 40 Health on use. Charges refill when enemies are slain.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Magic,
		Stackable = false,
		Tags = new() { "flask", "healing", "consumable", "starter-gear" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Flask1
		},
		Consumable = new ConsumableData
		{
			Charges = 3,
			RestoreHealth = 40f,
			UseEffectId = "heal_health"
		}
	};
	public static ItemDef manaFlask = new ItemDef
	{
		Id = "mana_flask",
		Name = "Aether Flask",
		Description = "Restores 30 Mana on use. Charges refill when enemies are slain.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Magic,
		Stackable = false,
		Tags = new() { "flask", "mana", "consumable", "starter-gear" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Flask2
		},
		Consumable = new ConsumableData
		{
			Charges = 3,
			RestoreEnergy = 30f,
			UseEffectId = "restore_mana"
		}
	};
	public static ItemDef strengthPotion = new ItemDef
	{
		Id = "strength_potion",
		Name = "Elixir of Might",
		Description = "A dense crimson potion. Grants +5 Might for 10 seconds when consumed.",
		Category = ItemCategory.Consumable,
		Rarity = ItemRarity.Rare,
		Stackable = true,
		MaxStack = 10,
		Tags = new() { "potion", "buff", "consumable" },
		Consumable = new ConsumableData
		{
			Charges = 1,
			BuffEffect = BuffExamples.StrengthBoost,
			UseEffectId = "buff_might"
		}
	};
	public static ItemDef hastePotion = new ItemDef
	{
		Id = "haste_potion",
		Name = "Potion of Haste",
		Description = "A sparkling golden elixir. Boosts speed by 20% for 8 seconds when consumed.",
		Category = ItemCategory.Consumable,
		Rarity = ItemRarity.Magic,
		Stackable = true,
		MaxStack = 10,
		Tags = new() { "potion", "buff", "consumable" },
		Consumable = new ConsumableData
		{
			Charges = 1,
			BuffEffect = BuffExamples.Haste,
			UseEffectId = "buff_haste"
		}
	};
	public static ItemDef ironSkinPotion = new ItemDef
	{
		Id = "ironskin_potion",
		Name = "Ironskin Potion",
		Description = "A dense metallic potion. Grants +10 Armor for 15 seconds when consumed.",
		Category = ItemCategory.Consumable,
		Rarity = ItemRarity.Magic,
		Stackable = true,
		MaxStack = 10,
		Tags = new() { "potion", "buff", "consumable" },
		Consumable = new ConsumableData
		{
			Charges = 1,
			BuffEffect = BuffExamples.ToughSkin,
			UseEffectId = "buff_ironskin"
		}
	};
	public static ItemDef ironOre = new ItemDef
	{
		Id = "iron_ore",
		Name = "Iron Ore",
		Description = "Raw ore used in forging.",
		Category = ItemCategory.Material,
		Rarity = ItemRarity.Common,
		Stackable = true,
		MaxStack = 99,
		Tags = new() { "material", "ore", "metal" },
		Crafting = new CraftingData
		{
			MaterialValue = 1,
			MaterialTags = new() { "metal", "ore", "tier1" }
		}
	};

	public static ItemDef shortsword = new ItemDef
	{
		Id = "shortsword",
		Name = "Shortsword",
		Description = "A basic blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.MainHand1,
			Stats = new EquipmentStatBlock { BaseDamage = 10, BaseAttackSpeed = 2f, PoiseDamage = 4, Weight = 3 },
			WeaponVisual = new WeaponVisualDef{ PrefabPath = "sword1.prefab"}
		},
		Mods = new()
	};
	public static ItemDef longsword = new ItemDef
	{
		Id = "longsword",
		Name = "Longsword",
		Description = "A basic long blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.MainHand1,
			Stats = new EquipmentStatBlock { BaseDamage = 24, BaseAttackSpeed = 1f, PoiseDamage = 4, Weight = 3 }
		},
		Mods = new()
	};
	
	public static ItemDef dagger = new ItemDef
	{
		Id = "dagger",
		Name = "Dagger",
		Description = "A basic blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.MainHand1,
			Stats = new EquipmentStatBlock { BaseDamage = 8, BaseAttackSpeed = 3f, PoiseDamage = 4, Weight = 3 }
		},
		Mods = new()
	};

	public static ItemDef leatherCap = new ItemDef
	{
		Id = "leather_cap",
		Name = "Leather Cap",
		Description = "Simple protection, better than nothing.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "armor", "leather", "light-armor", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Head,
			Stats = new EquipmentStatBlock { Weight = 1, Armor = 4f }
		},
		Mods = new()
	};
	
	public static ItemDef leatherArmor = new ItemDef
	{
		Id = "leather_armor",
		Name = "Leather Armor",
		Description = "Simple protection, better than nothing.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "armor", "leather", "light-armor", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Chest,
			Stats = new EquipmentStatBlock { Weight = 1, Armor = 4f }
		},
		Mods = new()
	};
	
	public static ItemDef leatherBoot = new ItemDef
	{
		Id = "leather_boot",
		Name = "Leather Boots",
		Description = "Simple protection, better than nothing.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "armor", "leather", "light-armor", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Feet,
			Stats = new EquipmentStatBlock { Weight = 1, Armor = 4f }
		},
		Mods = new()
	};
}

