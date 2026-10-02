namespace Sandbox.Code.Data;

public class MobData
{
	public string Name { get; set; }

	// ============ BASE ATTRIBUTES ============
	public float Might      { get; set; } = 10f;
	public float Swiftness  { get; set; } = 10f;
	public float Endurance  { get; set; } = 10f;
	public float Will       { get; set; } = 10f;
	public float Acuity     { get; set; } = 10f;
	public float Wisdom     { get; set; } = 10f;

	// ============ SOUL & LEVELING ============
	/// <summary>How many souls this actor's orb is worth when they die.</summary>
	public float BaseSoulValue   { get; set; } = 10f;
	/// <summary>Souls required to reach level 2. Each subsequent level costs 40% more.</summary>
	public float SoulsToLevel    { get; set; } = 50f;

	// Flat attribute growth applied to the StatSheet each time this actor levels up
	public float MightPerLevel      { get; set; } = 1.0f;
	public float SwiftnessPerLevel  { get; set; } = 1.0f;
	public float EndurancePerLevel  { get; set; } = 1.0f;
	public float WillPerLevel       { get; set; } = 0.5f;
	public float AcuityPerLevel     { get; set; } = 0.5f;
	public float WisdomPerLevel     { get; set; } = 0.5f;
	
	public float Armor { get; set; } = 1f;
	public float ArmorPerLevel { get; set; } = 1f;
	public float Poise {get; set; } = 10.0f;

	/// <summary>Base critical hit chance in percent (e.g. 5 = 5%).</summary>
	public float CritChance { get; set; } = 5f;
	public float CritChancePerLevel { get; set; } = 0.25f;

	public MobData Clone()
	{
		return new MobData
		{
			Name             = Name,
			Might            = Might,
			Swiftness        = Swiftness,
			Endurance        = Endurance,
			Will             = Will,
			Acuity           = Acuity,
			Wisdom           = Wisdom,
			BaseSoulValue    = BaseSoulValue,
			SoulsToLevel     = SoulsToLevel,
			MightPerLevel     = MightPerLevel,
			SwiftnessPerLevel = SwiftnessPerLevel,
			EndurancePerLevel = EndurancePerLevel,
			WillPerLevel      = WillPerLevel,
			AcuityPerLevel    = AcuityPerLevel,
			WisdomPerLevel    = WisdomPerLevel,
			Armor = Armor,
			ArmorPerLevel =  ArmorPerLevel,
			Poise          = Poise,
			CritChance = CritChance,
			CritChancePerLevel = CritChancePerLevel,
		};
	}
}


public static class MobRegistry
{
	public static Dictionary<string, MobData> Library { get; private set; } = new();

	public static void Initialize()
	{
		
		Library["player"] = new MobData
		{
			Name          = "Hero",
			Might         = 15f,
			Swiftness     = 12f,
			Endurance     = 14f,
			Wisdom        = 12f,
			Will          = 12f,
			Acuity        = 12f,
			// Hero levels quickly, balanced growth
			BaseSoulValue  = 0f,   // player dropping souls is handled by CurrentSouls
			SoulsToLevel   = 30f,
			MightPerLevel      = 5f,
			SwiftnessPerLevel  = 10f,
			EndurancePerLevel  = 5f,
			WillPerLevel       = 0.8f,
			AcuityPerLevel     = 0.8f,
			WisdomPerLevel     = 0.8f,
			Armor =	10f,
			ArmorPerLevel =  10f,
			Poise          = 30f,
			CritChance = 5f,
			CritChancePerLevel = 0.5f
		};

		Library["goblin"] = new MobData
		{
			Name          = "Goblin Scout",
			Might         = 6f,
			Swiftness     = 66f,
			Endurance     = 8f,
			// Fast and cheap — lots of soul potential in numbers
			BaseSoulValue  = 8f,
			SoulsToLevel   = 60f,
			MightPerLevel      = 0.5f,
			SwiftnessPerLevel  = 4.0f,  // gets terrifyingly fast at high level
			EndurancePerLevel  = 0.8f,
			WillPerLevel       = 0.3f,
			AcuityPerLevel     = 0.3f,
			WisdomPerLevel     = 0.3f,
			Armor =	1f,
			ArmorPerLevel =  1f,
			Poise          = 10f,
			CritChance = 5f,
			CritChancePerLevel = 0.25f
		};

		Library["orc"] = new MobData
		{
			Name          = "Orc Berserker",
			Might         = 22f,
			Swiftness     = 12f,
			Endurance     = 15f,
			// Rare, tanky, drops a big soul orb
			BaseSoulValue  = 20f,
			SoulsToLevel   = 80f,
			MightPerLevel      = 3.0f,  // becomes extremely dangerous late
			SwiftnessPerLevel  = 0.5f,
			EndurancePerLevel  = 2.5f,
			WillPerLevel       = 0.5f,
			AcuityPerLevel     = 0.5f,
			WisdomPerLevel     = 0.5f,
			Armor =	10f,
			ArmorPerLevel =  10f,
			Poise          = 20f,
			CritChance = 5f,
			CritChancePerLevel = 0.25f
		};
	}
}
