using System;

namespace Sandbox.Code.Data;

/// <summary>
/// All meta-progression tuning in one place. Placeholder numbers, to be tuned alongside the in-run leveling pass.
/// </summary>
public static class MetaProgressionConfig
{
	public const int StartingSoulLevel = 1;
	public const int MaxSoulLevel = 100;

	/// <summary>Souls required for the first purchase (StartingSoulLevel to StartingSoulLevel + 1).</summary>
	public const float BaseLevelCost = 50f;

	/// <summary>Each further level costs this much more than the last (geometric).</summary>
	public const float CostGrowth = 1.15f;

	/// <summary>Flat attribute value granted per allocated point.</summary>
	public const float ValuePerPoint = 1f;

	/// <summary>Souls needed to buy the level that follows <paramref name="currentLevel"/>.</summary>
	public static float CostForNextLevel( int currentLevel )
	{
		int steps = System.Math.Max( 0, currentLevel - StartingSoulLevel );
		return BaseLevelCost * MathF.Pow( CostGrowth, steps );
	}
}
