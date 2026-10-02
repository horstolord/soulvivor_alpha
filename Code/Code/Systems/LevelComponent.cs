using System;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Manages leveling and soul accumulation for any actor.
/// Attach via Actor.OnStart — do not place manually.
/// </summary>
public class LevelComponent : Component
{
	public const int MaxLevel = 100;

	// ============ STATE ============
	public int   Level         { get; private set; } = 1;
	public float CurrentSouls  { get; private set; } = 0f;
	/// <summary>Souls needed to reach the next level.</summary>
	public float SoulThreshold { get; private set; } = 10f;

	// ============ DATA ============
	private MobData _data;

	/// <summary>Fired whenever a level-up occurs. Passes the new level.</summary>
	public Action<int> OnLevelUp;

	// ============ INIT ============
	/// <summary>Call once from Actor.OnStart after MobRegistry is loaded.</summary>
	public void Initialize( MobData data, int initialLevel = 1 )
	{
		_data         = data;
		Level         = 1;
		CurrentSouls  = 0f;
		SoulThreshold = ThresholdForLevel( 1 );

		if ( initialLevel > 1 )
		{
			SetLevel( initialLevel );
		}
	}

	/// <summary>Sets the level directly, advancing level-by-level and firing OnLevelUp.</summary>
	public void SetLevel( int targetLevel )
	{
		targetLevel = Math.Clamp( targetLevel, 1, MaxLevel );
		while ( Level < targetLevel )
		{
			Level++;
			SoulThreshold = ThresholdForLevel( Level );
			OnLevelUp?.Invoke( Level );
		}
	}


	// ============ SOUL GAIN ============
	/// <summary>
	/// Award souls to this actor. Triggers level-up(s) if threshold is crossed.
	/// </summary>
	public void GainSouls( float amount )
	{
		if ( Level >= MaxLevel ) return;

		CurrentSouls += amount;

		// Handle multi-level bursts (e.g. massive orb pickup)
		while ( CurrentSouls >= SoulThreshold && Level < MaxLevel )
		{
			CurrentSouls  -= SoulThreshold;
			Level++;
			SoulThreshold  = ThresholdForLevel( Level );
			OnLevelUp?.Invoke( Level );
			Log.Info( $"[Souls] Level up → {Level}  (next threshold: {SoulThreshold:F0})" );
		}
	}

	// ============ MATH ============
	/// <summary>
	/// Souls needed to advance past 'level'. Grows by 40% per tier:
	///   L1→2: base,  L2→3: base*1.4,  L3→4: base*1.96 ...
	/// </summary>
	private float ThresholdForLevel( int level )
	{
		return _data.SoulsToLevel * MathF.Pow( 1.4f, level - 1 );
	}

	// ============ UI HELPERS ============
	/// <summary>0..1 progress toward the next level.</summary>
	public float LevelProgress => Level >= MaxLevel ? 1f : CurrentSouls / SoulThreshold;
}
