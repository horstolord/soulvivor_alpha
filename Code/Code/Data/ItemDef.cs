using System;
using System.Collections.Generic;
using System.Linq;

namespace Sandbox.Code.Data;

public sealed class ItemDef
{
	public string Id { get; init; }
	public string Name { get; init; }
	public string Description { get; init; }
	public ItemCategory Category { get; init; }
	public ItemRarity Rarity { get; init; }
	public bool Stackable { get; init; }
	public int MaxStack { get; init; } = 1;
	public List<string> Tags { get; init; } = new();
	public List<ModData> Mods { get; init; } = new();
	public EquipmentData Equipment { get; init; }
	public ConsumableData Consumable { get; init; }
	public CraftingData Crafting { get; init; }
	public bool IsEquippable => Equipment != null;
	public bool IsConsumable => Consumable != null;
	public bool IsCraftingMaterial => Crafting != null;
}
public sealed class EquipmentData
{
	public EquipmentSlot Slot { get; init; }
	public EquipmentStatBlock Stats { get; init; } = new();
	public WeaponVisualDef WeaponVisual { get; init; } = new();
}
public sealed class WeaponVisualDef
{
	// Path relative to Assets, no "assets/" prefix. Empty = pose only, no model.
	public string PrefabPath { get; init; }
	public string HoldType { get; init; } = "melee_weapons";
	public string HoldBone { get; init; } = "hold_r";
	public Vector3 LocalPosition { get; init; } = Vector3.Zero;
	public Angles LocalRotation { get; init; } = Angles.Zero;
	public float Scale { get; init; } = 1f;
}
public sealed class EquipmentStatBlock
{
	public float BaseDamage { get; init; }
	public float BaseAttackSpeed { get; init; }
	public float GuardValue { get; init; }
	public float PoiseDamage { get; init; }
	
	public float Armor {get; init; }
	
	//public List<MaterialData> Material { get; init; }
	public float Quality { get; init; }
	public float Weight { get; init; }
	
}
public sealed class ConsumableData
{
	public int Charges { get; init; } = 1;
	public string UseEffectId { get; init; }
	public float RestoreHealth { get; init; }
	public float RestoreEnergy { get; init; }
	public float RestoreStamina { get; init; }
	public Systems.BuffDef BuffEffect { get; init; }
}
public sealed class CraftingData
{
	public int MaterialValue { get; init; } = 1;
	public List<string> MaterialTags { get; init; } = new();
}
public sealed class ItemInstance
{
	public string InstanceId { get; init; } = Guid.NewGuid().ToString();
	public ItemDef Definition { get; init; }
	public int StackCount { get; set; } = 1;
	public int RemainingCharges { get; set; }
	public int MaxCharges { get; set; }
	public List<ModData> RolledMods { get; init; } = new();

	/// <summary>Wraps a hand-authored ItemDef with no procedural rolls — RolledMods mirrors Definition.Mods.</summary>
	public static ItemInstance FromDefinition( ItemDef def, int stackCount = 1 )
	{
		int charges = def?.Consumable?.Charges ?? 0;
		return new ItemInstance
		{
			Definition = def,
			StackCount = stackCount,
			RemainingCharges = charges,
			MaxCharges = charges,
			RolledMods = new List<ModData>( def?.Mods ?? Enumerable.Empty<ModData>() )
		};
	}
}

