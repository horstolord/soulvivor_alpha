using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public sealed class MobSpawnRequest
{
	public SpawnCard Card;
	public Vector3 Position;
	public Rotation Rotation = Rotation.Identity;

	/// <summary>Elite affixes to apply. Null or empty = a normal mob.</summary>
	public List<EliteAffixDef> Affixes;

	/// <summary>Extra size on top of the affixes' own scale (splitter children use less than 1).</summary>
	public float ScaleMultiplier = 1f;
}

/// <summary>
/// The one place that turns a SpawnCard into a live enemy: clone the prefab disabled, apply preset / scale /
/// affixes, then enable. SpawnDirector and (next) the Splitter affix both go through here.
/// </summary>
public static class MobSpawner
{
	public static GameObject Spawn( Scene scene, MobSpawnRequest request )
	{
		var card = request?.Card;
		if ( scene == null || card == null ) return null;

		// Prefabs are resolved by path (static card data can't hold an editor-dragged GameObject).
		if ( !ResourceLibrary.TryGet<PrefabFile>( card.PrefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[MobSpawner] No prefab found at '{card.PrefabPath}' for card '{card.DisplayName}'!" );
			return null;
		}

		// Clone DISABLED so preset, scale and affixes are in place before any OnStart runs.
		var config = new CloneConfig( new Transform( request.Position, request.Rotation ), null, false );
		var go = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
		go.Name = card.DisplayName;

		var affixes = request.Affixes ?? new List<EliteAffixDef>();

		// Clone bigger instead of rescaling later: colliders follow the transform scale. The NavMeshAgent doesn't,
		// so Enemy.OnStart multiplies its Radius/Height by SpawnScale.
		float scale = request.ScaleMultiplier * EliteAffixLibrary.TotalScale( affixes );
		if ( MathF.Abs( scale - 1f ) > 0.001f )
			go.WorldScale = go.WorldScale * scale;

		var enemy = go.Components.Get<Enemy>( FindMode.EverythingInSelfAndDescendants );
		if ( enemy == null )
		{
			Log.Warning( $"[MobSpawner] Could not find an Enemy component on the cloned prefab for {card.DisplayName}!" );
		}
		else
		{
			enemy.PresetOverride = card.MobPresetId;
			enemy.SourceCard = card;
			enemy.SpawnScale = scale;
			enemy.Rank = card.Rank;

			if ( affixes.Count > 0 )
			{
				// On the Enemy's own GameObject (it may sit on a child of the prefab root).
				var elite = enemy.GameObject.Components.Create<EliteControl>();
				elite.SetAffixes( affixes );

				if ( enemy.Rank != MobRank.Boss )
					enemy.Rank = MobRank.Elite;
			}
		}

		go.Enabled = true;
		return go;
	}
}
