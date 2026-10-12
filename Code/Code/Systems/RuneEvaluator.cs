using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public class RuneEvaluationResult
{
	public SpellContext ConsolidatedContext;
	public List<SpellPayload> Payloads = new();
	public bool Success;
	public string ErrorMessage;
}

public static class RuneEvaluator
{
	public static RuneEvaluationResult EvaluateSequence( List<RuneDef> runes, GameObject caster, Vector3 origin, Vector3 aimDir, Vector3? targetPoint = null, int currentDepth = 0, float charge01 = 0f )
	{
		var result = new RuneEvaluationResult();
		if ( runes == null || runes.Count == 0 )
		{
			result.Success = false;
			result.ErrorMessage = "Empty rune sequence.";
			return result;
		}

		if ( currentDepth > SpellContext.MaxRecursionDepth )
		{
			result.Success = false;
			result.ErrorMessage = "Max spell recursion depth reached.";
			return result;
		}

		var ctx = new SpellContext
		{
			Caster = caster,
			Origin = origin,
			AimDirection = aimDir,
			TargetPoint = targetPoint,
			RecursionDepth = currentDepth
		};

		var statSheet = caster?.Components.GetInAncestorsOrSelf<Actor>()?.StatSheet;
		float will = statSheet?.Will.Value ?? 0f;
		float acuity = statSheet?.Acuity.Value ?? 0f;
		float wisdom = statSheet?.Wisdom.Value ?? 0f;

		// Gear bonuses (Projectile Pierce / Additional Projectiles) belong to the cast itself. Trigger
		// sub-spells (depth > 0) don't inherit them, or a cluster bomb's children would multiply too.
		if ( currentDepth == 0 && statSheet != null )
		{
			ctx.BonusPierce += Math.Max( 0, (int)MathF.Round( statSheet.ProjectilePierce?.Value ?? 0f ) );
			ctx.BonusProjectiles += Math.Max( 0, (int)MathF.Round( statSheet.ProjectileCount?.Value ?? 0f ) );
		}

		int index = 0;
		bool hasMethod = false;

		while ( index < runes.Count )
		{
			var rune = runes[index];
			if ( rune == null )
			{
				index++;
				continue;
			}

			// Aggregate costs & cast delay (Method delay scales with Acuity)
			ctx.TotalEnergyCost += rune.EnergyCost;
			ctx.TotalHealthCost += rune.HealthCost;
			ctx.TotalStaminaCost += rune.StaminaCost;

			float castDelay = rune.CastDelay;
			if ( rune.Category == RuneCategory.Method )
			{
				float acuityScale = rune.Scaling?.AcuityToCastSpeed ?? 0f;
				castDelay /= 1f + acuity * acuityScale / 100f;
			}
			ctx.TotalCastDelay += castDelay;

			switch ( rune.Category )
			{
				case RuneCategory.Modifier:
					rune.ModifierEffect?.Invoke( ctx );
					break;

				case RuneCategory.Multicast:
					ctx.MulticastCount = Math.Max( ctx.MulticastCount, rune.MulticastDrawCount );
					ctx.MulticastDelay = Math.Max( ctx.MulticastDelay, rune.MulticastDelay );
					break;

				case RuneCategory.Trigger:
					if ( rune.TriggerNestedRunes != null && rune.TriggerNestedRunes.Count > 0 )
					{
						ctx.TriggerPayloadRunes.AddRange( rune.TriggerNestedRunes );
					}
					break;

				case RuneCategory.Force:
					ctx.AccumulatedDamage.HealthDamage += rune.BasePower
						+ will * (rune.Scaling?.WillToPower ?? 0f);
					ctx.AccumulatedDamage.StaminaDamage += rune.StaminaDamage;
					ctx.AccumulatedDamage.KnockbackForce += rune.KnockbackForce;
					if ( rune.ElementTag.HasValue ) ctx.ElementTags.Add( rune.ElementTag.Value );
					ctx.PrimaryElement ??= rune.ElementTag;
					ctx.VisualMaterial ??= rune.VisualMaterial;
					foreach ( var tag in rune.SpellTags ) ctx.AttackTags.Add( tag );
					break;

				case RuneCategory.Method:
					hasMethod = true;
					var baseEffect = ResolveEffect( rune, ctx.PrimaryElement );
					if ( baseEffect != null )
					{
						var effect = baseEffect.Clone();
						var scaling = rune.Scaling;
						float potency = 1f + MathF.Max( 0f, will * (scaling?.WillToEffectPotency ?? 0f) / 100f );
						potency *= (statSheet?.EffectPotency.Value ?? 100f) / 100f;
						effect.PotencyMultiplier *= potency;
						effect.Strength *= potency;
						float durationScale = 1f + MathF.Max( 0f, wisdom * (scaling?.WisdomToDuration ?? 0f) / 100f );
						durationScale *= (statSheet?.EffectDuration.Value ?? 100f) / 100f;
						effect.Duration *= durationScale;
						if ( effect.Buff != null ) effect.Buff.Duration = effect.Duration;
						ctx.Effects.Add( effect );
					}
					float rangeScale = 1f + MathF.Max( 0f, acuity * (rune.Scaling?.AcuityToRange ?? 0f) / 100f );
					int payloadStart = result.Payloads.Count;
					CreatePayloadsForMethod( ctx, rune, result.Payloads );
					for ( int payloadIndex = payloadStart; payloadIndex < result.Payloads.Count; payloadIndex++ )
					{
						var payload = result.Payloads[payloadIndex];
						payload.BeamRange *= rangeScale;
						payload.AoERadius *= rangeScale;
						result.Payloads[payloadIndex] = payload;
					}
					break;
			}

			index++;
		}

		// Method payload contexts were cloned during the rune walk, so resolve charge onto each
		// clone as well as the consolidated cost context before any fallback payload is created.
		var chargeScaling = ResolveChargeScaling( runes );
		ctx.ApplyCharge( charge01, chargeScaling, will );
		foreach ( var payload in result.Payloads )
			payload.Context.ApplyCharge( charge01, chargeScaling, will );

		// Evoke Force Alone (Fallback delivery when Force is present without a Method)
		if ( !hasMethod && ctx.AccumulatedDamage.HealthDamage > 0 )
		{
			var fallbackPayload = new SpellPayload
			{
				Context = ctx.Clone(),
				DeliveryType = RuneDeliveryType.Nova,
				AoERadius = 120f,
				PrefabPath = SpellVisualResolver.ResolvePrefab( ctx.PrimaryElement, SpellShape.Nova )
			};
			result.Payloads.Add( fallbackPayload );
		}

		result.ConsolidatedContext = ctx;
		result.Success = result.Payloads.Count > 0;
		return result;
	}

