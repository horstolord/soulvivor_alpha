using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public struct SpellPayload
{
	public SpellContext Context;
	public RuneDeliveryType DeliveryType;
	public ProjectileTemplate ProjectileTemplate;
	public string ProjectilePrefabPath;
	public float BeamRange;
	public float BeamVisualLength;
	public float AoERadius;
	public float ConeAngle;
	public bool ConeRequiresLineOfSight;
}

public interface ISpellDeliveryMethod
{
	void Deliver( SpellPayload payload );
}

public class ProjectileDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;

		var template = payload.ProjectileTemplate?.Clone() ?? new ProjectileTemplate();
		template.Speed *= ctx.SpeedMultiplier;
		template.PierceCount += ctx.BonusPierce;

		var rot = Rotation.LookAt( ctx.AimDirection );
		if ( ctx.SpreadAngle > 0.01f )
		{
			var yawOffset = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			var pitchOffset = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			rot *= Rotation.From( pitchOffset, yawOffset, 0 );
		}

		var spawnTransform = new Transform( ctx.Origin, rot );

		var prefabPath = payload.ProjectilePrefabPath;
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
		{
			Log.Warning( "[ProjectileDelivery] Method rune has no ProjectilePrefabPath set." );
			return;
		}

		if ( !ResourceLibrary.TryGet<PrefabFile>( prefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[ProjectileDelivery] Could not find '{prefabPath}' in ResourceLibrary!" );
			return;
		}

		// Clone directly into the scene root (Parent = null) so it moves independently of the Caster
		// Clone DISABLED — CloneConfig.Transform's rotation isn't reliable through
// SceneUtility.GetPrefabScene(...).Clone() while the object is already live,
// so we set it explicitly before waking it up (same pattern as SpawnDirector).
		var config = new CloneConfig
		{
			Transform = spawnTransform,
			Parent = null,
			StartEnabled = false
		};

		var projGo = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
		projGo.Name = "SpellProjectile";
		projGo.WorldPosition = ctx.Origin;
		projGo.WorldRotation = rot;

// NOTE: object is still disabled here — must use EverythingInSelfAndDescendants,
// not EnabledInSelfAndDescendants, or this returns null (SpawnDirector hits the
// same requirement for the same reason).
		var projComp = projGo.Components.Get<Projectile>( FindMode.EverythingInSelfAndDescendants );
		if ( projComp == null )
		{
			Log.Warning( $"[ProjectileDelivery] '{prefabPath}' has no Projectile component — check the prefab." );
			projGo.Destroy();
			return;
		}

		var damageDef = new DamageProfileDef
		{
			HealthDamage = ctx.AccumulatedDamage.HealthDamage * ctx.DamageMultiplier,
			StaminaDamage = ctx.AccumulatedDamage.StaminaDamage,
			KnockbackForce = ctx.AccumulatedDamage.KnockbackForce,
			Tags = ctx.AttackTags
		};
		var casterSheet = ctx.Caster.Components.GetInAncestorsOrSelf<Actor>()?.StatSheet;
		damageDef = CombatMath.RollCrit( casterSheet, damageDef );

		projComp.Template = template;
		projComp.Payload = new ProjectilePayload
		{
			Caster = ctx.Caster, Damage = damageDef, AttackTags = ctx.AttackTags, SourceContext = ctx
		};

		if ( ctx.VisualMaterial != null )
		{
			var renderer = projGo.Components.GetInChildren<ModelRenderer>( true ); // include disabled
			if ( renderer != null )
				renderer.MaterialOverride = ctx.VisualMaterial;
		}

// Everything is configured — wake it up last.
		projGo.Enabled = true;
	}
}

public class BeamDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;
		var spawnTransform = new Transform(ctx.Origin, Rotation.LookAt(ctx.AimDirection));
		var range = payload.BeamRange > 0 ? payload.BeamRange : 3000f;
		var endPos = ctx.Origin + ctx.AimDirection * range;
		var config = new CloneConfig( spawnTransform , ctx.Caster, false );
		var tr = ctx.Caster.Scene.Trace
			.Ray( ctx.Origin, endPos )
			.IgnoreGameObjectHierarchy( ctx.Caster )
			.Run();

		var targetPos = tr.Hit ? tr.EndPosition : endPos;

		var damageDef = new DamageProfileDef
		{
			HealthDamage = ctx.AccumulatedDamage.HealthDamage * ctx.DamageMultiplier,
			StaminaDamage = ctx.AccumulatedDamage.StaminaDamage,
			KnockbackForce = ctx.AccumulatedDamage.KnockbackForce,
			Tags = ctx.AttackTags
		};
		var casterSheet = ctx.Caster.Components.GetInAncestorsOrSelf<Actor>()?.StatSheet;
		damageDef = CombatMath.RollCrit( casterSheet, damageDef );
		// ResourceLibrary.Get throws if not found; TryGet returns false silently.
		if ( ResourceLibrary.TryGet<PrefabFile>( "beamblue.prefab", out var prefabFile ) )
		{
			var beamInstance = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
			beamInstance.WorldPosition = ctx.Origin;
			

			float hitDistance = (targetPos - ctx.Origin).Length;
			float refLength = payload.BeamVisualLength > 0f ? payload.BeamVisualLength : 100f;
			float scale = MathF.Max( 0.01f, hitDistance / refLength );
			beamInstance.LocalScale = beamInstance.LocalScale.WithX( scale );

			beamInstance.Enabled = true;
		}
		else
		{
			Log.Warning( $"[BeamDelivery] Could not find beamblue.prefab in ResourceLibrary!" );
		}

		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			var actor = tr.GameObject.Components.GetInAncestorsOrSelf<Actor>();
			actor?.ApplyDamage( damageDef );
			CombatMath.ApplyKnockback( tr.GameObject, ctx.AimDirection, damageDef.KnockbackForce );
			SpellEffectApplier.Apply( ctx, tr.GameObject, ctx.Origin );
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
		{
			RuneEvaluator.ExecuteTriggerPayload( ctx, targetPos, tr.Normal, tr.GameObject );
		}
	}
}

