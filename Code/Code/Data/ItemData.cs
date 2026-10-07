using System.Collections.Generic;

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
	None = 0,
	Head = 1,
	Chest = 2,
	Hands = 3,
	Belt = 4,
	Feet = 5,
	Melee = 6,
	OffHand = 7,
	Ranged = 8,
	Ring1 = 12,
	Ring2 = 13,
	Amulet = 14,
	Flask1 = 15,
	Flask2 = 16
}

public static class ItemData
{
	public static ItemDef rustySword = new ItemDef
	{
		Id = "rusty_sword",
		WorldPrefabPath = ItemPrefabPath( "weapons/melee", "rusty_sword" ),
		Name = "Rusty Sword",
		Description = "An old blade, worn but still sharp.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "starter-gear", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Melee,
			WeaponClass = WeaponClass.Sword,
			Stats = new EquipmentStatBlock
			{
				BaseDamage = 14,
				BaseAttackSpeed = 2.0f,
				PoiseDamage = 6,
				Weight = 4
			},
			WeaponVisual = new WeaponVisualDef{ PrefabPath = "models/Weapons/swordtest.prefab"}
		},
		Implicits = new()
		{
			new ModData("Might", 1, ModifierType.Flat)
		}
	};
	public static ItemDef tatteredHood = new ItemDef
	{
		Id = "tattered_hood",
		WorldPrefabPath = ItemPrefabPath( "armor", "tattered_hood" ),
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
		Implicits = new()
		{
			new ModData("Will", 1, ModifierType.Flat),
			new ModData("MaxEnergy", 10, ModifierType.Flat)
		}
	};
	public static ItemDef healingFlask = new ItemDef
	{
		Id = "healing_flask",
		WorldPrefabPath = ItemPrefabPath( "consumables/flasks", "healing_flask" ),
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
		WorldPrefabPath = ItemPrefabPath( "consumables/flasks", "mana_flask" ),
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
		WorldPrefabPath = ItemPrefabPath( "consumables/potions", "strength_potion" ),
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
		WorldPrefabPath = ItemPrefabPath( "consumables/potions", "haste_potion" ),
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
		WorldPrefabPath = ItemPrefabPath( "consumables/potions", "ironskin_potion" ),
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
		WorldPrefabPath = ItemPrefabPath( "materials", "iron_ore" ),
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
		WorldPrefabPath = ItemPrefabPath( "weapons/melee", "swordtest2" ),
		Name = "Shortsword",
		Description = "A basic blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Melee,
			WeaponClass = WeaponClass.Sword,
			Stats = new EquipmentStatBlock { BaseDamage = 10, BaseAttackSpeed = 2f, PoiseDamage = 4, Weight = 3 },
			WeaponVisual = new WeaponVisualDef{ PrefabPath = "models/Weapons/sword1.prefab"}
		},
		Implicits = new()
	};
	public static ItemDef longsword = new ItemDef
	{
		Id = "longsword",
		WorldPrefabPath = ItemPrefabPath( "weapons/melee", "longsword" ),
		Name = "Longsword",
		Description = "A basic long blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Melee,
			WeaponClass = WeaponClass.Sword,
			Stats = new EquipmentStatBlock { BaseDamage = 24, BaseAttackSpeed = 1f, PoiseDamage = 4, Weight = 3 },
			WeaponVisual = new WeaponVisualDef { PrefabPath = ItemPrefabPath( "weapons/melee", "longsword" ) }
		},
		Implicits = new()
		{
			new ModData("PhysicalForce", 15, ModifierType.Flat)
		}
	};
	
	public static ItemDef dagger = new ItemDef
	{
		Id = "dagger",
		WorldPrefabPath = ItemPrefabPath( "weapons/melee", "dagger" ),
		Name = "Dagger",
		Description = "A basic blade found in the field.",
		Category = ItemCategory.Equipment,
		Rarity = ItemRarity.Common,
		Stackable = false,
		Tags = new() { "weapon", "sword", "metal", "tier1" },
		Equipment = new EquipmentData
		{
			Slot = EquipmentSlot.Melee,
			WeaponClass = WeaponClass.Dagger,
			Stats = new EquipmentStatBlock { BaseDamage = 8, BaseAttackSpeed = 3f, PoiseDamage = 4, Weight = 3 },
			WeaponVisual = new WeaponVisualDef { PrefabPath = ItemPrefabPath( "weapons/melee", "dagger" ) }
		},
		Implicits = new()
		{
			new ModData("CritChance", 3, ModifierType.Flat)
		}
	};

	public static ItemDef leatherCap = new ItemDef
	{
		Id = "leather_cap",
		WorldPrefabPath = ItemPrefabPath( "armor", "leather_cap" ),
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
		Implicits = new()
	};
	
	public static ItemDef leatherArmor = new ItemDef
	{
		Id = "leather_armor",
		WorldPrefabPath = ItemPrefabPath( "armor", "leather_armor" ),
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
		Implicits = new()
		{
			new ModData("Evasion", 3, ModifierType.Flat)
		}
	};
	
	public static ItemDef leatherBoot = new ItemDef
	{
		Id = "leather_boot",
		WorldPrefabPath = ItemPrefabPath( "armor", "leather_boot" ),
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
		Implicits = new()
	};

	// Loot Pool 1.0 equipment. Values are placeholders for a later balance pass.
	public static ItemDef tatteredRobes = CreateArmor( "tattered_robes", "Tattered Robes", "cloth", 3f, 2f, 1 );
	public static ItemDef discipleRobes = CreateArmor( "disciple_robes", "Disciple Robes", "cloth", 5f, 2f, 3 );
	public static ItemDef doomsayerRobes = CreateArmor( "doomsayer_robes", "Doomsayer Robes", "cloth", 6f, 2.5f, 3 );
	public static ItemDef prophetRobes = CreateArmor( "prophet_robes", "Prophet Robes", "cloth", 5f, 1.5f, 3 );
	public static ItemDef mageRobes = CreateArmor( "mage_robes", "Mage Robes", "cloth", 4f, 1.5f, 3 );

	public static ItemDef taintedLeather = CreateArmor( "tainted_leather", "Tainted Leather", "leather", 5f, 4f, 1 );
	public static ItemDef furArmor = CreateArmor( "fur_armor", "Fur Armor", "leather", 7f, 5f, 3 );
	public static ItemDef chainArmor = CreateArmor( "chain_armor", "Chain Armor", "metal", 10f, 8f, 3 );
	public static ItemDef assassinArmor = CreateArmor( "assassin_armor", "Assassin Armor", "leather", 8f, 3.5f, 3 );
	public static ItemDef gladiatorArmor = CreateArmor( "gladiator_armor", "Gladiator Armor", "metal", 11f, 7f, 3 );

	public static ItemDef tarnishedPlate = CreateArmor( "tarnished_plate", "Tarnished Plate", "metal", 10f, 10f, 1 );
	public static ItemDef knightPlate = CreateArmor( "knight_plate", "Knight Plate", "metal", 15f, 12f, 3 );
	public static ItemDef elitePlate = CreateArmor( "elite_plate", "Elite Plate", "metal", 18f, 13f, 3 );
	public static ItemDef deathPlate = CreateArmor( "death_plate", "Death Plate", "metal", 17f, 11f, 3 );
	public static ItemDef chaosPlate = CreateArmor( "chaos_plate", "Chaos Plate", "metal", 16f, 12f, 3 );

	public static ItemDef wornLongsword = CreateWeapon( "worn_longsword", "Worn Longsword", "sword", 16f, 1f, 6f, 5f, 1 );
	public static ItemDef crudeMachete = CreateWeapon( "crude_machete", "Crude Machete", "sword", 13f, 1.25f, 5f, 3.5f, 3 );
	public static ItemDef flintCleaver = CreateWeapon( "flint_cleaver", "Flint Cleaver", "axe", 19f, 0.9f, 8f, 6f, 3 );
	public static ItemDef bastardSword = CreateWeapon( "bastard_sword", "Bastard Sword", "sword", 23f, 0.9f, 8f, 6.5f, 3, true );
	public static ItemDef zweihaender = CreateWeapon( "zweihaender", "Zweihaender", "sword", 29f, 0.7f, 12f, 9f, 3, true );
	public static ItemDef streitaxt = CreateWeapon( "streitaxt", "Streitaxt", "axe", 26f, 0.75f, 11f, 8f, 3, true );

	public static ItemDef shoddySledge = CreateWeapon( "shoddy_sledge", "Shoddy Sledge", "hammer", 17f, 0.8f, 11f, 8f, 1, true );
	public static ItemDef maul = CreateWeapon( "maul", "Maul", "hammer", 28f, 0.65f, 17f, 12f, 3, true );
	public static ItemDef morningStar = CreateWeapon( "morning_star", "Morning Star", "mace", 22f, 0.9f, 12f, 8f, 3 );
	public static ItemDef blacksmithHammer = CreateWeapon( "blacksmith_hammer", "Blacksmith Hammer", "hammer", 20f, 0.85f, 14f, 9f, 3 );

	public static ItemDef splinteredSpear = CreateWeapon( "splintered_spear", "Splintered Spear", "spear", 13f, 1.1f, 7f, 4f, 1 );
	public static ItemDef soldierSpear = CreateWeapon( "soldier_spear", "Soldier Spear", "spear", 20f, 1f, 9f, 6f, 3, true );
	public static ItemDef halberd = CreateWeapon( "halberd", "Halberd", "polearm", 27f, 0.8f, 13f, 9f, 3, true );
	public static ItemDef crossSpear = CreateWeapon( "cross_spear", "Cross Spear", "spear", 23f, 0.95f, 10f, 7f, 3, true );

	public static ItemDef shortbow = CreateBow( "shortbow", "Shortbow", 12f, 1f, 4f, 2.5f, 1, 0.45f, 1.5f, 1f );
	public static ItemDef simpleBow = CreateBow( "simple_bow", "Simple Bow", 11f, 1.1f, 4f, 2.5f, 1 );
	public static ItemDef longBow = CreateBow( "long_bow", "Long Bow", 20f, 0.8f, 5f, 4f, 3 );
	public static ItemDef compositeBow = CreateBow( "composite_bow", "Composite Bow", 18f, 1.1f, 5f, 3.5f, 3 );
	public static ItemDef hunterBow = CreateBow( "hunter_bow", "Hunter Bow", 16f, 1.25f, 4f, 3f, 3 );
	public static ItemDef warBow = CreateBow( "war_bow", "War Bow", 25f, 0.9f, 7f, 5f, 3 );

	public static ItemDef ironRing = CreateAccessory( "iron_ring", "Iron Ring", EquipmentSlot.Ring1, "ring", "Endurance", 1f, 1 );
	public static ItemDef floralRing = CreateAccessory( "floral_ring", "Floral Ring", EquipmentSlot.Ring1, "ring", "Wisdom", 1f, 3 );
	public static ItemDef weddingRing = CreateAccessory( "wedding_ring", "Wedding Ring", EquipmentSlot.Ring1, "ring", "Will", 1f, 3 );
	public static ItemDef opalRing = CreateAccessory( "opal_ring", "Opal Ring", EquipmentSlot.Ring1, "ring", "Acuity", 2f, 3 );
	public static ItemDef rubyRing = CreateAccessory( "ruby_ring", "Ruby Ring", EquipmentSlot.Ring1, "ring", "Might", 2f, 3 );
	public static ItemDef sapphireRing = CreateAccessory( "sapphire_ring", "Sapphire Ring", EquipmentSlot.Ring1, "ring", "Will", 2f, 3 );
	public static ItemDef emeraldRing = CreateAccessory( "emerald_ring", "Emerald Ring", EquipmentSlot.Ring1, "ring", "Endurance", 2f, 3 );
	public static ItemDef diamondRing = CreateAccessory( "diamond_ring", "Diamond Ring", EquipmentSlot.Ring1, "ring", "Acuity", 3f, 3 );

	public static ItemDef crystalAmulet = CreateAccessory( "crystal_amulet", "Crystal Amulet", EquipmentSlot.Amulet, "amulet", "Wisdom", 2f, 3 );
	public static ItemDef curiousAmulet = CreateAccessory( "curious_amulet", "Curious Amulet", EquipmentSlot.Amulet, "amulet", "Acuity", 2f, 3 );
	public static ItemDef boneAmulet = CreateAccessory( "bone_amulet", "Bone Amulet", EquipmentSlot.Amulet, "amulet", "Endurance", 2f, 3 );
	public static ItemDef scaleAmulet = CreateAccessory( "scale_amulet", "Scale Amulet", EquipmentSlot.Amulet, "amulet", "Might", 2f, 3 );
	public static ItemDef toothAmulet = CreateAccessory( "tooth_amulet", "Tooth Amulet", EquipmentSlot.Amulet, "amulet", "Swiftness", 2f, 3 );
	public static ItemDef soldiersAmulet = CreateAccessory( "soldiers_amulet", "Soldier's Amulet", EquipmentSlot.Amulet, "amulet", "Might", 3f, 3 );
	public static ItemDef churchAmulet = CreateAccessory( "church_amulet", "Church Amulet", EquipmentSlot.Amulet, "amulet", "Will", 3f, 3 );
	public static ItemDef prismsAmulet = CreateAccessory( "prisms_amulet", "Prism Amulet", EquipmentSlot.Amulet, "amulet", "Wisdom", 3f, 3 );

	private static string ItemPrefabPath( string kind, string id ) => $"items/{kind}/{id}.prefab";

	private static ItemDef CreateArmor( string id, string name, string material, float armor, float weight, int minLevel )
	{
		return new ItemDef
		{
			Id = id,
			WorldPrefabPath = ItemPrefabPath( "armor", id ),
			Name = name,
			Description = $"{name} made from {material}.",
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common,
			Stackable = false,
			Tags = new() { "armor", material, "chest", $"tier{minLevel}" },
			Equipment = new EquipmentData
			{
				Slot = EquipmentSlot.Chest,
				Stats = new EquipmentStatBlock { Armor = armor, Weight = weight, Quality = 1f }
			}
		};
	}

	private static ItemDef CreateWeapon( string id, string name, string weaponType, float damage, float speed, float poise, float weight, int minLevel, bool twoHanded = false )
	{
		var tags = new List<string> { "weapon", weaponType, "melee", "metal", $"tier{minLevel}" };
		if ( twoHanded )
			tags.Add( "two-handed" );

		return new ItemDef
		{
			Id = id,
			WorldPrefabPath = ItemPrefabPath( "weapons/melee", id ),
			Name = name,
			Description = $"A {weaponType} weapon.",
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common,
			Stackable = false,
			Tags = tags,
			Equipment = new EquipmentData
			{
				Slot = EquipmentSlot.Melee,
				WeaponClass = WeaponClassFor( weaponType ),
				TwoHanded = twoHanded,
				Stats = new EquipmentStatBlock
				{
					BaseDamage = damage,
					BaseAttackSpeed = speed,
					PoiseDamage = poise,
					Weight = weight,
					Quality = 1f
				},
				WeaponVisual = new WeaponVisualDef { PrefabPath = ItemPrefabPath( "weapons/melee", id ) }
			}
		};
	}

	private static ItemDef CreateBow(
		string id,
		string name,
		float damage,
		float speed,
		float poise,
		float weight,
		int minLevel,
		float drawTime = 0.45f,
		float mightScaling = 1.5f,
		float swiftnessScaling = 1f )
	{
		return new ItemDef
		{
			Id = id,
			WorldPrefabPath = ItemPrefabPath( "weapons/ranged", id ),
			Name = name,
			Description = "A ranged bow.",
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common,
			Stackable = false,
			Tags = new() { "weapon", "bow", "ranged", "wood", "two-handed", $"tier{minLevel}" },
			Equipment = new EquipmentData
			{
				Slot = EquipmentSlot.Ranged,
				WeaponClass = WeaponClass.Bow,
				TwoHanded = true,
				WeaponVisual = new WeaponVisualDef { PrefabPath = ItemPrefabPath( "weapons/ranged", id ) },
				Scaling = new Systems.AttributeScalingDef
				{
					MightToHealthDamage = mightScaling,
					SwiftnessToHealthDamage = swiftnessScaling,
					MightToKnockbackForce = 1f
				},
				RangedWeapon = new RangedWeaponData
				{
					DrawTime = drawTime,
					ProjectilePrefabPath = "models/Weapons/arrow.prefab",
					ProjectileTemplate = new Systems.ProjectileTemplate
					{
						Termination = Systems.ProjectileTerminationType.PierceCount,
						PierceCount = 2,
						Speed = 1500f,
						Lifetime = 5f,
						CollisionBoxSize = new Vector3( 6f, 6f, 6f )
					}
				},
				Stats = new EquipmentStatBlock
				{
					BaseDamage = damage,
					BaseAttackSpeed = speed,
					PoiseDamage = poise,
					Weight = weight,
					Quality = 1f
				}
			}
		};
	}

	private static WeaponClass WeaponClassFor( string weaponType ) => weaponType switch
	{
		"sword" => WeaponClass.Sword,
		"axe" => WeaponClass.Axe,
		"hammer" => WeaponClass.Hammer,
		"mace" => WeaponClass.Mace,
		"spear" => WeaponClass.Spear,
		"polearm" => WeaponClass.Polearm,
		_ => WeaponClass.None
	};

	private static ItemDef CreateAccessory( string id, string name, EquipmentSlot slot, string type, string stat, float value, int minLevel )
	{
		var folder = type == "ring" ? "accessories/rings" : "accessories/amulets";
		return new ItemDef
		{
			Id = id,
			WorldPrefabPath = ItemPrefabPath( folder, id ),
			Name = name,
			Description = $"A {type} that bolsters {stat}.",
			Category = ItemCategory.Equipment,
			Rarity = ItemRarity.Common,
			Stackable = false,
			Tags = new() { "accessory", type, "metal", $"tier{minLevel}" },
			Equipment = new EquipmentData { Slot = slot, Stats = new EquipmentStatBlock { Weight = 0.2f, Quality = 1f } },
			Implicits = new() { new ModData( stat, value, ModifierType.Flat ) }
		};
	}
}
