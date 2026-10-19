using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>How one source (mob drop, elite, boss, level-up, shrine...) wants its offer rolled.</summary>
public sealed class BoonOfferRequest
{
	public int ChoiceCount = 3;

	/// <summary>Rarity is rolled this many times per choice and the best result is kept (elites/bosses use 2+).</summary>
	public int RarityRolls = 1;

	public ItemRarity MinRarity = ItemRarity.Common;
	public string SourceLabel;
}

/// <summary>
/// Builds pick-1-of-N boon offers. Mirrors LootGenerator: weighted rarity roll with optional bonus rolls,
/// then a weighted pick inside that rarity. Also holds the per-source presets (drop chance, rarity bias).
/// </summary>
public static class BoonGenerator
{
	// ===== TUNING — placeholders for the designer to adjust =====
	private static readonly (ItemRarity Rarity, float Weight)[] RarityWeights =
	{
		(ItemRarity.Common, 100f), (ItemRarity.Magic, 45f), (ItemRarity.Rare, 18f),
		(ItemRarity.Epic, 6f), (ItemRarity.Legendary, 1.5f), (ItemRarity.Mythical, 0.3f), (ItemRarity.Divine, 0.05f)
	};

	/// <summary>Percent chance an elite drops a boon pickup.</summary>
	private const float EliteDropChance = 20f;

	/// <summary>Percent chance a boss drops a boon pickup (always).</summary>
	private const float BossDropChance = 100f;

	// ============ SOURCE PRESETS ============

	/// <summary>Percent chance a kill drops a boon pickup. Normal mobs use their MobData value.</summary>
	public static float DropChanceFor( MobRank rank, float normalChance ) => rank switch
	{
		MobRank.Boss => BossDropChance,
		MobRank.Elite => EliteDropChance,
		_ => normalChance
	};

	/// <summary>How a dropped pickup rolls its offer once collected.</summary>
	public static BoonOfferRequest RequestForDrop( MobRank rank ) => rank switch
	{
		MobRank.Boss => new BoonOfferRequest { RarityRolls = 3, MinRarity = ItemRarity.Rare, SourceLabel = "Boss Slain" },
		MobRank.Elite => new BoonOfferRequest { RarityRolls = 2, SourceLabel = "Elite Slain" },
		_ => new BoonOfferRequest { SourceLabel = "Boon Found" }
	};

	public static BoonOfferRequest RequestForLevelUp() => new BoonOfferRequest { SourceLabel = "Level Up" };

	// ============ OFFER BUILDING ============

	/// <summary>
	/// Returns an offer of up to ChoiceCount distinct boons, or null if nothing is eligible.
	/// Pass the owner so boons already at MaxStacks are skipped.
	/// </summary>
	public static BoonOffer BuildOffer( BoonOfferRequest request, BoonControl owner = null )
	{
		request ??= new BoonOfferRequest();

		var eligible = BoonLibrary.All.Where( b => owner == null || owner.CanAdd( b ) ).ToList();
		if ( eligible.Count == 0 ) return null;

		var offer = new BoonOffer { SourceLabel = request.SourceLabel };
		int count = Math.Max( 1, request.ChoiceCount );

		for ( int i = 0; i < count && eligible.Count > 0; i++ )
		{
			var pick = PickForRarity( eligible, RollRarity( request ) );
			if ( pick == null ) break;

			offer.Choices.Add( pick );
			eligible.Remove( pick ); // no duplicates within one offer
		}

		return offer.Choices.Count > 0 ? offer : null;
	}

	private static ItemRarity RollRarity( BoonOfferRequest request )
	{
		var best = WeightedRarityRoll();
		for ( int i = 1; i < request.RarityRolls; i++ )
		{
			var next = WeightedRarityRoll();
			if ( next > best ) best = next; // enum order is ascending rarity
		}
		return best < request.MinRarity ? request.MinRarity : best;
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

	/// <summary>
	/// Picks a boon of the rolled rarity. While the library is small, a rarity may have no boons:
	/// fall back to the nearest populated rarity (lower first, then higher).
	/// </summary>
	private static BoonDef PickForRarity( List<BoonDef> eligible, ItemRarity rarity )
	{
		for ( int distance = 0; distance <= 6; distance++ )
		{
			var pool = OfRarity( eligible, (int)rarity - distance );
			if ( pool.Count == 0 && distance > 0 )
				pool = OfRarity( eligible, (int)rarity + distance );

			if ( pool.Count > 0 )
				return WeightedPick( pool );
		}
		return null;
	}

	private static List<BoonDef> OfRarity( List<BoonDef> eligible, int rarity )
	{
		return eligible.Where( b => (int)b.Rarity == rarity ).ToList();
	}

	private static BoonDef WeightedPick( List<BoonDef> pool )
	{
		float totalWeight = pool.Sum( b => MathF.Max( 0f, b.Weight ) );
		if ( totalWeight <= 0f ) return pool[Random.Shared.Next( pool.Count )];

		float roll = Random.Shared.NextSingle() * totalWeight;
		float acc = 0f;
		foreach ( var boon in pool )
		{
			acc += MathF.Max( 0f, boon.Weight );
			if ( roll <= acc ) return boon;
		}
		return pool[^1];
	}
}
