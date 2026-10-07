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
	private readonly Dictionary<EquipmentSlot, ItemInstance> _pending = new();
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

	public ItemInstance GetMeleeWeapon() => GetEquippedItem( EquipmentSlot.Melee );
	public ItemInstance GetRangedWeapon() => GetEquippedItem( EquipmentSlot.Ranged );

	/// <summary>Melee set only. Kept so HeldWeaponControl and GetWeaponAttackDef don't change.</summary>
	public ItemInstance GetEquippedWeapon() => GetMeleeWeapon();
	/// <summary>
	/// Builds a live AttackDef from the currently equipped melee weapon.
	/// Returns null if nothing is equipped (caller should fall back to unarmed).
	/// </summary>
	public AttackDef GetWeaponAttackDef()
	{
		var weapon = GetEquippedWeapon();
		if ( weapon?.Definition?.Equipment?.Stats == null )
			return null;

		return AttackData.BuildWeaponAttack( weapon.Definition );
	}

	public AttackDef GetRangedWeaponAttackDef()
	{
		var weapon = GetRangedWeapon();
		if ( weapon?.Definition?.Equipment?.RangedWeapon == null )
			return null;

		return AttackData.BuildWeaponAttack( weapon.Definition );
	}

	/// <summary>
	/// Equip an item instance. Automatically unequips whatever was in that slot first.
	/// </summary>
	public void Equip( ItemInstance instance ) =>
		Equip( instance, instance?.Definition?.Equipment?.Slot ?? EquipmentSlot.None );

	/// <summary>Equips into an explicit slot, so a Flask1 item dropped into Flask2 lands in Flask2.</summary>
	public void Equip( ItemInstance instance, EquipmentSlot targetSlot )
	{
		var item = instance?.Definition;
		if ( item?.Equipment == null || targetSlot == EquipmentSlot.None ) return;

		if ( targetSlot is EquipmentSlot.Melee or EquipmentSlot.Ranged )
		{
			bool isWeapon = item.Equipment.WeaponClass != WeaponClass.None;
			bool isRangedWeapon = item.Equipment.RangedWeapon != null;
			if ( !isWeapon || isRangedWeapon != (targetSlot == EquipmentSlot.Ranged) )
			{
				Log.Warning( $"[EquipmentControl] Cannot equip '{item.Name}' in {targetSlot}; choose its matching weapon slot." );
				return;
			}
		}

		var slot = NormalizeSlot( targetSlot );

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
			// Stats not initialized yet: remember it, ReapplyAll applies it after InitializeFromRegistry.
			_pending[slot] = instance;
			Log.Info( $"[EquipmentControl] StatSheet not ready, queued '{item.Name}' for {slot}." );
			return;
		}

		_pending.Remove( slot );

		// Remove previous item in this slot first
		if ( _slots.ContainsKey( slot ) )
			UnequipInternal( slot, statSheet );

		var entry = new EquippedEntry { Item = instance };
		var stats = item.Equipment.Stats;

		// 1) Named mods (Might, Will, etc.): fixed implicits first, then rolled affixes
		var allMods = (instance.ImplicitMods ?? Enumerable.Empty<ModData>())
			.Concat( instance.RolledMods ?? Enumerable.Empty<ModData>() )
			.ToList();
		foreach ( var mod in allMods )
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

		bool touchedAttribute = allMods.Any( m => IsAttributeStat( m.StatName ) );
		if ( touchedAttribute )
			statSheet.RecalculateDerivedStats();
		OnEquipped?.Invoke( slot, instance);

		Log.Info( $"[EquipmentControl] Equipped '{item.Name}' in {slot}. " +
		          $"Stats.Armor={stats?.Armor:F1}, mods=[{string.Join( ", ", allMods.Select( m => $"{m.StatName}:{m.Value}" ) )}], " +
		          $"applied={entry.AppliedModifiers.Count}, Armor now={statSheet.Armor.Value:F1} (base={statSheet.Armor.BaseValue:F1})" );
	}

	/// <summary>
	/// Unequip the item currently in the given slot, stripping its modifiers.
	/// </summary>
	public void Unequip( EquipmentSlot slot )
	{
		var normalized = NormalizeSlot( slot );
		_pending.Remove( normalized );
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
		if ( _slots.Count == 0 && _pending.Count == 0 ) return;

		// Pending (newer) wins over an already-applied item in the same slot.
		var toApply = new Dictionary<EquipmentSlot, ItemInstance>();
		foreach ( var kv in _slots ) toApply[kv.Key] = kv.Value.Item;
		foreach ( var kv in _pending ) toApply[kv.Key] = kv.Value;

		_slots.Clear();
		_pending.Clear();

		foreach ( var kv in toApply )
			Equip( kv.Value, kv.Key );
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

	// Melee and ranged weapons occupy distinct slots; neither needs remapping.
	private static EquipmentSlot NormalizeSlot( EquipmentSlot slot ) => slot;
}
