using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component: H spawns a mob in front of the player with the affixes listed in AffixIds
/// (comma separated ids from EliteAffixLibrary: volatile, massive, infused_fire, infused_frost, infused_air,
/// infused_earth). Leave AffixIds empty to use the real roll. Safe to delete once the director spawns elites.
/// </summary>
public sealed class EliteTestComponent : Component
{
	/// <summary>Which SpawnCard to use, matched by PrefabPath (see SpawnCardRegistry).</summary>
	[Property] public string PrefabPath { get; set; } = "goblin.prefab";
	[Property] public string AffixIds { get; set; } = "volatile";
	[Property] public float Distance { get; set; } = 500f;

	protected override void OnUpdate()
	{
		if ( !Input.Keyboard.Pressed( "H" ) ) return;

		var player = Player.Local;
		if ( player == null ) return;

		var card = SpawnCardRegistry.All.FirstOrDefault( c => c.PrefabPath == PrefabPath );
		if ( card == null )
		{
			Log.Warning( $"[EliteTest] No SpawnCard with PrefabPath '{PrefabPath}'." );
			return;
		}

		var position = player.GameObject.WorldPosition + player.GameObject.WorldRotation.Forward * Distance;
		MobSpawner.Spawn( Scene, new MobSpawnRequest { Card = card, Position = position, Affixes = ParseAffixes() } );
	}

	private List<EliteAffixDef> ParseAffixes()
	{
		if ( string.IsNullOrWhiteSpace( AffixIds ) )
			return EliteAffixLibrary.RollAffixes();

		var result = new List<EliteAffixDef>();
		foreach ( var id in AffixIds.Split( ',', System.StringSplitOptions.RemoveEmptyEntries | System.StringSplitOptions.TrimEntries ) )
		{
			var affix = EliteAffixLibrary.Get( id );
			if ( affix == null )
				Log.Warning( $"[EliteTest] Unknown affix id '{id}'." );
			else
				result.Add( affix );
		}
		return result;
	}
}
