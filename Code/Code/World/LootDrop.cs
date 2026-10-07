using System;
using System.Collections.Generic;
using System.Linq;
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
	private bool _visualBuilt;

	/// <summary>Tag a prefab's emissive/glow child so only those renderers receive rarity tint.</summary>
	public const string GlowTag = "rarity_glow";

	private static readonly HashSet<string> _warnedMissing = new();

	protected override void OnStart()
	{
		_baseZ = GameObject.WorldPosition.z;
		_initialized = true;
	}

	/// <summary>Build on the first update, after the spawner has assigned Item.</summary>
	private void BuildVisual()
	{
		_visualBuilt = true;
		var prefabFile = ResolvePrefab( Item?.Definition );
		if ( prefabFile == null )
		{
			Log.Warning( "[LootDrop] Could not resolve an item prefab or shared placeholder." );
			return;
		}

		var visual = SceneUtility.GetPrefabScene( prefabFile ).Clone();
		visual.Parent = GameObject;
		visual.LocalPosition = Vector3.Zero;
		ApplyRarityGlow( visual, Item?.Rarity ?? ItemRarity.Common );
	}

	/// <summary>Uses the item's authored world prefab path, then legacy and shared placeholders.</summary>
	private static PrefabFile ResolvePrefab( ItemDef def )
	{
		if ( def != null && !string.IsNullOrWhiteSpace( def.WorldPrefabPath ) )
		{
			if ( ResourceLibrary.TryGet<PrefabFile>( def.WorldPrefabPath, out var itemPrefab ) )
				return itemPrefab;

			if ( _warnedMissing.Add( def.Id ?? def.WorldPrefabPath ) )
				Log.Warning( $"[LootDrop] Could not find item prefab '{def.WorldPrefabPath}' for '{def.Name}'." );
		}

		if ( def != null && !string.IsNullOrWhiteSpace( def.Id )
			&& ResourceLibrary.TryGet<PrefabFile>( $"{def.Id}.prefab", out var byId ) )
			return byId;

		if ( ResourceLibrary.TryGet<PrefabFile>( "item.prefab", out var generic ) ) return generic;
		if ( ResourceLibrary.TryGet<PrefabFile>( "lootitem.prefab", out var legacy ) ) return legacy;
		return null;
	}

	private static void ApplyRarityGlow( GameObject root, ItemRarity rarity )
	{
		var color = RarityStyle.ColorOf( rarity );
		var renderers = root.Components.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants ).ToList();
		var glowing = renderers.Where( r => r.GameObject.Tags.Has( GlowTag ) ).ToList();

		foreach ( var renderer in glowing.Count > 0 ? glowing : renderers )
			renderer.Tint = color;
	}

	protected override void OnUpdate()
	{
		if ( !_initialized )
		{
			_baseZ = GameObject.WorldPosition.z;
			_initialized = true;
		}

		if ( !_visualBuilt )
			BuildVisual();

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
