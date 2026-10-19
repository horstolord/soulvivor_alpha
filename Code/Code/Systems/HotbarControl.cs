using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.Presentation;
using Sandbox.Code.World;
using HudUI = Sandbox.Code.Presentation.UI;

namespace Sandbox.Code.Systems;

public enum HotbarEntryType
{
	None,
	Spell,
	Technique,
	Item
}

public sealed class HotbarEntry
{
	[Property] public HotbarEntryType Type { get; set; }
	[Property] public string Id { get; set; }
	[Property] public string InstanceId { get; set; }
}

/// <summary>Owns the player's eight configurable hotbar bindings and their input.</summary>
public sealed class HotbarControl : Component
{
	public const int SlotCount = 8;

	[Property] public List<string> Keys { get; set; } = new()
	{
		"3", "4", "5", "6", "7", "8", "9", "0"
	};

	[Property] public List<HotbarEntry> Entries { get; set; } = new()
	{
		new() { Type = HotbarEntryType.Spell, Id = "airball" },
		new() { Type = HotbarEntryType.Spell, Id = "dual_frost_beam" },
		new() { Type = HotbarEntryType.Spell, Id = "cluster_bomb" },
		new() { Type = HotbarEntryType.Spell, Id = "raw_force" },
		new() { Type = HotbarEntryType.Spell, Id = "frostball" },
		new() { Type = HotbarEntryType.Technique, Id = "kick" },
		new() { Type = HotbarEntryType.Technique, Id = "shoot" },
		new()
	};

	public int Revision { get; private set; }

	private Player _player;
	private SpellComponent _spell;

	public int GetVisualHash()
	{
		CacheComponents();
		var hash = new HashCode();

		hash.Add( Revision );
		hash.Add( HudUI.MenuOpen );
		hash.Add( _player?.StatSheet?.CurrentHealth ?? 0f );
		hash.Add( _player?.StatSheet?.CurrentStamina ?? 0f );
		hash.Add( _player?.StatSheet?.CurrentEnergy ?? 0f );
		hash.Add( _player?.StateComp?.CurrentState );
		hash.Add( _player?.Combat?.CanAttack ?? false );

		for ( int i = 0; i < SlotCount; i++ )
		{
			var entry = GetEntry( i );
			var item = GetItem( i );
			hash.Add( GetKey( i ) );
			hash.Add( entry?.Type ?? HotbarEntryType.None );
			hash.Add( entry?.Id );
			hash.Add( entry?.InstanceId );
			hash.Add( CanUseEntry( i ) );
			hash.Add( item?.Quantity ?? 0 );
			hash.Add( item?.RemainingCharges ?? 0 );
			hash.Add( item?.MaxCharges ?? 0 );
		}
		return hash.ToHashCode();
	}

	protected override void OnStart()
	{
		CacheComponents();
		FillMissingEntries();
	}

	protected override void OnUpdate()
	{
		CacheComponents();
		if ( HudUI.MenuOpen ) return;

		for ( int i = 0; i < SlotCount; i++ )
		{
			var key = GetKey( i );
			if ( !string.IsNullOrWhiteSpace( key ) && Input.Keyboard.Pressed( key ) )
				TryUseSlot( i );
		}
	}

	public HotbarEntry GetEntry( int index ) =>
		Entries != null && index >= 0 && index < Entries.Count ? Entries[index] : null;

	public string GetKey( int index ) =>
		Keys != null && index >= 0 && index < Keys.Count ? Keys[index] : string.Empty;

	public InventoryItem GetItem( int index )
	{
		var entry = GetEntry( index );
		return entry?.Type == HotbarEntryType.Item
			? HudUI.LocalInventory?.FindItemById( entry.Id, entry.InstanceId )
			: null;
	}

	public string GetEntryLabel( int index )
	{
		var entry = GetEntry( index );
		if ( entry == null || entry.Type == HotbarEntryType.None ) return string.Empty;
		if ( entry.Type == HotbarEntryType.Item )
			return GetItem( index )?.Name ?? entry.Id ?? "Missing item";

		return entry.Id?.Replace( '_', ' ' ) ?? string.Empty;
	}

	public string GetItemBadge( int index )
	{
		var entry = GetEntry( index );
		var item = GetItem( index );
		if ( entry?.Type != HotbarEntryType.Item || item == null ) return string.Empty;
		if ( item.IsFlask ) return $"{item.RemainingCharges}/{item.MaxCharges}";
		if ( item.Quantity > 1 ) return $"x{item.Quantity}";
		if ( IsEquippedWeapon( item ) ) return "E";
		return string.Empty;
	}

