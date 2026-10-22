using System;
using System.Collections.Generic;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public enum ProcTarget { Owner, Other }

internal static class ProcEffectUtil
{
	/// <summary>The targeted actor if it is valid and not dead, else null.</summary>
	public static Actor ResolveAlive( in ProcContext ctx, ProcTarget target )
	{
		var actor = target == ProcTarget.Owner ? ctx.Owner : ctx.Event.Other;
		if ( !actor.IsValid() ) return null;
		if ( actor.StateComp?.CurrentState == ActorStateType.Dead ) return null;
		return actor;
	}
}

/// <summary>Debug effect: logs when its proc fires. Handy for testing chance/ICD/conditions.</summary>
public sealed class LogProcEffect : IProcEffect
{
	public string Message = "fired";

	public void Execute( in ProcContext ctx )
	{
		Log.Info( $"[Proc] {ctx.Owner?.GameObject?.Name} / '{ctx.Def.Id}' {Message} " +
		          $"(trigger={ctx.Event.Trigger}, other={ctx.Event.Other?.GameObject?.Name ?? "none"}, " +
		          $"amount={ctx.Event.Amount:F1}, crit={ctx.Event.IsCrit}, depth={ctx.Event.ProcDepth}, potency={ctx.Potency:F2})" );
	}
}

/// <summary>
/// Wraps another effect with its own roll, so one proc can bundle a guaranteed effect and a rarer one.
/// (ProcDef.Chance gates the whole proc; this gates just the inner effect.)
/// </summary>
public sealed class ChanceProcEffect : IProcEffect
{
	/// <summary>Percent.</summary>
	public float Chance = 100f;
	public IProcEffect Inner;

	public void Execute( in ProcContext ctx )
	{
		if ( Inner == null ) return;
		if ( Chance < 100f && Random.Shared.NextSingle() * 100f >= Chance ) return;
		Inner.Execute( ctx );
	}
}

/// <summary>
/// Runs another effect after a delay, using the owner's ProcControl as the scheduler (it keeps ticking while the
/// owner is dead, until the corpse is destroyed). Compose several of these for sequences (flash, flash, boom).
/// </summary>
public sealed class DelayedProcEffect : IProcEffect
{
	public float Delay = 1f;
	public IProcEffect Inner;

	public void Execute( in ProcContext ctx )
	{
		if ( Inner == null ) return;
		ctx.Owner?.Procs?.Schedule( Delay, Inner, ctx );
	}
}

/// <summary>Sets the owner's body tint. Used by FlashProcEffect for its on/off steps.</summary>
public sealed class SetTintProcEffect : IProcEffect
{
	public Color Tint = Color.White;

	public void Execute( in ProcContext ctx )
	{
		if ( !ctx.Owner.IsValid() ) return;
		var renderer = ctx.Owner.Components.GetInChildren<SkinnedModelRenderer>();
		if ( renderer.IsValid() ) renderer.Tint = Tint;
	}
}

/// <summary>
/// Flickers the owner's body tint Count times (telegraph, e.g. before a death explosion), then restores
/// whatever tint it had when the effect started (so elite tints survive).
/// </summary>
public sealed class FlashProcEffect : IProcEffect
{
	/// <summary>Values above 1 may be clamped by the renderer; it should still read as a hot flash.</summary>
	public Color FlashColor = new Color( 3f, 0.6f, 0.3f );
	public int Count = 3;
	public float Interval = 0.35f;
	public float OnTime = 0.15f;

	public void Execute( in ProcContext ctx )
	{
		var owner = ctx.Owner;
		if ( !owner.IsValid() || owner.Procs == null ) return;

		var renderer = owner.Components.GetInChildren<SkinnedModelRenderer>();
		if ( !renderer.IsValid() ) return;

		var restore = renderer.Tint;
		for ( int i = 0; i < Count; i++ )
		{
			owner.Procs.Schedule( i * Interval, new SetTintProcEffect { Tint = FlashColor }, ctx );
			owner.Procs.Schedule( i * Interval + OnTime, new SetTintProcEffect { Tint = restore }, ctx );
		}
	}
}

/// <summary>
/// Applies a BuffDef. Modifier values are multiplied by Potency (boon stacks etc.), like SpellEffectApplier.
/// RefreshExisting = true replaces a running copy (safe default); false stacks an independent copy per fire —
/// BuffComponent has no stack cap, so only use it with an ICD or a short duration.
/// </summary>
public sealed class BuffProcEffect : IProcEffect
{
	public BuffDef Buff;
	public ProcTarget Target = ProcTarget.Owner;
	public bool RefreshExisting = true;

