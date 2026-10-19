using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Systems;

namespace Sandbox.Code.World;

/// <summary>
/// A dropped boon. Homes toward the local player and, on contact, rolls a boon offer (built at pickup
/// time so it reflects the player's current stacks) and queues it on the player's BoonControl.
/// Only the player can collect it. Shrines later can reuse BoonControl.QueueOffer the same way.
/// </summary>
public sealed class BoonPickup : Component
{
	[Property] public float CollectRadius { get; set; } = 60f;
	[Property] public float HomingRadius { get; set; } = 600f;
	[Property] public float HomingSpeed { get; set; } = 320f;

	/// <summary>Visual prefab, loaded by path like SoulOrb's sprite (no "assets/" prefix).</summary>
	[Property] public string PrefabPath { get; set; } = "boon.prefab";

	/// <summary>Seconds before it despawns. 0 or less = never (boons are rare, so they stay by default).</summary>
	[Property] public float Lifetime { get; set; } = 0f;

	/// <summary>How the offer is rolled (set by whoever spawns this).</summary>
	public BoonOfferRequest Request { get; set; }

	private float _age;
	private bool _hasVisual;
	private bool _collected;

	public static BoonPickup Spawn( Scene scene, Vector3 position, BoonOfferRequest request )
	{
		var go = scene.CreateObject();
		go.WorldPosition = position;
		go.Name = "BoonPickup";

		var pickup = go.Components.Create<BoonPickup>();
		pickup.Request = request;
		return pickup;
	}

	protected override void OnStart()
	{
		if ( ResourceLibrary.TryGet<PrefabFile>( PrefabPath, out var prefabFile ) )
		{
			var visual = SceneUtility.GetPrefabScene( prefabFile ).Clone();
			visual.Parent = GameObject;
			visual.LocalPosition = Vector3.Zero;
			_hasVisual = true;
		}
		else
		{
			Log.Warning( $"[BoonPickup] Could not find '{PrefabPath}' in ResourceLibrary — drawing a placeholder box." );
		}
	}

	protected override void OnUpdate()
	{
		if ( _collected ) return;

		_age += Time.Delta;
		if ( Lifetime > 0f && _age >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		// Placeholder so the pickup is findable before its prefab exists.
		if ( !_hasVisual )
			DebugOverlay.Box( GameObject.WorldPosition, Vector3.One * 24f, Color.Magenta );

		var player = Player.Local;
		if ( !player.IsValid() ) return;
		if ( player.StateComp?.CurrentState == ActorStateType.Dead ) return;

		var toPlayer = player.GameObject.WorldPosition + Vector3.Up * 40f - GameObject.WorldPosition;
		float dist = toPlayer.Length;

		if ( dist <= CollectRadius )
		{
			Collect( player );
			return;
		}

		if ( dist <= HomingRadius )
			GameObject.WorldPosition += toPlayer.Normal * HomingSpeed * Time.Delta;
	}

	private void Collect( Player player )
	{
		_collected = true;

		var boons = player.Boons;
		if ( boons != null )
		{
			var offer = BoonGenerator.BuildOffer( Request, boons );
			if ( offer != null )
			{
				boons.QueueOffer( offer );
				Log.Info( $"[BoonPickup] Collected — queued offer '{offer.SourceLabel}' with {offer.Choices.Count} choices." );
			}
			else
			{
				Log.Info( "[BoonPickup] Collected, but nothing is eligible to offer — discarded." );
			}
		}

		GameObject.Destroy();
	}
}
