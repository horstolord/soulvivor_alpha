using System.Collections.Generic;
using System.Linq;
using Sandbox.Code.Data;

namespace Sandbox.Code.Presentation;

/// <summary>
/// Temporary UI-facing inventory state until the gameplay inventory system owns this data.
/// </summary>
public sealed class InventoryPanel
{
    public const int Columns = 10;
    public const int Rows = 5;

    public List<InventorySlot> Slots { get; private set; } = new();
    public Dictionary<EquipmentSlot, InventoryItem> EquippedSlots { get; private set; } = new();

    public InventoryItem HoveredItem { get; private set; }
    public int? SelectedSlotId { get; private set; }
    private int? draggingSlotId;

    public int UsedSlots => Slots.Count( s => s.Item != null );
    public int TotalSlots => Slots.Count;
    public int Revision { get; private set; }
    public int TotalAttack => EquippedSlots.Values.Sum( item => (int)(item.Definition?.Equipment?.Stats?.BaseDamage ?? item.Stats.Attack) );
    public int TotalDefense => EquippedSlots.Values.Sum( item => (int)(item.Definition?.Equipment?.Stats?.Armor ?? item.Stats.Defense) );
    public int TotalSpeed => EquippedSlots.Values.Sum( item => item.Stats.Speed );

    public InventoryPanel()
    {
        InitializeGrid();
        SeedStarterItems();
    }

    public InventoryItem GetEquippedItem( EquipmentSlot slot )
    {
        EquippedSlots.TryGetValue( NormalizeSlot( slot ), out var item );
        return item;
    }

    public void OnSlotMouseDown( InventorySlot slot )
    {
        if ( slot.IsLocked ) return;
        SelectedSlotId = slot.Id;
        draggingSlotId = slot.Id;
    }

    public void OnSlotMouseUp( InventorySlot targetSlot )
    {
        if ( draggingSlotId.HasValue && draggingSlotId.Value != targetSlot.Id )
        {
            SwapSlots( draggingSlotId.Value, targetSlot.Id );
        }

        draggingSlotId = null;
    }

    public void SetHoveredItem( InventoryItem item )
    {
        if ( HoveredItem == item ) return;
        HoveredItem = item;
        Revision++;
    }

    public void ClearHoveredItem( InventoryItem item )
    {
        if ( HoveredItem == item )
        {
            HoveredItem = null;
            Revision++;
        }
    }

    public void SwapSlots( int fromId, int toId )
    {
        var fromSlot = Slots.FirstOrDefault( s => s.Id == fromId );
        var toSlot = Slots.FirstOrDefault( s => s.Id == toId );

        if ( fromSlot == null || toSlot == null || toSlot.IsLocked ) return;

        (fromSlot.Item, toSlot.Item) = (toSlot.Item, fromSlot.Item);
        Revision++;
    }

    public void EquipItem( InventorySlot slot )
    {
        if ( slot?.Item?.Definition?.IsEquippable != true ) return;

        var equipmentSlot = NormalizeSlot( slot.Item.Definition.Equipment.Slot );
        if ( equipmentSlot == EquipmentSlot.Ring1
        	&& EquippedSlots.ContainsKey( EquipmentSlot.Ring1 )
        	&& !EquippedSlots.ContainsKey( EquipmentSlot.Ring2 ) )
        {
        	equipmentSlot = EquipmentSlot.Ring2;
        }
        EquipItemInSlot( slot, equipmentSlot );
    }

    public void EquipSelectedItemTo( EquipmentSlot targetSlot )
    {
        if ( !SelectedSlotId.HasValue ) return;

        var slot = Slots.FirstOrDefault( s => s.Id == SelectedSlotId.Value );
        if ( slot?.Item?.Definition?.IsEquippable != true ) return;

        var normalizedTarget = NormalizeSlot( targetSlot );
        var itemSlot = NormalizeSlot( slot.Item.Definition.Equipment.Slot );

        // Allow flasks to equip to either Flask1 or Flask2
        bool isFlaskSlotMatch = (normalizedTarget is EquipmentSlot.Flask1 or EquipmentSlot.Flask2)
                                && (slot.Item.Definition.Equipment.Slot is EquipmentSlot.Flask1 or EquipmentSlot.Flask2 || slot.Item.Definition.Tags.Contains("flask"));
        bool isRingSlotMatch = (normalizedTarget is EquipmentSlot.Ring1 or EquipmentSlot.Ring2)
                               && (itemSlot is EquipmentSlot.Ring1 or EquipmentSlot.Ring2);

        if ( !isFlaskSlotMatch && !isRingSlotMatch && normalizedTarget != itemSlot ) return;

        EquipItemInSlot( slot, normalizedTarget );
    }