public class SelfDeliveryMethod : ISpellDeliveryMethod
	{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;
		SpellEffectApplier.Apply( ctx, ctx.Caster, ctx.Origin );
		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
			RuneEvaluator.ExecuteTriggerPayload( ctx, ctx.Origin, Vector3.Up, ctx.Caster );
	}
}

public class NovaDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;
		var radius = payload.AoERadius > 0 ? payload.AoERadius : 150f;
		var hits = ctx.Caster.Scene.Trace
			.Sphere( radius, ctx.Origin, ctx.Origin )
			.IgnoreGameObjectHierarchy( ctx.Caster )
			.RunAll();

		var damageDef = new DamageProfileDef
		{
			HealthDamage = ctx.AccumulatedDamage.HealthDamage * ctx.DamageMultiplier,
			StaminaDamage = ctx.AccumulatedDamage.StaminaDamage,
			KnockbackForce = ctx.AccumulatedDamage.KnockbackForce,
			Tags = ctx.AttackTags
		};
		var casterSheet = ctx.Caster.Components.GetInAncestorsOrSelf<Actor>()?.StatSheet;
		damageDef = CombatMath.RollCrit( casterSheet, damageDef );

		var alreadyHit = new HashSet<Actor>();
		foreach ( var hit in hits )
		{
			if ( hit.GameObject.IsValid() )
			{
				var actor = hit.GameObject.Components.GetInAncestorsOrSelf<Actor>();
				if ( actor == null || !alreadyHit.Add( actor ) ) continue;
				actor.ApplyDamage( damageDef );

				var radialDirection = hit.GameObject.WorldPosition - ctx.Origin;
				CombatMath.ApplyKnockback( hit.GameObject, radialDirection, damageDef.KnockbackForce );
				SpellEffectApplier.Apply( ctx, hit.GameObject, ctx.Origin );
			}
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
		{
			RuneEvaluator.ExecuteTriggerPayload( ctx, ctx.Origin, Vector3.Up, null );
		}
	}
}

public class ConeDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;
		float range = payload.BeamRange > 0f ? payload.BeamRange : 300f;
		float halfAngle = Math.Clamp( payload.ConeAngle, 1f, 179f ) * 0.5f;
		float minDot = MathF.Cos( halfAngle * MathF.PI / 180f );
		var aim = ctx.AimDirection.LengthSquared > 0.001f ? ctx.AimDirection.Normal : ctx.Caster.WorldRotation.Forward;
		var candidates = ctx.Caster.Scene.Trace.Sphere( range, ctx.Origin, ctx.Origin )
			.IgnoreGameObjectHierarchy( ctx.Caster ).RunAll();
		var damage = BuildDamage( ctx );
		var alreadyHit = new HashSet<Actor>();

		foreach ( var candidate in candidates )
		{
			if ( !candidate.GameObject.IsValid() ) continue;
			var actor = candidate.GameObject.Components.GetInAncestorsOrSelf<Actor>();
			if ( actor == null || alreadyHit.Contains( actor ) ) continue;

			var toTarget = actor.GameObject.WorldPosition - ctx.Origin;
			float distance = toTarget.Length;
			if ( distance > range || distance <= 0.001f || Vector3.Dot( toTarget / distance, aim ) < minDot ) continue;

			if ( payload.ConeRequiresLineOfSight )
			{
				var sight = ctx.Caster.Scene.Trace.Ray( ctx.Origin, actor.GameObject.WorldPosition )
					.IgnoreGameObjectHierarchy( ctx.Caster ).Run();
				if ( sight.Hit && (!sight.GameObject.IsValid() || sight.GameObject.Components.GetInAncestorsOrSelf<Actor>() != actor) ) continue;
			}
			if ( !alreadyHit.Add( actor ) ) continue;

			actor.ApplyDamage( damage );
			CombatMath.ApplyKnockback( candidate.GameObject, toTarget.WithZ(0), damage.KnockbackForce );
			SpellEffectApplier.Apply( ctx, candidate.GameObject, ctx.Origin );
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
			RuneEvaluator.ExecuteTriggerPayload( ctx, ctx.Origin, aim, null );
	}

	private static DamageProfileDef BuildDamage( SpellContext ctx )
	{
		var damage = new DamageProfileDef
		{
			HealthDamage = ctx.AccumulatedDamage.HealthDamage * ctx.DamageMultiplier,
			StaminaDamage = ctx.AccumulatedDamage.StaminaDamage,
			KnockbackForce = ctx.AccumulatedDamage.KnockbackForce,
			Tags = ctx.AttackTags
		};
		return CombatMath.RollCrit( ctx.Caster.Components.GetInAncestorsOrSelf<Actor>()?.StatSheet, damage );
	}

}
