using System.Collections.Generic;

namespace Sandbox.Code.Data;

public static class AffixPool
{
	public static List<AffixDef> All { get; } = new()
	{
		// ==========================================
		// ATTRIBUTES
		// ==========================================

		// Might (Flat) — weapon-only
		new AffixDef { Id = "might_t1", GroupId = "might_flat", StatName = "Might", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 10f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "might_t2", GroupId = "might_flat", StatName = "Might", Tier = 2, MinLevel = 15, MinValue = 3f, MaxValue = 5f, Weight = 6f,  RequiredTags = { "weapon" } },
		new AffixDef { Id = "might_t3", GroupId = "might_flat", StatName = "Might", Tier = 3, MinLevel = 45, MinValue = 6f, MaxValue = 9f, Weight = 2f,  RequiredTags = { "weapon" } },

		// Might (Percent) — weapon-only
		new AffixDef { Type = ModifierType.Percent, Id = "might_pct_t1", GroupId = "might_pct", StatName = "Might", Tier = 1, MinLevel = 10, MinValue = 5f,  MaxValue = 10f, Weight = 6f, RequiredTags = { "weapon" } },
		new AffixDef { Type = ModifierType.Percent, Id = "might_pct_t2", GroupId = "might_pct", StatName = "Might", Tier = 2, MinLevel = 30, MinValue = 11f, MaxValue = 18f, Weight = 3f, RequiredTags = { "weapon" } },
		new AffixDef { Type = ModifierType.Percent, Id = "might_pct_t3", GroupId = "might_pct", StatName = "Might", Tier = 3, MinLevel = 50, MinValue = 19f, MaxValue = 25f, Weight = 1f, RequiredTags = { "weapon" } },

		// Swiftness — universal (scales MoveSpeed & StaminaRegen)
		new AffixDef { Id = "swift_t1", GroupId = "swiftness_flat", StatName = "Swiftness", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 10f },
		new AffixDef { Id = "swift_t2", GroupId = "swiftness_flat", StatName = "Swiftness", Tier = 2, MinLevel = 15, MinValue = 3f, MaxValue = 5f, Weight = 6f },
		new AffixDef { Id = "swift_t3", GroupId = "swiftness_flat", StatName = "Swiftness", Tier = 3, MinLevel = 40, MinValue = 6f, MaxValue = 8f, Weight = 2f },

		// Endurance (Vitality) — universal (scales MaxHealth, MaxStamina, HealthRegen)
		new AffixDef { Id = "vit_t1", GroupId = "vitality_flat", StatName = "Endurance", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 10f },
		new AffixDef { Id = "vit_t2", GroupId = "vitality_flat", StatName = "Endurance", Tier = 2, MinLevel = 20, MinValue = 3f, MaxValue = 5f, Weight = 6f },
		new AffixDef { Id = "vit_t3", GroupId = "vitality_flat", StatName = "Endurance", Tier = 3, MinLevel = 40, MinValue = 6f, MaxValue = 8f, Weight = 2f },

		// Will — universal (scales magic resistance/mind)
		new AffixDef { Id = "will_t1", GroupId = "will_flat", StatName = "Will", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 8f },
		new AffixDef { Id = "will_t2", GroupId = "will_flat", StatName = "Will", Tier = 2, MinLevel = 18, MinValue = 3f, MaxValue = 5f, Weight = 5f },
		new AffixDef { Id = "will_t3", GroupId = "will_flat", StatName = "Will", Tier = 3, MinLevel = 40, MinValue = 6f, MaxValue = 8f, Weight = 2f },

		// Acuity — universal (scales EnergyRegen)
		new AffixDef { Id = "acuity_t1", GroupId = "acuity_flat", StatName = "Acuity", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 8f },
		new AffixDef { Id = "acuity_t2", GroupId = "acuity_flat", StatName = "Acuity", Tier = 2, MinLevel = 18, MinValue = 3f, MaxValue = 5f, Weight = 5f },
		new AffixDef { Id = "acuity_t3", GroupId = "acuity_flat", StatName = "Acuity", Tier = 3, MinLevel = 40, MinValue = 6f, MaxValue = 8f, Weight = 2f },

		// Wisdom — universal (scales MaxEnergy)
		new AffixDef { Id = "wis_t1", GroupId = "wisdom_flat", StatName = "Wisdom", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 2f, Weight = 8f },
		new AffixDef { Id = "wis_t2", GroupId = "wisdom_flat", StatName = "Wisdom", Tier = 2, MinLevel = 18, MinValue = 3f, MaxValue = 5f, Weight = 5f },
		new AffixDef { Id = "wis_t3", GroupId = "wisdom_flat", StatName = "Wisdom", Tier = 3, MinLevel = 40, MinValue = 6f, MaxValue = 8f, Weight = 2f },

		// ==========================================
		// RESOURCE POOLS
		// ==========================================

		// Max Health (Flat) — armor-only
		new AffixDef { Id = "max_hp_t1", GroupId = "max_hp_flat", StatName = "MaxHealth", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 25f, Weight = 10f, RequiredTags = { "armor" } },
		new AffixDef { Id = "max_hp_t2", GroupId = "max_hp_flat", StatName = "MaxHealth", Tier = 2, MinLevel = 15, MinValue = 30f, MaxValue = 60f, Weight = 6f,  RequiredTags = { "armor" } },
		new AffixDef { Id = "max_hp_t3", GroupId = "max_hp_flat", StatName = "MaxHealth", Tier = 3, MinLevel = 35, MinValue = 70f, MaxValue = 120f, Weight = 2f, RequiredTags = { "armor" } },

		// Max Stamina (Flat) — universal
		new AffixDef { Id = "max_stam_t1", GroupId = "max_stam_flat", StatName = "MaxStamina", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 20f, Weight = 8f },
		new AffixDef { Id = "max_stam_t2", GroupId = "max_stam_flat", StatName = "MaxStamina", Tier = 2, MinLevel = 20, MinValue = 25f, MaxValue = 45f, Weight = 5f },
		new AffixDef { Id = "max_stam_t3", GroupId = "max_stam_flat", StatName = "MaxStamina", Tier = 3, MinLevel = 40, MinValue = 50f, MaxValue = 80f, Weight = 2f },

		// Max Energy (Flat) — universal
		new AffixDef { Id = "max_energy_t1", GroupId = "max_energy_flat", StatName = "MaxEnergy", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 25f, Weight = 8f },
		new AffixDef { Id = "max_energy_t2", GroupId = "max_energy_flat", StatName = "MaxEnergy", Tier = 2, MinLevel = 18, MinValue = 30f, MaxValue = 55f, Weight = 5f },
		new AffixDef { Id = "max_energy_t3", GroupId = "max_energy_flat", StatName = "MaxEnergy", Tier = 3, MinLevel = 38, MinValue = 60f, MaxValue = 100f, Weight = 2f },

		// Max Stagger / Stagger Pool — armor-only
		new AffixDef { Id = "max_stagger_t1", GroupId = "max_stagger_flat", StatName = "MaxStagger", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 20f, Weight = 7f, RequiredTags = { "armor" } },
		new AffixDef { Id = "max_stagger_t2", GroupId = "max_stagger_flat", StatName = "MaxStagger", Tier = 2, MinLevel = 20, MinValue = 25f, MaxValue = 45f, Weight = 4f, RequiredTags = { "armor" } },
		new AffixDef { Id = "max_stagger_t3", GroupId = "max_stagger_flat", StatName = "MaxStagger", Tier = 3, MinLevel = 40, MinValue = 50f, MaxValue = 80f, Weight = 1f, RequiredTags = { "armor" } },

		// ==========================================
		// REGENERATION
		// ==========================================

		// Health Regen — armor-only
		new AffixDef { Id = "hp_regen_t1", GroupId = "hp_regen_flat", StatName = "HealthRegen", Tier = 1, MinLevel = 1,  MinValue = 0.5f, MaxValue = 1.0f, Weight = 7f, RequiredTags = { "armor" } },
		new AffixDef { Id = "hp_regen_t2", GroupId = "hp_regen_flat", StatName = "HealthRegen", Tier = 2, MinLevel = 20, MinValue = 1.2f, MaxValue = 2.5f, Weight = 4f, RequiredTags = { "armor" } },
		new AffixDef { Id = "hp_regen_t3", GroupId = "hp_regen_flat", StatName = "HealthRegen", Tier = 3, MinLevel = 40, MinValue = 3.0f, MaxValue = 5.0f, Weight = 2f, RequiredTags = { "armor" } },

		// Stamina Regen — universal
		new AffixDef { Id = "stam_regen_t1", GroupId = "stam_regen_flat", StatName = "StaminaRegen", Tier = 1, MinLevel = 1,  MinValue = 1f, MaxValue = 3f, Weight = 8f },
		new AffixDef { Id = "stam_regen_t2", GroupId = "stam_regen_flat", StatName = "StaminaRegen", Tier = 2, MinLevel = 18, MinValue = 4f, MaxValue = 7f, Weight = 5f },
		new AffixDef { Id = "stam_regen_t3", GroupId = "stam_regen_flat", StatName = "StaminaRegen", Tier = 3, MinLevel = 38, MinValue = 8f, MaxValue = 14f, Weight = 2f },

		// Energy Regen — universal
		new AffixDef { Id = "energy_regen_t1", GroupId = "energy_regen_flat", StatName = "EnergyRegen", Tier = 1, MinLevel = 1,  MinValue = 0.5f, MaxValue = 1.5f, Weight = 8f },
		new AffixDef { Id = "energy_regen_t2", GroupId = "energy_regen_flat", StatName = "EnergyRegen", Tier = 2, MinLevel = 18, MinValue = 2.0f, MaxValue = 4.0f, Weight = 5f },
		new AffixDef { Id = "energy_regen_t3", GroupId = "energy_regen_flat", StatName = "EnergyRegen", Tier = 3, MinLevel = 38, MinValue = 4.5f, MaxValue = 8.0f, Weight = 2f },

		// ==========================================
		// OFFENSIVE COMBAT
		// ==========================================

		// Damage Multiplier (%) — weapon-only
		new AffixDef { Type = ModifierType.Percent, Id = "dmg_mult_t1", GroupId = "dmg_mult_pct", StatName = "DamageMultiplier", Tier = 1, MinLevel = 5,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "weapon" } },
		new AffixDef { Type = ModifierType.Percent, Id = "dmg_mult_t2", GroupId = "dmg_mult_pct", StatName = "DamageMultiplier", Tier = 2, MinLevel = 22, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "weapon" } },
		new AffixDef { Type = ModifierType.Percent, Id = "dmg_mult_t3", GroupId = "dmg_mult_pct", StatName = "DamageMultiplier", Tier = 3, MinLevel = 45, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "weapon" } },

		// Crit Chance (%) — weapon-only
		new AffixDef { Id = "crit_t1", GroupId = "crit_chance", StatName = "CritChance", Tier = 1, MinLevel = 1,  MinValue = 2f, MaxValue = 4f,  Weight = 10f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "crit_t2", GroupId = "crit_chance", StatName = "CritChance", Tier = 2, MinLevel = 20, MinValue = 5f, MaxValue = 8f,  Weight = 6f,  RequiredTags = { "weapon" } },
		new AffixDef { Id = "crit_t3", GroupId = "crit_chance", StatName = "CritChance", Tier = 3, MinLevel = 45, MinValue = 9f, MaxValue = 14f, Weight = 2f,  RequiredTags = { "weapon" } },

		// Crit Damage (%) — weapon-only
		new AffixDef { Id = "crit_dmg_t1", GroupId = "crit_damage", StatName = "CritDamage", Tier = 1, MinLevel = 5,  MinValue = 10f, MaxValue = 20f, Weight = 8f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "crit_dmg_t2", GroupId = "crit_damage", StatName = "CritDamage", Tier = 2, MinLevel = 22, MinValue = 22f, MaxValue = 40f, Weight = 5f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "crit_dmg_t3", GroupId = "crit_damage", StatName = "CritDamage", Tier = 3, MinLevel = 45, MinValue = 45f, MaxValue = 75f, Weight = 2f, RequiredTags = { "weapon" } },

		// Attack Speed (%) — weapon-only
		new AffixDef { Id = "atk_speed_t1", GroupId = "atk_speed", StatName = "AttackSpeed", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "atk_speed_t2", GroupId = "atk_speed", StatName = "AttackSpeed", Tier = 2, MinLevel = 20, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "atk_speed_t3", GroupId = "atk_speed", StatName = "AttackSpeed", Tier = 3, MinLevel = 42, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "weapon" } },

		// Physical Force (Knockback power) — weapon-only
		new AffixDef { Id = "phys_force_t1", GroupId = "phys_force", StatName = "PhysicalForce", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 25f, Weight = 6f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "phys_force_t2", GroupId = "phys_force", StatName = "PhysicalForce", Tier = 2, MinLevel = 20, MinValue = 30f, MaxValue = 55f, Weight = 4f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "phys_force_t3", GroupId = "phys_force", StatName = "PhysicalForce", Tier = 3, MinLevel = 40, MinValue = 60f, MaxValue = 100f, Weight = 1f, RequiredTags = { "weapon" } },

		// Attack Range — weapon-only
		new AffixDef { Id = "range_t1", GroupId = "melee_range", StatName = "Range", Tier = 1, MinLevel = 1,  MinValue = 5f,  MaxValue = 12f, Weight = 6f, RequiredTags = { "weapon" } },
		new AffixDef { Id = "range_t2", GroupId = "melee_range", StatName = "Range", Tier = 2, MinLevel = 20, MinValue = 14f, MaxValue = 25f, Weight = 3f, RequiredTags = { "weapon" } },

		// ==========================================
		// DEFENSE & RESISTANCES
		// ==========================================

		// Armor (Flat) — armor-only
		new AffixDef { Id = "armor_t1", GroupId = "armor_flat", StatName = "Armor", Tier = 1, MinLevel = 1,  MinValue = 3f,  MaxValue = 6f,  Weight = 10f, RequiredTags = { "armor" } },
		new AffixDef { Id = "armor_t2", GroupId = "armor_flat", StatName = "Armor", Tier = 2, MinLevel = 15, MinValue = 7f,  MaxValue = 12f, Weight = 6f,  RequiredTags = { "armor" } },
		new AffixDef { Id = "armor_t3", GroupId = "armor_flat", StatName = "Armor", Tier = 3, MinLevel = 35, MinValue = 13f, MaxValue = 20f, Weight = 2f,  RequiredTags = { "armor" } },

		// Block Reduction (%) — armor-only
		new AffixDef { Id = "block_red_t1", GroupId = "block_red", StatName = "BlockReduction", Tier = 1, MinLevel = 1,  MinValue = 5f,  MaxValue = 10f, Weight = 7f, RequiredTags = { "armor" } },
		new AffixDef { Id = "block_red_t2", GroupId = "block_red", StatName = "BlockReduction", Tier = 2, MinLevel = 18, MinValue = 12f, MaxValue = 20f, Weight = 4f, RequiredTags = { "armor" } },
		new AffixDef { Id = "block_red_t3", GroupId = "block_red", StatName = "BlockReduction", Tier = 3, MinLevel = 38, MinValue = 22f, MaxValue = 35f, Weight = 1f, RequiredTags = { "armor" } },

		// Fire Resistance (%) — armor-only
		new AffixDef { Id = "res_fire_t1", GroupId = "res_fire", StatName = "ResistanceFire", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_fire_t2", GroupId = "res_fire", StatName = "ResistanceFire", Tier = 2, MinLevel = 15, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_fire_t3", GroupId = "res_fire", StatName = "ResistanceFire", Tier = 3, MinLevel = 35, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "armor" } },

		// Frost Resistance (%) — armor-only
		new AffixDef { Id = "res_frost_t1", GroupId = "res_frost", StatName = "ResistanceFrost", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_frost_t2", GroupId = "res_frost", StatName = "ResistanceFrost", Tier = 2, MinLevel = 15, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_frost_t3", GroupId = "res_frost", StatName = "ResistanceFrost", Tier = 3, MinLevel = 35, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "armor" } },

		// Air / Shock Resistance (%) — armor-only
		new AffixDef { Id = "res_air_t1", GroupId = "res_air", StatName = "ResistanceAir", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_air_t2", GroupId = "res_air", StatName = "ResistanceAir", Tier = 2, MinLevel = 15, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_air_t3", GroupId = "res_air", StatName = "ResistanceAir", Tier = 3, MinLevel = 35, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "armor" } },

		// Earth / Physical Resistance (%) — armor-only
		new AffixDef { Id = "res_earth_t1", GroupId = "res_earth", StatName = "ResistanceEarth", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_earth_t2", GroupId = "res_earth", StatName = "ResistanceEarth", Tier = 2, MinLevel = 15, MinValue = 9f,  MaxValue = 16f, Weight = 5f, RequiredTags = { "armor" } },
		new AffixDef { Id = "res_earth_t3", GroupId = "res_earth", StatName = "ResistanceEarth", Tier = 3, MinLevel = 35, MinValue = 18f, MaxValue = 28f, Weight = 2f, RequiredTags = { "armor" } },

		// Poise (Stagger threshold) — armor-only
		new AffixDef { Id = "poise_t1", GroupId = "poise_flat", StatName = "Poise", Tier = 1, MinLevel = 1,  MinValue = 3f,  MaxValue = 8f,  Weight = 7f, RequiredTags = { "armor" } },
		new AffixDef { Id = "poise_t2", GroupId = "poise_flat", StatName = "Poise", Tier = 2, MinLevel = 18, MinValue = 10f, MaxValue = 20f, Weight = 4f, RequiredTags = { "armor" } },
		new AffixDef { Id = "poise_t3", GroupId = "poise_flat", StatName = "Poise", Tier = 3, MinLevel = 38, MinValue = 22f, MaxValue = 35f, Weight = 1f, RequiredTags = { "armor" } },

		// Evasion (%) — universal
		new AffixDef { Id = "evasion_t1", GroupId = "evasion_flat", StatName = "Evasion", Tier = 1, MinLevel = 1,  MinValue = 2f, MaxValue = 5f,  Weight = 8f },
		new AffixDef { Id = "evasion_t2", GroupId = "evasion_flat", StatName = "Evasion", Tier = 2, MinLevel = 20, MinValue = 6f, MaxValue = 10f, Weight = 5f },
		new AffixDef { Id = "evasion_t3", GroupId = "evasion_flat", StatName = "Evasion", Tier = 3, MinLevel = 42, MinValue = 12f, MaxValue = 18f, Weight = 2f },

		// ==========================================
		// MOVEMENT & UTILITY
		// ==========================================

		// Move Speed — universal
		new AffixDef { Id = "movespeed_t1", GroupId = "move_speed", StatName = "MoveSpeed", Tier = 1, MinLevel = 1,  MinValue = 5f,  MaxValue = 10f, Weight = 8f },
		new AffixDef { Id = "movespeed_t2", GroupId = "move_speed", StatName = "MoveSpeed", Tier = 2, MinLevel = 25, MinValue = 11f, MaxValue = 18f, Weight = 4f },
		new AffixDef { Id = "movespeed_t3", GroupId = "move_speed", StatName = "MoveSpeed", Tier = 3, MinLevel = 45, MinValue = 20f, MaxValue = 30f, Weight = 1f },

		// Acceleration Speed — universal
		new AffixDef { Id = "accel_t1", GroupId = "accel_speed", StatName = "AccelerationSpeed", Tier = 1, MinLevel = 1,  MinValue = 10f, MaxValue = 20f, Weight = 6f },
		new AffixDef { Id = "accel_t2", GroupId = "accel_speed", StatName = "AccelerationSpeed", Tier = 2, MinLevel = 20, MinValue = 25f, MaxValue = 45f, Weight = 3f },

		// Jump Power — universal
		new AffixDef { Id = "jump_t1", GroupId = "jump_power", StatName = "JumpPower", Tier = 1, MinLevel = 1,  MinValue = 5f,  MaxValue = 12f, Weight = 6f },
		new AffixDef { Id = "jump_t2", GroupId = "jump_power", StatName = "JumpPower", Tier = 2, MinLevel = 20, MinValue = 15f, MaxValue = 25f, Weight = 3f },

		// Cast Speed (%) — universal
		new AffixDef { Id = "cast_speed_t1", GroupId = "cast_speed", StatName = "CastSpeed", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 8f,  Weight = 8f },
		new AffixDef { Id = "cast_speed_t2", GroupId = "cast_speed", StatName = "CastSpeed", Tier = 2, MinLevel = 20, MinValue = 9f,  MaxValue = 16f, Weight = 5f },
		new AffixDef { Id = "cast_speed_t3", GroupId = "cast_speed", StatName = "CastSpeed", Tier = 3, MinLevel = 42, MinValue = 18f, MaxValue = 28f, Weight = 2f },

		// Effect Duration (%) — universal
		new AffixDef { Id = "eff_dur_t1", GroupId = "effect_duration", StatName = "EffectDuration", Tier = 1, MinLevel = 1,  MinValue = 5f,  MaxValue = 12f, Weight = 7f },
		new AffixDef { Id = "eff_dur_t2", GroupId = "effect_duration", StatName = "EffectDuration", Tier = 2, MinLevel = 20, MinValue = 15f, MaxValue = 25f, Weight = 4f },
		new AffixDef { Id = "eff_dur_t3", GroupId = "effect_duration", StatName = "EffectDuration", Tier = 3, MinLevel = 42, MinValue = 28f, MaxValue = 45f, Weight = 1f },

		// Effect Potency (%) — universal
		new AffixDef { Id = "eff_pot_t1", GroupId = "effect_potency", StatName = "EffectPotency", Tier = 1, MinLevel = 1,  MinValue = 4f,  MaxValue = 10f, Weight = 7f },
		new AffixDef { Id = "eff_pot_t2", GroupId = "effect_potency", StatName = "EffectPotency", Tier = 2, MinLevel = 20, MinValue = 12f, MaxValue = 22f, Weight = 4f },
		new AffixDef { Id = "eff_pot_t3", GroupId = "effect_potency", StatName = "EffectPotency", Tier = 3, MinLevel = 42, MinValue = 25f, MaxValue = 40f, Weight = 1f },
	};
}
