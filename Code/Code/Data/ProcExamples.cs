using Sandbox.Code.Systems;
using Sandbox.Code.World;

namespace Sandbox.Code.Data;

/// <summary>
/// Example procs for testing and as templates for boons / elite affixes / item procs.
/// Expression-bodied properties on purpose: every access builds a fresh instance (no shared mutable state,
/// no static-initializer hotreload staleness), same pattern as BuffExamples.
/// </summary>
public static class ProcExamples
{
	/// <summary>Leech: heal for 10% of damage dealt.</summary>
	public static ProcDef Vampiric => new ProcDef
	{
		Id = "vampiric",
		DisplayName = "Vampiric",
		Trigger = ProcTrigger.HitDealt,
		Chance = 100f,
		Effects = new List<IProcEffect> { new RestoreProcEffect { HealthPctOfEventAmount = 10f } }
	};

	/// <summary>On death, detonate a fire nova where the owner stands. Hurts everything nearby, mobs included.</summary>
	public static ProcDef Volatile => new ProcDef
	{
		Id = "volatile",
		DisplayName = "Volatile",
		Trigger = ProcTrigger.Death,
		Chance = 100f,
		Effects = new List<IProcEffect>
		{
			new CastRunesProcEffect { Runes = RuneLibrary.GetPreset( "fire_nova" ), AimAtOther = false }
		}
	};

	/// <summary>On kill: 12s damage boost (refreshes, doesn't stack).</summary>
	public static ProcDef Bloodlust => new ProcDef
	{
		Id = "bloodlust",
		DisplayName = "Bloodlust",
		Trigger = ProcTrigger.Kill,
		Chance = 100f,
		Effects = new List<IProcEffect> { new BuffProcEffect { Buff = BuffExamples.DamageBoost } }
	};

	/// <summary>
	/// Depth test: 30% on hit to cast a frost nova, which can itself re-proc up to 2 levels deep with the
	/// chance falling off by a square root per level. The ICD keeps the cascade bounded.
	/// </summary>
	public static ProcDef Cascade => new ProcDef
	{
		Id = "cascade",
		DisplayName = "Cascade",
		Trigger = ProcTrigger.HitDealt,
		Chance = 30f,
		InternalCooldown = 0.5f,
		MaxProcDepth = 2,
		ChainFalloffExponent = 0.5f,
		Effects = new List<IProcEffect>
		{
			new CastRunesProcEffect { Runes = RuneLibrary.GetPreset( "frost_nova" ), FromEventPosition = true, AimAtOther = false }
		}
	};

	public static ProcDef Get( string id ) => id?.ToLower() switch
	{
		"vampiric" => Vampiric,
		"volatile" => Volatile,
		"bloodlust" => Bloodlust,
		"cascade" => Cascade,
		_ => null
	};
}