	/// <summary>Uses the first method rune with explicit charge scaling, or the shared default.</summary>
	public static ChargeScalingDef ResolveChargeScaling( List<RuneDef> runes )
	{
		if ( runes != null )
		{
			foreach ( var rune in runes )
			{
				if ( rune?.Category == RuneCategory.Method && rune.ChargeScaling != null )
					return rune.ChargeScaling;
			}
		}

		return ChargeScalingDef.Default;
	}

	/// <summary>The Method rune's element-specific effect if the Force rune picked an element it defines one for.</summary>
	private static SpellEffect ResolveEffect( RuneDef rune, RuneElementTag? element )
	{
		if ( element.HasValue && rune.ElementEffects != null
			&& rune.ElementEffects.TryGetValue( element.Value, out var variant ) && variant != null )
			return variant;
		return rune.Effect;
	}

	private static void CreatePayloadsForMethod( SpellContext ctx, RuneDef methodRune, List<SpellPayload> outPayloads )
	{
		// Each multicast draw is a volley: 1 + BonusProjectiles fanned around the aim. Only shapes that
		// actually travel along an aim get extra projectiles; a beam or nova has nothing to fan.
		int draws = Math.Max( 1, ctx.MulticastCount );
		bool canFan = methodRune.DeliveryType is RuneDeliveryType.Projectile or RuneDeliveryType.Blast;
		int volley = 1 + (canFan ? Math.Max( 0, ctx.BonusProjectiles ) : 0);

		for ( int draw = 0; draw < draws; draw++ )
		{
			// The stagger is per draw, so a whole volley leaves together and the next draw follows it.
			float delay = draw * MathF.Max( 0f, ctx.MulticastDelay );

			for ( int shot = 0; shot < volley; shot++ )
			{
				var payloadCtx = ctx.Clone();
				if ( volley > 1 && payloadCtx.AimDirection.LengthSquared > 0.001f )
				{
					var fanned = Rotation.LookAt( payloadCtx.AimDirection.Normal ) * Rotation.From( 0f, ProjectileTuning.FanYaw( shot, volley ), 0f );
					payloadCtx.AimDirection = fanned.Forward;
				}

				var payload = new SpellPayload
				{
					Context = payloadCtx,
					DeliveryType = methodRune.DeliveryType,
					ProjectileTemplate = methodRune.ProjectileTemplate,
					PrefabPath = SpellVisualResolver.ResolvePrefab( payloadCtx.PrimaryElement, SpellVisualResolver.ShapeOf( methodRune.DeliveryType ), methodRune ),
					BeamRange = methodRune.Range,
					BeamVisualLength = methodRune.BeamVisualLength,
					BeamRadius = methodRune.BeamRadius,
					AoERadius = methodRune.AoERadius,
					ConeAngle = methodRune.ConeAngle,
					ConeRequiresLineOfSight = methodRune.ConeRequiresLineOfSight,
					DeliveryDelay = delay
				};
				outPayloads.Add( payload );
			}
		}
	}

