using System;
using System.IO;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Permanent, soul-bound proficiencies (1-100). The single entry point for progression is GainXp;
/// event listeners feed it, nothing else in the combat code knows skills exist.
/// </summary>
public sealed class SkillControl : Component
{
	private const string SavePath = "soul_skills.json";

	public event Action<string, int> OnSkillLevelUp;

	private readonly Dictionary<string, SkillData> _data = new();
	private readonly Dictionary<string, List<(Stat Stat, StatModifier Mod)>> _applied = new();
	private Actor _actor;

	protected override void OnStart()
	{
		_actor = Components.GetInAncestorsOrSelf<Actor>();
		Load();
		if ( _actor?.Procs != null )
			foreach ( var def in SkillLibrary.All )
			foreach ( var proc in def.XpSources )
				_actor.Procs.AddProc( proc, source: this );
	}

	protected override void OnDestroy() => Save();

	public int GetLevel( string id ) => SkillLibrary.Get( id ) == null ? 0 : Get( id ).Level;
	public float GetProgress01( string id )
	{
		if ( SkillLibrary.Get( id ) is not { } def ) return 0f;
		var d = Get( id );
		return d.Level >= SkillDef.MaxLevel ? 1f : Math.Clamp( d.Xp / def.XpToNext( d.Level ), 0f, 1f );
	}

	public void GainXp( string id, float amount )
	{
		var def = SkillLibrary.Get( id );
		if ( def == null || amount <= 0f || float.IsNaN( amount ) || float.IsInfinity( amount ) ) return;

		var d = Get( id );
		if ( d.Level >= SkillDef.MaxLevel ) return;

		d.Xp += amount;
		bool leveled = false;
		while ( d.Level < SkillDef.MaxLevel && d.Xp >= def.XpToNext( d.Level ) )
		{
			d.Xp -= def.XpToNext( d.Level );
			d.Level++;
			leveled = true;
			OnSkillLevelUp?.Invoke( id, d.Level );
		}
		if ( d.Level >= SkillDef.MaxLevel ) d.Xp = 0f;

		if ( leveled )
		{
			ApplyEffects( def );
			Save();
		}
	}

	/// <summary>Outgoing damage multiplier (>= 1) for a hit carrying these tags.</summary>
	public float DamageMultiplier( HashSet<AttackTag> tags )
	{
		if ( tags == null || tags.Count == 0 ) return 1f;
		float mult = 1f;
		foreach ( var def in SkillLibrary.All )
		{
			if ( def.DamagePercentPerLevel <= 0f || !def.DamageTags.Overlaps( tags ) ) continue;
			mult *= 1f + def.DamagePercentPerLevel * Get( def.Id ).Level / 100f;
		}
		return mult;
	}

	/// <summary>Call once StatSheet is initialised (it replaces Stat instances, dropping old modifiers).</summary>
	public void ApplyAll()
	{
		foreach ( var def in SkillLibrary.All )
			ApplyEffects( def );
	}

	private void ApplyEffects( SkillDef def )
	{
		var sheet = _actor?.StatSheet;
		if ( sheet == null ) return;

		if ( _applied.TryGetValue( def.Id, out var old ) )
			foreach ( var (stat, mod) in old ) stat.RemoveModifier( mod );

		var list = new List<(Stat, StatModifier)>();
		int level = Get( def.Id ).Level;
		foreach ( var fx in def.StatEffects )
		{
			var stat = sheet.GetStat( fx.StatName );
			if ( stat == null ) continue;
			var mod = new StatModifier( fx.PerLevel * level, fx.Type, source: def );
			stat.AddModifier( mod );
			list.Add( (stat, mod) );
		}
		_applied[def.Id] = list;
	}

	private SkillData Get( string id )
	{
		if ( !_data.TryGetValue( id, out var d ) )
			_data[id] = d = new SkillData { SkillId = id };
		return d;
	}

	private void Save() => FileSystem.Data.WriteJson( SavePath, _data.Values.ToList() );

	private void Load()
	{
		if ( !FileSystem.Data.FileExists( SavePath ) ) return;
		foreach ( var d in FileSystem.Data.ReadJson<List<SkillData>>( SavePath ) ?? new() )
		{
			if ( d == null || string.IsNullOrEmpty( d.SkillId ) || SkillLibrary.Get( d.SkillId ) == null ) continue;
			d.Level = Math.Clamp( d.Level, 1, SkillDef.MaxLevel );
			if ( float.IsNaN( d.Xp ) || float.IsInfinity( d.Xp ) || d.Xp < 0f ) d.Xp = 0f;
			if ( d.Level >= SkillDef.MaxLevel ) d.Xp = 0f;
			_data[d.SkillId] = d;
		}
	}
}
