using System;
using System.Linq;
using Sandbox.Code.Actors;

namespace Sandbox.Code.World;

/// <summary>
/// A soul orb dropped when an actor dies.
/// Slowly homes toward the nearest actor in range, then gets absorbed on contact.
/// Any actor — player or enemy — can collect it.
/// </summary>
public class SoulOrb : Component
{
	// ============ CONFIG ============
	/// <summary>How many souls this orb grants when collected.</summary>
	public float SoulValue     { get; set; } = 10f;

	[Property] public float CollectRadius  { get; set; } = 55f;   // absorb distance
	[Property] public float HomingRadius   { get; set; } = 350f;  // start homing distance
	[Property] public float HomingSpeed    { get; set; } = 200f;  // units per second
	[Property] public float Lifetime       { get; set; } = 25f;   // seconds before despawn
	// Prefab loaded from code — no editor assignment needed

	// ============ RUNTIME ============
	private float        _age       = 0f;
	private Actor        _target    = null;

	protected override void OnStart()
	{
		// In s&box, Assets/ is the filesystem root — paths must NOT include "assets/" prefix.
		// ResourceLibrary.Get throws if not found; TryGet returns false silently.
		if ( ResourceLibrary.TryGet<PrefabFile>( "soulsprite.prefab", out var prefabFile ) )
		{
			var spriteInstance = SceneUtility.GetPrefabScene( prefabFile ).Clone();
			spriteInstance.Parent = GameObject;
			spriteInstance.LocalPosition = Vector3.Zero;
		}
		else
		{
			Log.Warning( "[SoulOrb] Could not find soulsprite.prefab in ResourceLibrary!" );
		}
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		_age += Time.Delta;
		if ( _age >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		// Gentle bob so it's visible 
		float bobOffset = MathF.Sin( _age * 6f ) * 120f;
		GameObject.LocalPosition = GameObject.LocalPosition.WithZ(
			GameObject.LocalPosition.z + bobOffset * Time.Delta
		);

		FindOrUpdateTarget();

		if ( _target == null ) return;

		// Home toward target
		Vector3 toTarget = _target.GameObject.WorldPosition - GameObject.WorldPosition;
		float dist = toTarget.Length;

		if ( dist <= CollectRadius )
		{
			// Absorbed!
			_target.Leveling?.GainSouls( SoulValue );
			Log.Info( $"[Souls] {_target.GameObject.Name} absorbed {SoulValue:F0} souls." );
			GameObject.Destroy();
			return;
		}

		// Move toward target
		GameObject.WorldPosition += toTarget.Normal * HomingSpeed * Time.Delta;
	}

	private void FindOrUpdateTarget()
	{
		// If we already have a valid close target, keep it
		if ( _target != null && _target.IsValid()  )
		{
			float dist = (_target.GameObject.WorldPosition - GameObject.WorldPosition).Length;
			if ( dist <= HomingRadius ) return;
			_target = null; // walked out of range
		}

		// Find the nearest actor within HomingRadius
		var myPos = GameObject.WorldPosition;
		_target = Scene.GetAllComponents<Actor>()
			.Where( a => a.IsValid() && a.GameObject != null && a.StateComp?.CurrentState != ActorStateType.Dead && (a.StatSheet == null || a.StatSheet.CurrentHealth > 0f) )
			.OrderBy( a => (a.GameObject.WorldPosition - myPos).LengthSquared )
			.FirstOrDefault( a => (a.GameObject.WorldPosition - myPos).Length <= HomingRadius );
	}
}
