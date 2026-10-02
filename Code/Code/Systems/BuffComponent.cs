using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;
using Sandbox.Code.Data;

public class ActiveBuff
{
	public string Id { get; set; }
	public TimeUntil TimeUntilExpire { get; set; }
	public List<(Stat Stat, StatModifier Modifier)> AppliedModifiers { get; set; } = new();
	public bool ModifiedAttributes { get; set; }
}

public class BuffComponent : Component
{
	private readonly List<ActiveBuff> _active = new();

	public void ApplyBuff( BuffDef def, StatSheet statSheet, bool refreshExisting = false )
	{
		if ( def == null || statSheet == null )
			return;

		// Cantrip buffs refresh their own duration instead of stacking another copy.
		if ( refreshExisting )
		{
			for ( int i = _active.Count - 1; i >= 0; i-- )
			{
				if ( _active[i].Id == def.Id )
					RemoveBuff( _active[i] );
			}
		}

		var buff = new ActiveBuff
		{
			Id = def.Id,
			TimeUntilExpire = def.Duration
		};

		foreach ( var modDef in def.Modifiers )
		{
			var stat = statSheet.GetStat( modDef.StatName );
			if ( stat == null )
			{
				Log.Warning( $"Buff '{def.DisplayName}' tried to modify unknown stat: {modDef.StatName}" );
				continue;
			}

			var modifier = new StatModifier( modDef.Value, modDef.Type, buff );
			stat.AddModifier( modifier );
			buff.AppliedModifiers.Add( (stat, modifier) );
		}

		// If this buff modified attributes, recalculate derived stats
		if ( def.Modifiers.Any( m => IsAttributeStat( m.StatName ) ) )
		{
			buff.ModifiedAttributes = true;
			statSheet.RecalculateDerivedStats();
		}

		_active.Add( buff );
		Log.Info( $"Buff '{def.DisplayName}' applied. Duration: {def.Duration}s" );
	}

	protected override void OnUpdate()
	{
		for ( int i = _active.Count - 1; i >= 0; i-- )
		{
			if ( _active[i].TimeUntilExpire )
			{
				RemoveBuff( _active[i] );
			}
		}
	}

	private void RemoveBuff( ActiveBuff buff )
	{
		var statSheet = Components.GetInAncestorsOrSelf<StatSheet>() ?? GameObject.GetComponent<StatSheet>();
		
		foreach ( var (stat, _) in buff.AppliedModifiers )
		{
			stat.RemoveModifiersFromSource( buff );
		}

		// If this buff modified attributes, recalculate derived stats
		if ( buff.ModifiedAttributes && statSheet != null )
		{
			statSheet.RecalculateDerivedStats();
		}

		_active.Remove( buff );
		Log.Info( $"Buff '{buff.Id}' removed." );
	}

	private bool IsAttributeStat( string statName )
	{
		return statName is "Might" or "Swiftness" or "Endurance" or "Will" or "Acuity" or "Wisdom";
	}
}