	public static void ExecuteTriggerPayload( SpellContext parentContext, Vector3 triggerOrigin, Vector3 triggerNormal, GameObject hitTarget )
	{
		if ( parentContext == null || parentContext.TriggerPayloadRunes == null || parentContext.TriggerPayloadRunes.Count == 0 )
			return;

		if ( parentContext.RecursionDepth >= SpellContext.MaxRecursionDepth )
			return;

		var aimDir = triggerNormal.LengthSquared > 0.001f ? triggerNormal : parentContext.AimDirection;
		var evalResult = EvaluateSequence(
			parentContext.TriggerPayloadRunes,
			parentContext.Caster,
			triggerOrigin,
			aimDir,
			triggerOrigin + aimDir * 100f,
			parentContext.RecursionDepth + 1,
			parentContext.Charge01
		);

		if ( evalResult.Success )
		{
			foreach ( var payload in evalResult.Payloads )
			{
				Dispatch( payload );
			}
		}
	}

	/// <summary>
	/// Delivers a payload now, or hands it to the caster's SpellComponent when it has a DeliveryDelay.
	/// A payload with no scheduler to wait on is delivered immediately rather than lost.
	/// </summary>
	public static void Dispatch( SpellPayload payload )
	{
		if ( payload.DeliveryDelay > 0.001f )
		{
			var scheduler = payload.Context?.Caster?.Components.GetInAncestorsOrSelf<SpellComponent>();
			if ( scheduler != null )
			{
				scheduler.QueueDelivery( payload );
				return;
			}
		}
		ExecuteDelivery( payload );
	}

	public static void ExecuteDelivery( SpellPayload payload )
	{
		ISpellDeliveryMethod deliveryMethod = payload.DeliveryType switch
		{
			RuneDeliveryType.Projectile => new ProjectileDeliveryMethod(),
			RuneDeliveryType.Blast => new BlastDeliveryMethod(),
			RuneDeliveryType.Beam => new BeamDeliveryMethod(),
			RuneDeliveryType.Nova => new NovaDeliveryMethod(),
			RuneDeliveryType.Self => new SelfDeliveryMethod(),
			RuneDeliveryType.Cone => new ConeDeliveryMethod(),
			RuneDeliveryType.Imbue => new ImbueDeliveryMethod(),
			RuneDeliveryType.AoE => null,
			_ => null
		};
		if ( deliveryMethod == null )
		{
			Log.Warning( $"[RuneEvaluator] Unsupported delivery type '{payload.DeliveryType}'." );
			return;
		}
		deliveryMethod.Deliver( payload );
		
	}
}