	public bool CanUseEntry( int index )
	{
		CacheComponents();
		var entry = GetEntry( index );
		if ( entry == null || string.IsNullOrWhiteSpace( entry.Id ) || _player == null )
			return false;

		switch ( entry.Type )
		{
			case HotbarEntryType.Spell:
			{
				if ( _spell == null || !_spell.CanCast() ) return false;
				if ( !RuneLibrary.TryGetPreset( entry.Id, out var runes ) ) return false;
				var preview = RuneEvaluator.EvaluateSequence(
					runes, _player.GameObject, _player.GameObject.WorldPosition, GetAimDirection() );
				return preview.Success && _player.CanPayCost( preview.ConsolidatedContext );
			}
			case HotbarEntryType.Technique:
			{
				var attack = ResolveAttack( entry.Id );
				return attack != null
					&& _player.Combat?.CanAttack == true
					&& (_player.StateComp == null || _player.StateComp.CurrentState == ActorStateType.Idle)
					&& _player.CanPayCost( attack );
			}
			case HotbarEntryType.Item:
			{
				var item = HudUI.LocalInventory?.FindItemById( entry.Id, entry.InstanceId );
				if ( item?.Definition == null ) return false;
				if ( item.IsFlask ) return item.RemainingCharges > 0;
				return item.Definition.IsConsumable || item.Definition.IsEquippable;
			}
			default:
				return false;
		}
	}

	public bool TryUseSlot( int index )
	{
		var entry = GetEntry( index );
		if ( entry == null || !CanUseEntry( index ) ) return false;

		switch ( entry.Type )
		{
			case HotbarEntryType.Spell:
			{
				if ( !RuneLibrary.TryGetPreset( entry.Id, out var runes ) ) return false;
				var camera = Scene.Camera;
				var rotation = camera?.WorldRotation ?? _player.GameObject.WorldRotation;
				var origin = camera?.WorldPosition ?? (_player.GameObject.WorldPosition + Vector3.Up * 50f);
				return _spell.CastSpell( runes, origin + rotation.Forward * 30f, rotation.Forward );
			}
			case HotbarEntryType.Technique:
				return _player.TryUseAttack( ResolveAttack( entry.Id ) );
			case HotbarEntryType.Item:
				return HudUI.LocalInventory?.TryUseItemById( entry.Id, entry.InstanceId ) == true;
			default:
				return false;
		}
	}

	public void SetItem( int index, string itemId )
	{
		if ( index < 0 || index >= SlotCount || string.IsNullOrWhiteSpace( itemId ) ) return;
		EnsureEntryCapacity();
		Entries[index] = new HotbarEntry { Type = HotbarEntryType.Item, Id = itemId };
		Revision++;
	}

	public void SetItem( int index, InventoryItem item )
	{
		if ( index < 0 || index >= SlotCount || item?.Definition == null
			|| string.IsNullOrWhiteSpace( item.Instance?.InstanceId ) ) return;
		EnsureEntryCapacity();
		Entries[index] = new HotbarEntry
		{
			Type = HotbarEntryType.Item,
			Id = item.Definition.Id,
			InstanceId = item.Instance?.InstanceId
		};
		Revision++;
	}

	public void SwapSlots( int first, int second )
	{
		if ( first < 0 || first >= SlotCount || second < 0 || second >= SlotCount || first == second )
			return;

		EnsureEntryCapacity();
		(Entries[first], Entries[second]) = (Entries[second], Entries[first]);
		Revision++;
	}

	public void ClearSlot( int index )
	{
		if ( index < 0 || index >= SlotCount ) return;
		EnsureEntryCapacity();
		Entries[index] = new HotbarEntry();
		Revision++;
	}

	private void CacheComponents()
	{
		_player ??= Components.GetInAncestorsOrSelf<Player>();
		_spell ??= _player?.Components.GetOrCreate<SpellComponent>();
	}

	private void FillMissingEntries()
	{
		Keys ??= new List<string>();
		while ( Keys.Count < SlotCount )
			Keys.Add( new[] { "3", "4", "5", "6", "7", "8", "9", "0" }[Keys.Count] );

		Entries ??= new List<HotbarEntry>();
		EnsureEntryCapacity();
	}

	private void EnsureEntryCapacity()
	{
		Entries ??= new List<HotbarEntry>();
		while ( Entries.Count < SlotCount )
			Entries.Add( new HotbarEntry() );
	}

	private AttackDef ResolveAttack( string id ) => id?.ToLowerInvariant() switch
	{
		"kick" => AttackData.Kick,
		"shoot" => _player?.Equipment?.GetRangedWeaponAttackDef() ?? AttackData.Shoot,
		_ => null
	};

	private Vector3 GetAimDirection()
	{
		var direction = Scene.Camera?.WorldRotation.Forward ?? _player.GameObject.WorldRotation.Forward;
		return direction.LengthSquared > 0.001f ? direction.Normal : Vector3.Forward;
	}

	private bool IsEquippedWeapon( InventoryItem item )
	{
		if ( item?.Definition?.Equipment == null ) return false;
		if ( item.Definition.Equipment.WeaponClass == WeaponClass.None ) return false;
		return HudUI.LocalInventory?.IsEquippedById( item.Definition.Id, item.Instance?.InstanceId ) == true;
	}
}