    public void UseConsumable( InventorySlot slot )
    {
        if ( slot?.Item == null || !slot.Item.Definition.IsConsumable ) return;

        var player = Actors.Player.Local;
        if ( player == null ) return;

        bool used = player.UsePotion( slot.Item );
        if ( used )
        {
            slot.Item.Quantity--;
            if ( slot.Item.Quantity <= 0 )
            {
                slot.Item = null;
            }
            Revision++;
        }
    }

    public bool UseFlask( EquipmentSlot slot )
    {
        var normalized = NormalizeSlot( slot );
        if ( !EquippedSlots.TryGetValue( normalized, out var flaskItem ) || flaskItem == null ) return false;

        EnsureFlaskCharges( flaskItem );

        if ( flaskItem.RemainingCharges <= 0 )
        {
            Log.Info( $"[Flask] {flaskItem.Name} has no remaining charges!" );
            return false;
        }

        var player = Actors.Player.Local;
        if ( player == null ) return false;

        flaskItem.RemainingCharges--;
        if ( flaskItem.Instance != null )
            flaskItem.Instance.RemainingCharges = flaskItem.RemainingCharges;
        player.ApplyFlaskEffect( flaskItem.Definition );
        Revision++;
        Log.Info( $"[Flask] Used {flaskItem.Name}. Charges left: {flaskItem.RemainingCharges}/{flaskItem.MaxCharges}" );
        return true;
    }

    public void RefillFlasks( int amount = 1 )
    {
        int refilledCount = 0;
        foreach ( var slot in new[] { EquipmentSlot.Flask1, EquipmentSlot.Flask2 } )
        {
            if ( EquippedSlots.TryGetValue( slot, out var flask ) && flask != null && flask.IsFlask )
            {
                EnsureFlaskCharges( flask );
                if ( flask.MaxCharges > 0 && flask.RemainingCharges < flask.MaxCharges )
                {
                    flask.RemainingCharges = System.Math.Min( flask.MaxCharges, flask.RemainingCharges + amount );
                    if ( flask.Instance != null )
                        flask.Instance.RemainingCharges = flask.RemainingCharges;
                    refilledCount++;
                }
            }
        }

        // Also refill any flasks sitting in inventory grid
        foreach ( var slot in Slots )
        {
            if ( slot.Item != null && slot.Item.IsFlask )
            {
                EnsureFlaskCharges( slot.Item );
                if ( slot.Item.MaxCharges > 0 && slot.Item.RemainingCharges < slot.Item.MaxCharges )
                {
                    slot.Item.RemainingCharges = System.Math.Min( slot.Item.MaxCharges, slot.Item.RemainingCharges + amount );
                    if ( slot.Item.Instance != null )
                        slot.Item.Instance.RemainingCharges = slot.Item.RemainingCharges;
                    refilledCount++;
                }
            }
        }

        if ( refilledCount > 0 )
        {
            Revision++;
            Log.Info( $"[Flask] Refilled {refilledCount} flask(s) (+{amount} charge)." );
        }
    }

    /// <summary>
    /// Repairs flasks that were created without MaxCharges (old loot path left them at 0).
    /// </summary>
    private static void EnsureFlaskCharges( InventoryItem item )
    {
        if ( item == null || !item.IsFlask ) return;

        int defCharges = item.Definition?.Consumable?.Charges ?? 0;
        if ( defCharges <= 0 || item.MaxCharges > 0 ) return;

        item.MaxCharges = defCharges;
        item.RemainingCharges = defCharges;
        if ( item.Instance != null )
        {
            item.Instance.MaxCharges = defCharges;
            item.Instance.RemainingCharges = defCharges;
        }
    }

