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
	/// <summary>Resolved at evaluation: rune override, then element override, then shared shape prefab.</summary>
	public string PrefabPath;
	public float BeamRange;
	public float BeamVisualLength;
	public float BeamRadius;
	public float AoERadius;
	public float ConeAngle;
	public bool ConeRequiresLineOfSight;
	/// <summary>Seconds after the cast completes before this payload is delivered (multicast stagger, echo).</summary>
	public float DeliveryDelay;
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
		template.Speed *= ctx.SpeedMultiplier * ElementLibrary.Get( ctx.PrimaryElement ).ProjectileSpeedMultiplier;
		template.AddPierce( ctx.BonusPierce ); // upgrades a FirstHit template; a bare += did nothing there

		var rot = Rotation.LookAt( ctx.AimDirection );
		if ( ctx.SpreadAngle > 0.01f )
		{
			var yawOffset = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			var pitchOffset = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			rot *= Rotation.From( pitchOffset, yawOffset, 0 );
		}

		var spawnTransform = new Transform( ctx.Origin, rot );

		var prefabPath = payload.PrefabPath;
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
		{
			Log.Warning( "[ProjectileDelivery] No prefab resolved for this projectile." );
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
		var range = payload.BeamRange > 0 ? payload.BeamRange : 3000f;
		var aim = ctx.AimDirection.LengthSquared > 0.001f
			? ctx.AimDirection.Normal
			: ctx.Caster.WorldRotation.Forward;
		var endPos = ctx.Origin + aim * range;
		float hitRadius = MathF.Max( 0.01f, payload.BeamRadius );
		var hits = ctx.Caster.Scene.Trace
			.Sphere( hitRadius, ctx.Origin, endPos )
			.IgnoreGameObjectHierarchy( ctx.Caster )
			.RunAll();

		float visibleDistance = range;
		Vector3 endNormal = -aim;
		GameObject blockingObject = null;
		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;
			if ( CombatMath.ResolveActorRoot( hit.GameObject ).IsValid() ) continue;

			float distance = Vector3.Dot( hit.HitPosition - ctx.Origin, aim );
			if ( distance < 0f || distance >= visibleDistance ) continue;

			visibleDistance = distance;
			endNormal = hit.Normal;
			blockingObject = hit.GameObject;
		}
		var targetPos = ctx.Origin + aim * visibleDistance;

		var baseDamage = ctx.BuildDamageProfile();
		float refLength = payload.BeamVisualLength > 0f ? payload.BeamVisualLength : SpellVfx.RefSize;
		float beamScale = MathF.Max( 0.01f, visibleDistance / refLength );
		SpellVfx.Spawn( payload.PrefabPath, ctx.Origin, Rotation.LookAt( aim ), new Vector3( beamScale, 1f, 1f ),
			SpellVisualResolver.ResolveMaterial( ctx ), ctx.Caster, autoBurst: true );

		var alreadyHit = new HashSet<GameObject>();
		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;
			var actorRoot = CombatMath.ResolveActorRoot( hit.GameObject );
			if ( !actorRoot.IsValid() || alreadyHit.Contains( actorRoot ) ) continue;

			float distance = Vector3.Dot( hit.HitPosition - ctx.Origin, aim );
			if ( distance < 0f || distance > visibleDistance ) continue;

			if ( HitResolver.Apply( ctx.Caster, hit.GameObject, baseDamage, aim, alreadyHit, hit.HitPosition ) )
				SpellEffectApplier.Apply( ctx, actorRoot, hit.HitPosition );
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
			RuneEvaluator.ExecuteTriggerPayload( ctx, targetPos, endNormal, blockingObject );
	}
}

public class SelfDeliveryMethod : ISpellDeliveryMethod
	{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;

		float? visualLifetime = null;
		foreach ( var effect in ctx.Effects )
		{
			if ( effect?.Type != SpellEffectType.Buff || effect.Duration <= 0f ) continue;
			if ( !visualLifetime.HasValue || effect.Duration > visualLifetime.Value )
				visualLifetime = effect.Duration;
		}

		SpellVfx.Spawn( payload.PrefabPath, ctx.Caster.WorldPosition, ctx.Caster.WorldRotation, Vector3.One,
			SpellVisualResolver.ResolveMaterial( ctx ), ctx.Caster, lifetime: visualLifetime );
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
		// A top-level nova is centered on its caster, not the camera/aim origin.
		// Triggered novas keep their impact origin so chained effects stay at the trigger point.
		var novaOrigin = ctx.RecursionDepth == 0 ? ctx.Caster.WorldPosition : ctx.Origin;
		var radius = payload.AoERadius > 0 ? payload.AoERadius : 150f;
		// Shape prefab is authored with a 100-unit radius at scale 1.
		SpellVfx.Spawn( payload.PrefabPath, novaOrigin, Rotation.Identity, Vector3.One * (radius / SpellVfx.RefSize),
			SpellVisualResolver.ResolveMaterial( ctx ) );
		var hits = ctx.Caster.Scene.Trace
			.Sphere( radius, novaOrigin, novaOrigin )
			.IgnoreGameObjectHierarchy( ctx.Caster )
			.RunAll();

		var baseDamage = ctx.BuildDamageProfile();

		var alreadyHit = new HashSet<GameObject>();
		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;

			// Radial direction away from the blast origin, z preserved (upward bias added by ApplyKnockback).
			var root = CombatMath.GetKnockbackRoot( hit.GameObject ) ?? hit.GameObject;
			var radialDirection = root.WorldPosition - novaOrigin;

			// HitResolver deduplicates by actor-root via alreadyHit, fixing the multi-collider bug.
			if ( HitResolver.Apply( ctx.Caster, hit.GameObject, baseDamage, radialDirection, alreadyHit, hit.HitPosition ) )
				SpellEffectApplier.Apply( ctx, hit.GameObject, novaOrigin );
		}

		if ( ctx.TriggerPayloadRunes != null && ctx.TriggerPayloadRunes.Count > 0 )
		{
			RuneEvaluator.ExecuteTriggerPayload( ctx, novaOrigin, Vector3.Up, null );
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
		float spread = MathF.Tan( halfAngle * MathF.PI / 180f ) * range / SpellVfx.RefSize;
		SpellVfx.Spawn( payload.PrefabPath, ctx.Origin, Rotation.LookAt( aim ), new Vector3( range / SpellVfx.RefSize, spread, spread ),
			SpellVisualResolver.ResolveMaterial( ctx ) );
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

/// <summary>Applies the cast's effects to the caster; an Imbue effect hooks the weapon (see WeaponImbueControl).</summary>
public class ImbueDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;
		SpellEffectApplier.Apply( ctx, ctx.Caster, ctx.Origin );
	}
}
