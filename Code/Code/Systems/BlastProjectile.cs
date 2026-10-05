using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public sealed class BlastProjectile : Component, Component.ICollisionListener
{
	[Property] public float GravityScale { get; set; } = 1f;
	/// <summary>Preferred elevation above the horizontal for the lob.</summary>
	[Property] public float LaunchAngleDegrees { get; set; } = 35f;
	/// <summary>Maximum angle the lob may deviate from the caster's aim direction.</summary>
	[Property] public float MaxAimDeviationDegrees { get; set; } = 35f;
	[Property] public float ColliderRadius { get; set; } = 12f;
	[Property] public float BlastRadius { get; set; } = 180f;
	[Property] public float FuseSeconds { get; set; } = 8f;
	[Property] public int BounceCount { get; set; }
	[Property] public float BounceElasticity { get; set; } = 0.5f;

	private SpellContext _context;
	private Vector3 _launchDirection;
	private float _launchSpeed;
	private float _age;
	private int _bounces;
	private bool _armed;
	private bool _detonated;
	private Rigidbody _body;
	private SphereCollider _collider;

	public void Arm( SpellContext context, Vector3 launchDirection, float launchSpeed )
	{
		_context = context?.Clone();
		_launchDirection = launchDirection.LengthSquared > 0.001f ? launchDirection.Normal : Vector3.Up;
		_launchSpeed = MathF.Max( 0f, launchSpeed );
		_armed = _context != null;
	}

	protected override void OnStart()
	{
		if ( !_armed )
		{
			Enabled = false;
			return;
		}

		_collider = Components.GetOrCreate<SphereCollider>();
		_collider.Enabled = true;
		_collider.IsTrigger = false;
		_collider.Radius = MathF.Max( 1f, ColliderRadius );
		_collider.Elasticity = Math.Clamp( BounceElasticity, 0f, 1f );

		_body = Components.GetOrCreate<Rigidbody>();
		_body.Enabled = true;
		_body.Gravity = true;
		_body.GravityScale = MathF.Max( 0f, GravityScale );
		_body.MotionEnabled = true;
		_body.CollisionEventsEnabled = true;
		_body.EnhancedCcd = true;
		_body.EnableImpactDamage = false;
		_body.Velocity = _launchDirection * _launchSpeed;
	}

	protected override void OnUpdate()
	{
		if ( !_armed || _detonated ) return;

		_age += Time.Delta;
		if ( _age >= MathF.Max( 0.1f, FuseSeconds ) )
			Detonate( GameObject.WorldPosition, Vector3.Up, null );
	}

	void Component.ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( !_armed || _detonated ) return;

		var other = collision.Other.GameObject;
		if ( IsCaster( other ) ) return;

		if ( _bounces < Math.Max( 0, BounceCount ) )
		{
			_bounces++;
			return;
		}

		Detonate( GameObject.WorldPosition, collision.Contact.Normal, other );
	}

	void Component.ICollisionListener.OnCollisionStop( CollisionStop collisionStop ) { }

	void Component.ICollisionListener.OnCollisionUpdate( Collision collision ) { }

	private bool IsCaster( GameObject other )
	{
		if ( !other.IsValid() || !_context.Caster.IsValid() ) return false;
		if ( other == _context.Caster ) return true;
		return other.Components.GetInAncestorsOrSelf<Actor>()?.GameObject == _context.Caster;
	}

	private void Detonate( Vector3 position, Vector3 normal, GameObject impactTarget )
	{
		if ( _detonated ) return;
		_detonated = true;
		_armed = false;

		if ( _body.IsValid() )
		{
			_body.Velocity = Vector3.Zero;
			_body.MotionEnabled = false;
		}
		if ( _collider.IsValid() )
			_collider.Enabled = false;

		float radius = MathF.Max( 1f, BlastRadius );
		var prefabPath = SpellVisualResolver.ResolvePrefab( _context.PrimaryElement, SpellShape.Nova );
		SpellVfx.Spawn( prefabPath, position, Rotation.Identity, Vector3.One * (radius / SpellVfx.RefSize),
			SpellVisualResolver.ResolveMaterial( _context ) );

		var hits = Scene.Trace.Sphere( radius, position, position )
			.IgnoreGameObjectHierarchy( _context.Caster )
			.RunAll();
		var alreadyHit = new HashSet<GameObject>();
		var baseDamage = _context.BuildDamageProfile();

		foreach ( var hit in hits )
		{
			if ( !hit.GameObject.IsValid() ) continue;
			var actorRoot = CombatMath.ResolveActorRoot( hit.GameObject );
			if ( !actorRoot.IsValid() || alreadyHit.Contains( actorRoot ) ) continue;

			var direction = actorRoot.WorldPosition - position;
			if ( HitResolver.Apply( _context.Caster, hit.GameObject, baseDamage, direction, alreadyHit, hit.HitPosition ) )
				SpellEffectApplier.Apply( _context, actorRoot, position );
		}

		if ( _context.TriggerPayloadRunes != null && _context.TriggerPayloadRunes.Count > 0 )
			RuneEvaluator.ExecuteTriggerPayload( _context, position, normal, impactTarget );

		GameObject.Destroy();
	}
}