    public bool AddItem( ItemInstance instance )
    {
        if ( instance?.Definition == null ) return false;

        var def = instance.Definition;
        int remaining = System.Math.Max( 1, instance.StackCount );
        int starting = remaining;

        if ( def.Stackable )
        {
            int maxStack = System.Math.Max( 1, def.MaxStack );
            foreach ( var slot in Slots )
            {
                if ( remaining <= 0 ) break;
                if ( slot.IsLocked || slot.Item == null ) continue;
                if ( slot.Item.Definition?.Id != def.Id ) continue;

                int space = maxStack - slot.Item.Quantity;
                if ( space <= 0 ) continue;

                int take = System.Math.Min( space, remaining );
                slot.Item.Quantity += take;
                if ( slot.Item.Instance != null )
                    slot.Item.Instance.StackCount = slot.Item.Quantity;
                remaining -= take;
            }
        }

        bool usedOriginalInstance = false;
        while ( remaining > 0 )
        {
            var empty = Slots.FirstOrDefault( s => s.Item == null && !s.IsLocked );
            if ( empty == null ) break;

            int place = def.Stackable
                ? System.Math.Min( remaining, System.Math.Max( 1, def.MaxStack ) )
                : 1;

            ItemInstance placed;
            if ( !usedOriginalInstance )
            {
                placed = instance;
                usedOriginalInstance = true;
            }
            else
            {
                placed = ItemInstance.FromDefinition( def, place );
                if ( instance.MaxCharges > 0 )
                {
                    placed.MaxCharges = instance.MaxCharges;
                    placed.RemainingCharges = instance.RemainingCharges;
                }
            }

            placed.StackCount = place;
            empty.Item = InventoryItem.FromInstance( placed, place );
            remaining -= place;

            if ( !def.Stackable )
                break;
        }

        bool fullyAdded = remaining <= 0;
        if ( !fullyAdded )
            instance.StackCount = remaining;

        if ( remaining < starting )
            Revision++;

        return fullyAdded;
    }

    public void Unequip( EquipmentSlot equipmentSlot )
    {
        SelectedSlotId = null;
        var normalizedSlot = NormalizeSlot( equipmentSlot );
        if ( !EquippedSlots.TryGetValue( normalizedSlot, out var item ) ) return;

        var emptySlot = Slots.FirstOrDefault( s => s.Item == null && !s.IsLocked );
        if ( emptySlot == null ) return;

        emptySlot.Item = item;
        EquippedSlots.Remove( normalizedSlot );

        // Strip stat modifiers from the gameplay layer
        Sandbox.Code.Actors.Player.Local?.Equipment?.Unequip( normalizedSlot );

        Revision++;
    }

    private void EquipItemInSlot( InventorySlot inventorySlot, EquipmentSlot equipmentSlot )
    {
        var itemToEquip = inventorySlot.Item;
        if ( itemToEquip == null ) return;

        if ( EquippedSlots.TryGetValue( equipmentSlot, out var equippedItem ) )
        {
            inventorySlot.Item = equippedItem;
        }
        else
        {
            inventorySlot.Item = null;
        }

        EquippedSlots[equipmentSlot] = itemToEquip;

        // Forward to the gameplay EquipmentControl so stat modifiers are actually applied
        var def = itemToEquip.Definition;
        var instance = itemToEquip.Instance;
        Log.Info( $"[Inventory] Equipping '{def?.Name}' — Definition.Stats.Armor={def?.Equipment?.Stats?.Armor:F1}, Implicits={instance?.ImplicitMods?.Count ?? 0}, Affixes={instance?.RolledMods?.Count ?? 0}" );
        Actors.Player.Local?.Equipment?.Equip( itemToEquip.Instance, equipmentSlot );

        SelectedSlotId = null;
        draggingSlotId = null;
        Revision++;
    }

    private void InitializeGrid()
    {
        Slots.Clear();

        int count = Columns * Rows;
        for ( int i = 0; i < count; i++ )
        {
            Slots.Add( new InventorySlot { Id = i, IsLocked = false } );
        }
    }

    private void SeedStarterItems()
    {
        SetSlot( 0, ItemData.rustySword );
        SetSlot( 1, ItemData.tatteredHood );
        SetSlot( 2, ItemData.healingFlask, 1 );
        SetSlot( 3, ItemData.manaFlask, 1 );
        SetSlot( 4, ItemData.strengthPotion, 3 );
        SetSlot( 5, ItemData.hastePotion, 3 );
        SetSlot( 6, ItemData.ironSkinPotion, 3 );
        SetSlot( 7, ItemData.ironOre, 12 );
    }

