using System.Collections.Generic;
using System.Linq;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using System;

namespace Sandbox.Code.Systems;

/// <summary>
/// Manages an actor's equipped items.
/// Applying an item adds its Mods to the StatSheet as tracked StatModifiers.
/// Removing it strips those same modifiers back off cleanly.
/// </summary>
public class EquipmentControl : Component
{
	// Key = normalized equipment slot, Value = equipped item + its applied modifiers
	private readonly Dictionary<EquipmentSlot, EquippedEntry> _slots = new();

	private sealed class EquippedEntry
	{
		public ItemInstance Item;
		public List<(Stat Stat, StatModifier Modifier)> AppliedModifiers = new();
	}

	public Action<EquipmentSlot, ItemInstance> OnEquipped;
	public Action<EquipmentSlot, ItemInstance> OnUnequipped;

	// ============ PUBLIC API ============

	public ItemInstance GetEquippedItem( EquipmentSlot slot ) =>
		_slots.TryGetValue( NormalizeSlot( slot ), out var entry ) ? entry.Item : null;

	public ItemInstance GetEquippedWeapon() => GetEquippedItem( EquipmentSlot.MainHand1 );

	/// <summary>
	/// Builds a live AttackDef from the currently equipped main-hand weapon.
	/// Returns null if nothing is equipped (caller should fall back to unarmed).
	/// </summary>
	public AttackDef GetWeaponAttackDef()
	{
		var weapon = GetEquippedWeapon();
		if ( weapon?.Definition?.Equipment?.Stats == null )
			return null;

		return AttackData.BuildWeaponAttack( weapon.Definition );
	}

	/// <summary>
	/// Equip an item instance. Automatically unequips whatever was in that slot first.
	/// </summary>
	public void Equip( ItemInstance instance )
	{
		var item = instance?.Definition;
		if ( item?.Equipment == null ) return;

		var actor = Components.GetInAncestorsOrSelf<Actor>();
		var statSheet = actor?.StatSheet
		                ?? Components.GetInAncestorsOrSelf<StatSheet>();
		if ( statSheet == null )
		{
			Log.Warning( $"[EquipmentControl] No StatSheet found on {GameObject.Name}" );
			return;
		}

		if ( statSheet.Armor == null )
		{
			Log.Warning( $"[EquipmentControl] StatSheet on {GameObject.Name} is not initialized yet — deferring equip of '{item.Name}'" );
			return;
		}

		var slot = NormalizeSlot( item.Equipment.Slot );

		// Remove previous item in this slot first
		if ( _slots.ContainsKey( slot ) )
			UnequipInternal( slot, statSheet );

		var entry = new EquippedEntry { Item = instance };
		var stats = item.Equipment.Stats;

		// 1) Named Mods (Might, Will, etc.) from RolledMods
		foreach ( var mod in instance.RolledMods ?? Enumerable.Empty<ModData>() )
		{
			var stat = statSheet.GetStat( mod.StatName );
			if ( stat == null )
			{
				Log.Warning( $"[EquipmentControl] Item '{item.Name}' references unknown stat '{mod.StatName}'" );
				continue;
			}
			var modifier = new StatModifier( mod.Value, mod.Type, source: entry );
			stat.AddModifier( modifier );
			entry.AppliedModifiers.Add( (stat, modifier) );
		}

		// 2) EquipmentStatBlock.Armor → StatSheet.Armor (same role as BaseDamage for weapons)
		if ( stats != null && stats.Armor != 0f )
		{
			var modifier = new StatModifier( stats.Armor, ModifierType.Flat, source: entry );
			statSheet.Armor.AddModifier( modifier );
			entry.AppliedModifiers.Add( (statSheet.Armor, modifier) );
		}

		// 3) BaseAttackSpeed → AttackSpeed
		if ( stats != null && stats.BaseAttackSpeed != 0f && stats.BaseAttackSpeed != 1f )
		{
			var attackSpeedStat = statSheet.GetStat( "AttackSpeed" );
			if ( attackSpeedStat != null )
			{
				float delta = (stats.BaseAttackSpeed - 1f) * 100f;
				var modifier = new StatModifier( delta, ModifierType.Flat, source: entry );
				attackSpeedStat.AddModifier( modifier );
				entry.AppliedModifiers.Add( (attackSpeedStat, modifier) );
			}
		}

		_slots[slot] = entry;

		bool touchedAttribute = (instance.RolledMods ?? Enumerable.Empty<ModData>()).Any( m => IsAttributeStat( m.StatName ) );
		if ( touchedAttribute )
			statSheet.RecalculateDerivedStats();
		OnEquipped?.Invoke( slot, instance);

		Log.Info( $"[EquipmentControl] Equipped '{item.Name}' in {slot}. " +
		          $"Stats.Armor={stats?.Armor:F1}, mods=[{string.Join( ", ", (instance.RolledMods ?? Enumerable.Empty<ModData>()).Select( m => $"{m.StatName}:{m.Value}" ) )}], " +
		          $"applied={entry.AppliedModifiers.Count}, Armor now={statSheet.Armor.Value:F1} (base={statSheet.Armor.BaseValue:F1})" );
	}

	/// <summary>
	/// Unequip the item currently in the given slot, stripping its modifiers.
	/// </summary>
	public void Unequip( EquipmentSlot slot )
	{
		var normalized = NormalizeSlot( slot );
		if ( !_slots.ContainsKey( normalized ) ) return;

		var actor = Components.GetInAncestorsOrSelf<Actor>();
		var statSheet = actor?.StatSheet
		                ?? Components.GetInAncestorsOrSelf<StatSheet>();
		UnequipInternal( normalized, statSheet );

		if ( statSheet != null )
			statSheet.RecalculateDerivedStats();
	}

	/// <summary>
	/// Re-applies every currently equipped item's modifiers.
	/// Call after StatSheet.InitializeFromRegistry, which replaces Stat instances and drops old modifiers.
	/// </summary>
	public void ReapplyAll()
	{
		if ( _slots.Count == 0 ) return;

		var items = _slots.Values.Select( e => e.Item ).ToList();
		_slots.Clear();

		foreach ( var item in items )
			Equip( item );
	}

	// ============ INTERNALS ============

	private void UnequipInternal( EquipmentSlot slot, StatSheet statSheet )
	{
		if ( !_slots.TryGetValue( slot, out var entry ) ) return;

		foreach ( var (stat, modifier) in entry.AppliedModifiers )
			stat.RemoveModifier( modifier );

		_slots.Remove( slot );
		OnUnequipped?.Invoke( slot, entry.Item);
		Log.Info( $"[EquipmentControl] Unequipped '{entry.Item?.Definition?.Name ?? "Item"}' from slot {slot}." );
	}

	private static bool IsAttributeStat( string name ) =>
		name is "Might" or "Swiftness" or "Endurance" or "Will" or "Acuity" or "Wisdom";

	private static EquipmentSlot NormalizeSlot( EquipmentSlot slot ) => slot switch
	{
		EquipmentSlot.MainHand2 or EquipmentSlot.MainHand3 => EquipmentSlot.MainHand1,
		EquipmentSlot.OffHand2  or EquipmentSlot.OffHand3  => EquipmentSlot.OffHand1,
		_ => slot
	};
}
