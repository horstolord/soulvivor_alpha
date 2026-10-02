using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public static class LootGenerator
{
	// ===== TUNING — placeholders, mine to adjust, do not treat as final balance =====
	private const float BaseDropChance = 0.15f;        // TODO tune
	private const float MobValueToDropChance = 0.01f;  // TODO tune — added per point of BaseSoulValue
	private const float BonusRollMobValueThreshold = 15f; // TODO tune — mobs above this get 2 rarity rolls, keep better

	private static readonly (ItemRarity Rarity, float Weight)[] RarityWeights =
	{
		(ItemRarity.Common, 100f), (ItemRarity.Magic, 45f), (ItemRarity.Rare, 18f),
		(ItemRarity.Epic, 6f), (ItemRarity.Legendary, 1.5f), (ItemRarity.Mythical, 0.3f), (ItemRarity.Divine, 0.05f)
	};

	/// <summary>Single gated roll for enemy kills — may return null if the drop-chance roll fails.</summary>
	public static ItemInstance RollDrop( int sourceLevel, float mobValue )
	{
		float chance = MathF.Min( 0.95f, BaseDropChance + mobValue * MobValueToDropChance );
		if ( Random.Shared.NextSingle() > chance ) return null;
		return BuildInstance( sourceLevel, mobValue );
	}

	/// <summary>Guaranteed multi-roll for chests/altars — no drop-chance gate.</summary>
	public static List<ItemInstance> RollMultiple( int sourceLevel, float mobValue, int count )
	{
		var results = new List<ItemInstance>();
		for ( int i = 0; i < count; i++ )
		{
			var instance = BuildInstance( sourceLevel, mobValue );
			if ( instance != null ) results.Add( instance );
		}
		return results;
	}

	private static ItemInstance BuildInstance( int sourceLevel, float mobValue )
	{
		var baseItem = PickBaseItem( sourceLevel );
		if ( baseItem == null ) return null;

		var rarity = RollRarity( mobValue );
		var affixes = RollAffixes( baseItem, rarity, sourceLevel );
		int charges = baseItem.Consumable?.Charges ?? 0;

		return new ItemInstance
		{
			Definition = baseItem,
			StackCount = 1,
			RemainingCharges = charges,
			MaxCharges = charges,
			RolledMods = affixes
		};
	}

	private static ItemDef PickBaseItem( int sourceLevel )
	{
		var eligible = LootDropRegistry.All.Where( e => e.MinLevel <= sourceLevel ).ToList();
		if ( eligible.Count == 0 ) return null;

		float totalWeight = eligible.Sum( e => e.Weight );
		float roll = Random.Shared.NextSingle() * totalWeight;
		float acc = 0f;
		foreach ( var entry in eligible )
		{
			acc += entry.Weight;
			if ( roll <= acc ) return LootDropRegistry.Resolve( entry.ItemId );
		}
		return LootDropRegistry.Resolve( eligible[^1].ItemId );
	}

	private static ItemRarity RollRarity( float mobValue )
	{
		var first = WeightedRarityRoll();
		if ( mobValue < BonusRollMobValueThreshold ) return first;

		var second = WeightedRarityRoll();
		return second > first ? second : first; // enum order is ascending rarity, so max() picks the better roll
	}

	private static ItemRarity WeightedRarityRoll()
	{
		float totalWeight = RarityWeights.Sum( r => r.Weight );
		float roll = Random.Shared.NextSingle() * totalWeight;
		float acc = 0f;
		foreach ( var (rarity, weight) in RarityWeights )
		{
			acc += weight;
			if ( roll <= acc ) return rarity;
		}
		return ItemRarity.Common;
	}

	private static List<ModData> RollAffixes( ItemDef baseItem, ItemRarity rarity, int sourceLevel )
	{
		int affixCount = (int)rarity; // Common=0 ... Divine=6, matches the enum's declared order
		var itemTags = baseItem.Tags?.ToHashSet() ?? new HashSet<string>();

		var eligible = AffixPool.All
			.Where( a => a.MinLevel <= sourceLevel )
			.Where( a => a.RequiredTags.Count == 0 || a.RequiredTags.Overlaps( itemTags ) )
			.ToList();

		var rolled = new List<ModData>();
		var usedGroups = new HashSet<string>();

		for ( int i = 0; i < affixCount && eligible.Count > 0; i++ )
		{
			var pool = eligible.Where( a => !usedGroups.Contains( a.GroupId ) ).ToList();
			if ( pool.Count == 0 ) break;

			float totalWeight = pool.Sum( a => a.Weight );
			float roll = Random.Shared.NextSingle() * totalWeight;
			float acc = 0f;
			AffixDef picked = pool[^1];
			foreach ( var affix in pool )
			{
				acc += affix.Weight;
				if ( roll <= acc ) { picked = affix; break; }
			}

			usedGroups.Add( picked.GroupId );
			float value = Random.Shared.NextSingle() * (picked.MaxValue - picked.MinValue) + picked.MinValue;
			rolled.Add( new ModData( picked.StatName, value, picked.Type ) );
		}

		return rolled;
	}
}