    private void SetSlot( int index, ItemDef definition, int quantity = 1 )
    {
        if ( index < 0 || index >= Slots.Count || definition == null ) return;

        Slots[index].Item = InventoryItem.FromDefinition( definition, quantity );
    }

    // Melee and ranged weapons occupy distinct slots.
    private static EquipmentSlot NormalizeSlot( EquipmentSlot slot ) => slot;
}

public sealed class InventorySlot
{
    public int Id { get; init; }
    public bool IsLocked { get; init; }
    public InventoryItem Item { get; set; }
}

public sealed class InventoryItem
{
    public ItemInstance Instance { get; init; }
    public ItemDef Definition => Instance?.Definition;
    public string Name => Definition?.Name ?? "Unknown Item";
    public string Description => Definition?.Description ?? "";
    public ItemRarity Rarity => Instance?.Rarity ?? Definition?.Rarity ?? ItemRarity.Common;
    public string RarityClass => RarityStyle.CssClass( Rarity );
    public int Quantity { get; set; } = 1;
    public int RemainingCharges { get; set; }
    public int MaxCharges { get; set; }
    public InventoryItemStats Stats { get; init; } = new();
    public string IconGlyph { get; init; } = "?";

    public IReadOnlyList<ModData> ImplicitMods => (IReadOnlyList<ModData>)Instance?.ImplicitMods ?? System.Array.Empty<ModData>();
    public IReadOnlyList<ModData> AffixMods => (IReadOnlyList<ModData>)Instance?.RolledMods ?? System.Array.Empty<ModData>();

    public EquipmentStatBlock EquipmentStats => Definition?.Equipment?.Stats;
    public ConsumableData Consumable => Definition?.Consumable;
    public EquipmentSlot Slot => Definition?.Equipment?.Slot ?? EquipmentSlot.None;
    public string ItemTypeLabel => GetItemTypeLabel( Definition );
    public bool IsFlask => IsFlaskDefinition( Definition );
    public bool IsPotion => Definition?.Tags?.Contains( "potion" ) == true;

    public static bool IsFlaskDefinition( ItemDef definition )
    {
        if ( definition == null ) return false;
        if ( definition.Tags?.Contains( "flask" ) == true ) return true;
        return definition.Equipment?.Slot is EquipmentSlot.Flask1 or EquipmentSlot.Flask2;
    }

    public static string FormatMod( ModData mod )
    {
        if ( mod == null ) return string.Empty;
        string name = FormatStatName( mod.StatName );
        string sign = mod.Value >= 0 ? "+" : "";
        string valStr = mod.Type == ModifierType.Percent ? $"{sign}{mod.Value:0.#}%" : $"{sign}{mod.Value:0.#}";
        return $"{valStr} {name}";
    }

    public static string FormatStatName( string statName )
    {
        return statName switch
        {
            "MaxHealth" => "Max Health",
            "MaxStamina" => "Max Stamina",
            "MaxEnergy" => "Max Mana",
            "AttackSpeed" => "Attack Speed",
            "PhysicalDamage" => "Physical Damage",
            "MagicDamage" => "Magic Damage",
            "FireDamage" => "Fire Damage",
            "LightningDamage" => "Lightning Damage",
            "CritChance" => "Critical Chance",
            "CritDamage" => "Critical Multiplier",
            _ => statName
        };
    }

    public static string GetItemTypeLabel( ItemDef def )
    {
        if ( def == null ) return "Item";
        if ( def.Equipment != null && def.Equipment.Slot != EquipmentSlot.None )
        {
            if ( def.Tags?.Contains( "bow" ) == true ) return "Bow";
            if ( def.Tags?.Contains( "polearm" ) == true ) return "Polearm";
            if ( def.Tags?.Contains( "spear" ) == true ) return "Spear";
            if ( def.Tags?.Contains( "hammer" ) == true || def.Tags?.Contains( "mace" ) == true ) return "Hammer / Mace";
            if ( def.Tags?.Contains( "axe" ) == true ) return "Axe";
            if ( def.Tags?.Contains( "sword" ) == true ) return "Sword";

            return def.Equipment.Slot switch
            {
                EquipmentSlot.Melee => "Melee Weapon",
                EquipmentSlot.Ranged => "Ranged Weapon",
                EquipmentSlot.OffHand => "Off Hand / Shield",
                EquipmentSlot.Head => "Head Armor",
                EquipmentSlot.Chest => "Chest Armor",
                EquipmentSlot.Hands => "Gloves / Hands",
                EquipmentSlot.Belt => "Belt / Waist",
                EquipmentSlot.Feet => "Boots / Feet",
                EquipmentSlot.Amulet => "Amulet",
                EquipmentSlot.Ring1 or EquipmentSlot.Ring2 => "Ring",
                EquipmentSlot.Flask1 or EquipmentSlot.Flask2 => "Flask",
                _ => "Equipment"
            };
        }
        return def.Category switch
        {
            ItemCategory.Consumable => def.Tags?.Contains( "potion" ) == true ? "Potion" : "Consumable",
            ItemCategory.Material => "Crafting Material",
            ItemCategory.KeyItem => "Key Item",
            _ => "Item"
        };
    }

