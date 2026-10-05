using System;
using Sandbox;

namespace Sandbox.Code.Systems;

/// <summary>
/// Grows a spawned one-shot effect (nova ring, cone, pulse) from a small scale to its target
/// scale, then destroys it. Added automatically by SpellVfx.Spawn if the prefab lacks one.
/// </summary>
public sealed class SpellBurstVfx : Component
{
	[Property] public float Duration { get; set; } = 0.6f;
	[Property] public float GrowTime { get; set; } = 0.2f;
	[Property] public float StartScale { get; set; } = 0.05f;

	public Vector3 TargetScale { get; set; } = Vector3.One;
	private float _startTime;

	protected override void OnStart()
	{
		_startTime = Time.Now;
		GameObject.LocalScale = TargetScale * StartScale;
	}

	protected override void OnUpdate()
	{
		float age = Time.Now - _startTime;
		float t = GrowTime > 0f ? Math.Clamp( age / GrowTime, 0f, 1f ) : 1f;
		GameObject.LocalScale = TargetScale * (StartScale + (1f - StartScale) * t);
		if ( age >= Duration ) GameObject.Destroy();
	}
}

public static class SpellVfx
{
	/// <summary>Shape prefabs are authored at this size (radius / length in units) and scaled from it.</summary>
	public const float RefSize = 100f;

	/// <summary>
	/// Spawns a spell prefab. scale is multiplied onto the prefab's authored scale.
	/// autoBurst: grow-then-destroy for one-shot effects; pass false for beams/persistent fx (the prefab
	/// or the caller owns their lifetime). lifetime only applies when autoBurst is true.
	/// Returns null (silently for a blank path, with a warning for a missing prefab).
	/// </summary>
	public static GameObject Spawn( string prefabPath, Vector3 position, Rotation rotation, Vector3 scale,
		Material material = null, GameObject parent = null, bool autoBurst = true, float? lifetime = null )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) ) return null;
		if ( !ResourceLibrary.TryGet<PrefabFile>( prefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[SpellVfx] Could not find '{prefabPath}' in ResourceLibrary!" );
			return null;
		}

		var go = SceneUtility.GetPrefabScene( prefabFile ).Clone( new CloneConfig
		{
			Transform = new Transform( position, rotation ),
			Parent = parent,
			StartEnabled = false
		} );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		go.WorldPosition = position;
		go.WorldRotation = rotation;

		// MaterialOverride, not base assignment — that is what fixed the shader sorting bug.
		//if ( material != null )
		//{
		//	foreach ( var renderer in go.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ) )
		//		renderer.MaterialOverride = material;
		//}

		var authored = go.LocalScale;
		if ( autoBurst )
		{
			var burst = go.Components.Get<SpellBurstVfx>( FindMode.EverythingInSelfAndDescendants )
				?? go.Components.Create<SpellBurstVfx>();
			burst.TargetScale = authored * scale;
			if ( lifetime.HasValue ) burst.Duration = lifetime.Value;
		}
		else
		{
			go.LocalScale = authored * scale;
		}

		go.Enabled = true;
		return go;
	}
}