	public void Execute( in ProcContext ctx )
	{
		if ( Buff == null ) return;
		var actor = ProcEffectUtil.ResolveAlive( ctx, Target );
		if ( actor?.Buffs == null || actor.StatSheet == null ) return;

		var buff = Buff.Clone();
		foreach ( var modifier in buff.Modifiers )
			modifier.Value *= ctx.Potency;

		actor.Buffs.ApplyBuff( buff, actor.StatSheet, RefreshExisting );
	}
}

/// <summary>
/// Restores resources. HealthPctOfEventAmount turns it into leech: percent of the event's damage Amount
/// (use with HitDealt/Crit/Kill). Everything is multiplied by Potency and clamped to max.
/// </summary>
public sealed class RestoreProcEffect : IProcEffect
{
	public ProcTarget Target = ProcTarget.Owner;
	public float Health;
	public float Stamina;
	public float Energy;
	public float HealthPctOfEventAmount;
	/// <summary>Player only; ignored for mobs.</summary>
	public int FlaskCharges;

	public void Execute( in ProcContext ctx )
	{
		var actor = ProcEffectUtil.ResolveAlive( ctx, Target );
		var sheet = actor?.StatSheet;
		if ( sheet?.MaxHealth == null ) return;

		float potency = ctx.Potency;

		float health = (Health + ctx.Event.Amount * HealthPctOfEventAmount / 100f) * potency;
		if ( health > 0f )
			sheet.CurrentHealth = MathF.Min( sheet.MaxHealth.Value, sheet.CurrentHealth + health );

		if ( Stamina > 0f )
			sheet.CurrentStamina = MathF.Min( sheet.MaxStamina.Value, sheet.CurrentStamina + Stamina * potency );

		if ( Energy > 0f )
			sheet.CurrentEnergy = MathF.Min( sheet.MaxEnergy.Value, sheet.CurrentEnergy + Energy * potency );

		if ( FlaskCharges > 0 && actor is Player )
			Presentation.UI.LocalInventory?.RefillFlasks( FlaskCharges );
	}
}

/// <summary>
/// Casts a rune sequence for free (no energy cost, no cast time, no recharge) as the proc owner.
/// The resulting damage is stamped ProcDepth + 1, so ProcDef.MaxProcDepth decides whether it may chain.
/// Potency scales the cast's damage multiplier.
/// </summary>
public sealed class CastRunesProcEffect : IProcEffect
{
	public List<RuneDef> Runes = new();
	/// <summary>Added to the cast origin (owner position, or event position if FromEventPosition).</summary>
	public Vector3 OriginOffset = Vector3.Up * 40f;
	/// <summary>false = cast from the owner (volatile death nova), true = from where the event happened (retaliation at the victim).</summary>
	public bool FromEventPosition = false;
	/// <summary>Aim at ctx.Event.Other when there is one that isn't at the cast origin; else owner's forward.</summary>
	public bool AimAtOther = true;
	/// <summary>Scales the cast's AoE radius and beam/cone range (1 = as authored on the rune). Nova 750 x 0.35 is about 260.</summary>
	public float RangeScale = 1f;

	public void Execute( in ProcContext ctx )
	{
		var owner = ctx.Owner;
		if ( !owner.IsValid() || Runes == null || Runes.Count == 0 ) return;

		var basePosition = FromEventPosition ? ctx.Event.Position : owner.GameObject.WorldPosition;
		var origin = basePosition + OriginOffset;

		var aim = owner.GameObject.WorldRotation.Forward;
		if ( AimAtOther && ctx.Event.Other.IsValid() )
		{
			var toOther = ctx.Event.Other.GameObject.WorldPosition + Vector3.Up * 40f - origin;
			if ( toOther.LengthSquared > 1f ) aim = toOther.Normal;
		}

		var result = RuneEvaluator.EvaluateSequence( Runes, owner.GameObject, origin, aim,
			procDepth: ctx.Event.ProcDepth + 1 );
		if ( !result.Success ) return;

		for ( int i = 0; i < result.Payloads.Count; i++ )
		{
			var payload = result.Payloads[i]; // struct copy: modify the copy, then deliver it
			payload.AoERadius *= RangeScale;
			payload.BeamRange *= RangeScale;
			payload.Context.DamageMultiplier *= ctx.Potency;
			RuneEvaluator.ExecuteDelivery( payload );
		}
	}
	/// <summary>Grants skill XP: Flat + PerAmount x event Amount (damage dealt/taken, energy spent), times Potency.</summary>
	public sealed class SkillXpProcEffect : IProcEffect
	{
		public string SkillId;
		public float Flat;
		public float PerAmount;

		public void Execute( in ProcContext ctx )
		{
			var skills = ctx.Owner?.Components.Get<SkillControl>();
			skills?.GainXp( SkillId, (Flat + ctx.Event.Amount * PerAmount) * ctx.Potency );
		}
	}
}