    public static InventoryItem FromDefinition( ItemDef definition, int quantity = 1 )
    {
        var instance = ItemInstance.FromDefinition( definition, quantity );
        return FromInstance( instance, quantity );
    }

    public static InventoryItem FromInstance( ItemInstance instance, int quantity = 1 )
    {
        var definition = instance?.Definition;
        var stats = definition?.Equipment?.Stats;
        int defCharges = definition?.Consumable?.Charges ?? 0;

        // Loot used to spawn instances with MaxCharges left at 0 — fall back to the def.
        int maxCharges = instance != null && instance.MaxCharges > 0 ? instance.MaxCharges : defCharges;
        int charges = instance != null && instance.MaxCharges > 0
            ? instance.RemainingCharges
            : (instance != null && instance.RemainingCharges > 0 ? instance.RemainingCharges : defCharges);

        if ( instance != null && maxCharges > 0 && instance.MaxCharges <= 0 )
        {
            instance.MaxCharges = maxCharges;
            instance.RemainingCharges = charges;
        }

        var mods = (instance?.ImplicitMods ?? new List<ModData>())
            .Concat( instance?.RolledMods ?? new List<ModData>() )
            .ToList();

        return new InventoryItem
        {
            Instance = instance,
            Quantity = quantity,
            RemainingCharges = charges,
            MaxCharges = maxCharges,
            IconGlyph = GetIconGlyph( definition ),
            Stats = new InventoryItemStats
            {
                Attack = (int)(stats?.BaseDamage ?? 0f),
                Defense = (int)(stats?.Armor ?? 0f),
                Speed = (int)mods.Where( mod => mod.StatName == "Swiftness" ).Sum( mod => mod.Value )
            }
        };
    }

    private static string GetIconGlyph( ItemDef definition )
    {
        if ( definition == null ) return "?";

        if ( definition.Tags.Contains( "sword" ) ) return "SW";
        if ( definition.Tags.Contains( "bow" ) ) return "BW";
        if ( definition.Tags.Contains( "polearm" ) ) return "PL";
        if ( definition.Tags.Contains( "spear" ) ) return "SP";
        if ( definition.Tags.Contains( "hammer" ) || definition.Tags.Contains( "mace" ) ) return "HM";
        if ( definition.Tags.Contains( "axe" ) ) return "AX";
        if ( definition.Tags.Contains( "ring" ) ) return "RG";
        if ( definition.Tags.Contains( "amulet" ) ) return "AM";
        if ( definition.Tags.Contains( "armor" ) || definition.Equipment?.Slot == EquipmentSlot.Head ) return "HD";
        if ( definition.Tags.Contains( "flask" ) || definition.Equipment?.Slot is EquipmentSlot.Flask1 or EquipmentSlot.Flask2 ) return "FL";
        if ( definition.Tags.Contains( "potion" ) ) return "PT";
        if ( definition.Tags.Contains( "healing" ) ) return "HP";
        if ( definition.Tags.Contains( "mana" ) ) return "MP";
        if ( definition.Tags.Contains( "ore" ) ) return "OR";

        return definition.Category switch
        {
            ItemCategory.Equipment => "EQ",
            ItemCategory.Consumable => "CO",
            ItemCategory.Material => "MA",
            _ => "IT"
        };
    }
}

public sealed class InventoryItemStats
{
    public int Attack { get; init; }
    public int Defense { get; init; }
    public int Speed { get; init; }
}
