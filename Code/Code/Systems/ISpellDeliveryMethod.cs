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

		projComp.Template = template;
		projComp.Payload = new ProjectilePayload
		{
			Caster = ctx.Caster,
			// Base profile (pre-crit, pre-PhysicalForce). HitResolver.Apply resolves outgoing mods per hit,
			// so a piercing projectile rolls crit independently for each target it passes through.
			Damage = ctx.BuildDamageProfile(),
			AttackTags = ctx.AttackTags,
			SourceContext = ctx
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

		var baseDamage = ctx.BuildDamageProfile();
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
			// HitResolver handles crit, PhysicalForce, damage, and knockback in one place.
			// Single-target ray hit — no dedup set needed (null).
			HitResolver.Apply( ctx.Caster, tr.GameObject, baseDamage, ctx.AimDirection,
				alreadyHit: null, hitPoint: tr.HitPosition );
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

		var baseDamage = ctx.BuildDamageProfile();

		var alreadyHit = new HashSet<GameObject>();
		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;

			// Radial direction away from the blast origin, z preserved (upward bias added by ApplyKnockback).
			var root = CombatMath.GetKnockbackRoot( hit.GameObject ) ?? hit.GameObject;
			var radialDirection = root.WorldPosition - ctx.Origin;

			// HitResolver deduplicates by actor-root via alreadyHit, fixing the multi-collider bug.
			if ( HitResolver.Apply( ctx.Caster, hit.GameObject, baseDamage, radialDirection, alreadyHit, hit.HitPosition ) )
				SpellEffectApplier.Apply( ctx, hit.GameObject, ctx.Origin );
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
		var baseDamage = ctx.BuildDamageProfile();
		var alreadyHit = new HashSet<GameObject>();

		foreach ( var candidate in candidates )
		{
			if ( !candidate.GameObject.IsValid() ) continue;
			var targetRoot = CombatMath.GetKnockbackRoot( candidate.GameObject );
			if ( targetRoot == null || alreadyHit.Contains( targetRoot ) ) continue;

			var toTarget = targetRoot.WorldPosition - ctx.Origin;
			float distance = toTarget.Length;
			if ( distance > range || distance <= 0.001f || Vector3.Dot( toTarget / distance, aim ) < minDot ) continue;

			if ( payload.ConeRequiresLineOfSight )
			{
				var sight = ctx.Caster.Scene.Trace.Ray( ctx.Origin, targetRoot.WorldPosition )
					.IgnoreGameObjectHierarchy( ctx.Caster ).Run();
				if ( sight.Hit && (!sight.GameObject.IsValid() || CombatMath.GetKnockbackRoot( sight.GameObject ) != targetRoot) ) continue;
			}

			// HitResolver handles dedup (adds to alreadyHit), crit, PhysicalForce, damage, knockback.
			// toTarget.WithZ(0) keeps knockback horizontal; ApplyKnockback adds the upward bias.
			if ( HitResolver.Apply( ctx.Caster, candidate.GameObject, baseDamage, toTarget.WithZ( 0f ), alreadyHit, candidate.HitPosition ) )
				SpellEffectApplier.Apply( ctx, candidate.GameObject, ctx.Origin );
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
			RuneEvaluator.ExecuteTriggerPayload( ctx, ctx.Origin, aim, null );
	}

}
