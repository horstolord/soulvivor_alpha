using System;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;

namespace Sandbox.Code.World;

/// <summary>
/// A world-space item pickup. Unlike SoulOrb, this does NOT auto-absorb on overlap — the player must
/// interact with it (see IInteractable). Stays in place if the inventory is full when interacted with.
/// </summary>
public sealed class LootDrop : Component, IInteractable
{
	public ItemInstance Item { get; set; }

	[Property] public float Lifetime { get; set; } = 45f; // seconds before despawn if never picked up
	[Property] public float BobHeight { get; set; } = 8f;
	[Property] public float BobSpeed { get; set; } = 3f;

	private float _age;
	private float _baseZ;
	private bool _initialized;

	protected override void OnStart()
	{
		_baseZ = GameObject.WorldPosition.z;
		_initialized = true;

		// Try loading item.prefab or fallback to lootitem.prefab
		if ( ResourceLibrary.TryGet<PrefabFile>( "item.prefab", out var prefabFile )
		     || ResourceLibrary.TryGet<PrefabFile>( "lootitem.prefab", out prefabFile ) )
		{
			var visual = SceneUtility.GetPrefabScene( prefabFile ).Clone();
			visual.Parent = GameObject;
			visual.LocalPosition = Vector3.Zero;
		}
		else
		{
			Log.Warning( "[LootDrop] Could not find item.prefab or lootitem.prefab in ResourceLibrary — assign one." );
		}
	}

	protected override void OnUpdate()
	{
		if ( !_initialized )
		{
			_baseZ = GameObject.WorldPosition.z;
			_initialized = true;
		}

		_age += Time.Delta;
		if ( _age >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		float bob = MathF.Sin( _age * BobSpeed ) * BobHeight;
		GameObject.WorldPosition = GameObject.WorldPosition.WithZ( _baseZ + bob );
	}

	public bool CanInteract( GameObject interactor ) => true;

	public void OnInteract( GameObject interactor )
	{
		Item ??= LootGenerator.RollDrop( 1, 10f ) ?? ItemInstance.FromDefinition( ItemData.shortsword );
		if ( Item == null ) return;

		var inventory = Presentation.UI.LocalInventory;
		if ( inventory == null )
		{
			Log.Warning( "[LootDrop] No LocalInventory found — cannot pick up." );
			return;
		}

		bool added = inventory.AddItem( Item );
		if ( added )
		{
			Log.Info( $"[LootDrop] Picked up '{Item.Definition?.Name}'." );
			GameObject.Destroy();
		}
		else
		{
			Log.Info( $"[LootDrop] Inventory full — '{Item.Definition?.Name}' stays in the world." );
		}
	}
}
