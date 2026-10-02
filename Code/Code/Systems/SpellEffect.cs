using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public enum SpellEffectType
{
	Buff,
	Impulse
}

public enum SpellImpulseDirection
{
	Aim,
	WorldUp,
	AwayFromOrigin
}

/// <summary>Resolved per-cast effect data. Definitions are copied before runtime scaling is applied.</summary>
public class SpellEffect
{
	public SpellEffectType Type;
	public BuffDef Buff;
	public float Strength;
	public float Duration;
	public SpellImpulseDirection Direction;
	public float PotencyMultiplier = 1f;

	public SpellEffect Clone() => new()
	{
		Type = Type,
		Buff = Buff?.Clone(),
		Strength = Strength,
		Duration = Duration,
		Direction = Direction,
		PotencyMultiplier = PotencyMultiplier
	};
}

public static class SpellEffectApplier
{
	public static void Apply( SpellContext context, GameObject target, Vector3 hitOrigin )
	{
		if ( context == null || !target.IsValid() ) return;

		var actor = target.Components.GetInAncestorsOrSelf<Actor>();
		if ( actor == null ) return;

		foreach ( var effect in context.Effects )
		{
			if ( effect == null ) continue;

			switch ( effect.Type )
			{
				case SpellEffectType.Buff:
					if ( effect.Buff == null || actor.Buffs == null || actor.StatSheet == null ) break;
					var buff = effect.Buff.Clone();
					buff.Duration = effect.Duration;
					foreach ( var modifier in buff.Modifiers )
						modifier.Value *= effect.PotencyMultiplier;
					actor.Buffs.ApplyBuff( buff, actor.StatSheet, refreshExisting: true );
					break;

				case SpellEffectType.Impulse:
					var direction = effect.Direction switch
					{
						SpellImpulseDirection.WorldUp => Vector3.Up,
						SpellImpulseDirection.AwayFromOrigin => target.WorldPosition - hitOrigin,
						_ => context.AimDirection
					};
					if ( direction.LengthSquared <= 0.001f ) direction = Vector3.Up;
					direction = direction.Normal;
					CombatMath.ApplyKnockback( target, direction, effect.Strength, addUpwardBias: false, preventGrounding: true );
					break;
			}
		}
	}
}
