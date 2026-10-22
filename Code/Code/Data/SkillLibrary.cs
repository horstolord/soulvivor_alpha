using System.Collections.Generic;
using Sandbox.Code.Systems;
namespace Sandbox.Code.Data;
/// <summary>
/// Authored skills. Per-level numbers are placeholders to tune.
/// </summary>
public static class SkillLibrary
{
	public static List<SkillDef> All { get; } = new();
	static SkillLibrary()
	{
		All.Add( Combat( SkillIds.Melee, "Melee", AttackTag.Melee ) );
		All.Add( Combat( SkillIds.Ranged, "Ranged", AttackTag.Ranged ) );
		All.Add( Element( SkillIds.Fire, "Fire", AttackTag.Fire ) );
		All.Add( Element( SkillIds.Frost, "Frost", AttackTag.Frost ) );
		All.Add( Element( SkillIds.Air, "Air", AttackTag.Air ) );
		All.Add( Element( SkillIds.Earth, "Earth", AttackTag.Earth ) );
		var magic = Combat( SkillIds.Magic, "Magic", AttackTag.Spell );
		magic.XpSources.Clear();
		// Cast events do not carry a resource amount, so casting XP must be flat.
		magic.XpSources.Add( XpOn( SkillIds.Magic, ProcTrigger.Cast, 0f, 5f ) );
		All.Add( magic );
		var defense = new SkillDef { Id = SkillIds.Defense, DisplayName = "Defense" };
		defense.XpSources.Add( XpOn( SkillIds.Defense, ProcTrigger.Blocked, 0.5f, 0f ) );
		defense.XpSources.Add( XpOn( SkillIds.Defense, ProcTrigger.HitTaken, 0.1f, 0f ) );
		All.Add( defense );
		var mobility = new SkillDef { Id = SkillIds.Mobility, DisplayName = "Mobility" };
		mobility.XpSources.Add( XpOn( SkillIds.Mobility, ProcTrigger.Dodge, 0f, 5f ) );
		mobility.XpSources.Add( XpOn( SkillIds.Mobility, ProcTrigger.Evaded, 0f, 5f ) );
		All.Add( mobility );
	}
	public static SkillDef Get( string id )
	{
		if ( string.IsNullOrEmpty( id ) ) return null;
		foreach ( var skill in All )
		{
			if ( skill.Id == id )
				return skill;
		}
		return null;
	}
	private static SkillDef Combat( string id, string displayName, AttackTag tag )
	{
		var def = new SkillDef
		{
			Id = id,
			DisplayName = displayName,
			DamageTags = new HashSet<AttackTag> { tag },
			// Percent points: 0.5 means +0.5% per level, up to +50% at level 100.
			DamagePercentPerLevel = 0.5f
		};
		def.XpSources.Add( XpOnDamage( id, tag ) );
		return def;
	}
	private static SkillDef Element( string id, string displayName, AttackTag tag ) =>
		Combat( id, displayName, tag );
	private static ProcDef XpOnDamage( string skillId, AttackTag tag ) =>
		XpOn( skillId, ProcTrigger.HitDealt, 1f, 0f, tag );
	private static ProcDef XpOn( string skillId, ProcTrigger trigger, float perAmount, float flat ) =>
		XpOn( skillId, trigger, perAmount, flat, null );
	private static ProcDef XpOn( string skillId, ProcTrigger trigger, float perAmount, float flat, AttackTag? tag )
	{
		var def = new ProcDef
		{
			Id = "skill_xp_" + skillId + "_" + trigger,
			DisplayName = skillId + " XP",
			Trigger = trigger,
			Effects = new List<IProcEffect>
			{
				new CastRunesProcEffect.SkillXpProcEffect { SkillId = skillId, PerAmount = perAmount, Flat = flat }
			}
		};
		if ( tag != null )
			def.Condition = new ProcCondition { RequiredTags = new HashSet<AttackTag> { tag.Value } };
		return def;
	}
}
