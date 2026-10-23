using System;
using System.Linq;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Static service owning the persistent SoulProfile: load/save, soul deposit, level purchase, point allocation.
/// Deliberately not a Component: the profile must outlive the player GameObject (respawn, scene reload, vessel switching later).
/// Every mutation saves immediately; mutations are rare (run end, shrine purchase), so no throttling is needed.
/// UI and appliers listen to <see cref="Changed"/> or poll <see cref="Revision"/> (cheap to include in BuildHash).
/// </summary>
public static class MetaProgression
{
	private const string SaveFile = "soul_profile.json";
	private const int CurrentVersion = 1;

	public static readonly string[] Attributes = { "Might", "Swiftness", "Endurance", "Will", "Acuity", "Wisdom" };

	/// <summary>Raised after any profile mutation (after the save).</summary>
	public static event Action Changed;

	/// <summary>Increments on every mutation, for BuildHash in Razor panels.</summary>
	public static int Revision { get; private set; }

	// Lazy on purpose: static initializers misbehave across hot-reload, so nothing here touches the file system until first use.
	private static SoulProfile _profile;
	public static SoulProfile Profile => _profile ??= Load();

	// ============ DERIVED ============
	public static int SoulLevel => Profile.SoulLevel;
	public static float Souls => Profile.Souls;
	public static bool IsMaxLevel => SoulLevel >= MetaProgressionConfig.MaxSoulLevel;
	public static float NextLevelCost => IsMaxLevel ? 0f : MetaProgressionConfig.CostForNextLevel( SoulLevel );

	public static int AttributePointsSpent => Profile.AttributeAllocations.Values.Sum();

	/// <summary>Talent points equal the soul level (design decision), independent of attribute points.</summary>
	public static int TalentPointsTotal => SoulLevel;
	public static int TalentPointsSpent => Profile.TalentRanks.Values.Sum();
	public static int TalentPointsFree => TalentPointsTotal - TalentPointsSpent;

	public static int GetAllocation( string attribute ) =>
		Profile.AttributeAllocations.TryGetValue( attribute, out var v ) ? v : 0;

	public static int GetTalentRank( string talentId ) =>
		Profile.TalentRanks.TryGetValue( talentId, out var v ) ? v : 0;

	/// <summary>Flat bonus an attribute receives from permanent allocation.</summary>
	public static float GetAttributeBonus( string attribute ) =>
		GetAllocation( attribute ) * MetaProgressionConfig.ValuePerPoint;

	/// <summary>Applies permanent allocations after registry stats have been recreated.</summary>
	public static void ApplyAttributeBonuses( StatSheet sheet )
	{
		if ( sheet == null ) return;
		foreach ( var attribute in Attributes )
		{
			var stat = sheet.GetStat( attribute );
			var bonus = GetAttributeBonus( attribute );
			if ( stat != null && bonus > 0f )
				stat.AddModifier( new StatModifier( bonus, ModifierType.Flat, source: typeof( MetaProgression ) ) );
		}
		sheet.RecalculateDerivedStats();
		sheet.FillCurrentPoolsToMax();
	}

	// ============ MUTATIONS ============

	/// <summary>Banks souls collected during a completed run.</summary>
	public static void DepositSouls( float amount )
	{
		if ( amount <= 0f ) return;
		Profile.Souls += amount;
		Commit();
	}

	public static bool CanLevelUp( string attribute ) =>
		StatModApplier.IsAttributeStat( attribute ) && !IsMaxLevel && Souls >= NextLevelCost;

	/// <summary>Dark Souls style: pay the soul cost, gain one soul level, and that level goes into the chosen attribute.</summary>
	public static bool TryLevelUp( string attribute )
	{
		if ( !CanLevelUp( attribute ) ) return false;

		Profile.Souls -= NextLevelCost;
		Profile.SoulLevel++;
		Profile.AttributeAllocations[attribute] = GetAllocation( attribute ) + 1;
		var playerStat = Player.Local?.StatSheet?.GetStat( attribute );
		playerStat?.AddModifier( new StatModifier( MetaProgressionConfig.ValuePerPoint, ModifierType.Flat, source: typeof( MetaProgression ) ) );
		Player.Local?.StatSheet?.RecalculateDerivedStats();
		Commit();
		return true;
	}

	/// <summary>
	/// Talent shape only for v0.1: a rank per id, bounded by free points and the caller-supplied max rank.
	/// A TalentDef registry will own maxRank and prerequisites later.
	/// </summary>
	public static bool TrySpendTalentPoint( string talentId, int maxRank = 1 )
	{
		if ( string.IsNullOrEmpty( talentId ) ) return false;
		if ( TalentPointsFree <= 0 ) return false;
		int rank = GetTalentRank( talentId );
		if ( rank >= maxRank ) return false;

		Profile.TalentRanks[talentId] = rank + 1;
		Commit();
		return true;
	}

	/// <summary>Dev/test only: wipes the profile and deletes the save.</summary>
	public static void ResetProfile()
	{
		_profile = new SoulProfile { Version = CurrentVersion };
		Commit();
	}

	// ============ PERSISTENCE ============

	private static void Commit()
	{
		Revision++;
		Save();
		Changed?.Invoke();
	}

	private static void Save()
	{
		try
		{
			FileSystem.Data.WriteJson( SaveFile, _profile );
		}
		catch ( Exception e )
		{
			Log.Error( $"[MetaProgression] Save failed: {e.Message}" );
		}
	}

	private static SoulProfile Load()
	{
		SoulProfile profile;
		try
		{
			profile = FileSystem.Data.ReadJsonOrDefault<SoulProfile>( SaveFile, null );
		}
		catch ( Exception e )
		{
			Log.Error( $"[MetaProgression] Load failed, starting fresh: {e.Message}" );
			profile = null;
		}

		profile ??= new SoulProfile { Version = CurrentVersion };
		Sanitize( profile );
		return profile;
	}

	/// <summary>Defends against hand-edited or older saves; also the future migration hook (switch on Version).</summary>
	private static void Sanitize( SoulProfile p )
	{
		p.AttributeAllocations ??= new();
		p.TalentRanks ??= new();

		foreach ( var key in p.AttributeAllocations.Keys.ToList() )
		{
			if ( !StatModApplier.IsAttributeStat( key ) || p.AttributeAllocations[key] <= 0 )
				p.AttributeAllocations.Remove( key );
		}

		// SoulLevel must at least cover what was allocated.
		int minLevel = MetaProgressionConfig.StartingSoulLevel + p.AttributeAllocations.Values.Sum();
		p.SoulLevel = Math.Clamp( Math.Max( p.SoulLevel, minLevel ), MetaProgressionConfig.StartingSoulLevel, MetaProgressionConfig.MaxSoulLevel );
		p.Souls = MathF.Max( 0f, p.Souls );
		p.Version = CurrentVersion;
	}
}