public sealed class BlastDeliveryMethod : ISpellDeliveryMethod
{
	public void Deliver( SpellPayload payload )
	{
		var ctx = payload.Context;
		if ( ctx == null || !ctx.Caster.IsValid() ) return;

		if ( string.IsNullOrWhiteSpace( payload.PrefabPath )
			|| !ResourceLibrary.TryGet<PrefabFile>( payload.PrefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[BlastDelivery] Could not find projectile prefab '{payload.PrefabPath}'." );
			return;
		}

		var template = payload.ProjectileTemplate?.Clone() ?? new ProjectileTemplate();
		template.Speed *= ctx.SpeedMultiplier * ElementLibrary.Get( ctx.PrimaryElement ).ProjectileSpeedMultiplier;
		var aim = ctx.AimDirection.LengthSquared > 0.001f
			? ctx.AimDirection.Normal
			: ctx.Caster.WorldRotation.Forward;

		var projectile = SceneUtility.GetPrefabScene( prefabFile ).Clone( new CloneConfig
		{
			Transform = new Transform( ctx.Origin, Rotation.LookAt( aim ) ),
			Parent = null,
			StartEnabled = false
		} );
		projectile.Name = "BlastProjectile";
		projectile.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		projectile.WorldPosition = ctx.Origin;

		var blast = projectile.Components.Get<BlastProjectile>( FindMode.EverythingInSelfAndDescendants );
		if ( blast == null )
		{
			blast = projectile.Components.Create<BlastProjectile>();
			if ( ctx.PrimaryElement == RuneElementTag.Earth )
			{
				blast.BounceCount = 1;
				blast.BounceElasticity = 0.65f;
			}
		}

		var flatAim = aim.WithZ( 0f );
		if ( flatAim.LengthSquared <= 0.001f )
			flatAim = ctx.Caster.WorldRotation.Forward.WithZ( 0f );
		if ( flatAim.LengthSquared <= 0.001f )
			flatAim = ctx.Caster.WorldRotation.Forward;
		flatAim = flatAim.Normal;

		float launchAngle = Math.Clamp( blast.LaunchAngleDegrees, 0f, 80f ) * MathF.PI / 180f;
		var preferredDirection = (flatAim * MathF.Cos( launchAngle ) + Vector3.Up * MathF.Sin( launchAngle )).Normal;
		var launchDirection = ClampToAim( aim, preferredDirection, blast.MaxAimDeviationDegrees );
		projectile.WorldRotation = Rotation.LookAt( launchDirection );

		var regularProjectile = projectile.Components.Get<Projectile>( FindMode.EverythingInSelfAndDescendants );
		if ( regularProjectile != null ) regularProjectile.Enabled = false;
		var ballMotion = projectile.Components.Get<BallMotion>( FindMode.EverythingInSelfAndDescendants );
		if ( ballMotion != null ) ballMotion.Enabled = false;

		blast.Arm( ctx, launchDirection, template.Speed );
		blast.Enabled = true;
		projectile.Enabled = true;
	}

	private static Vector3 ClampToAim( Vector3 aim, Vector3 preferredDirection, float maxDeviationDegrees )
	{
		float dot = Math.Clamp( Vector3.Dot( aim, preferredDirection ), -1f, 1f );
		float angle = MathF.Acos( dot );
		float maxAngle = Math.Clamp( maxDeviationDegrees, 0f, 180f ) * MathF.PI / 180f;
		if ( angle <= maxAngle || maxAngle <= 0f ) return angle <= maxAngle ? preferredDirection : aim;

		float sinAngle = MathF.Sin( angle );
		if ( MathF.Abs( sinAngle ) < 0.001f ) return aim;

		return (
			aim * MathF.Sin( angle - maxAngle )
			+ preferredDirection * MathF.Sin( maxAngle )
		).Normal;
	}
}
