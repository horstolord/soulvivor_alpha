using System;

namespace Sandbox.Code.Systems;

public static class RagdollImpulseApplier
{
	public static bool BodiesReady( ModelPhysics physics ) =>
		physics != null && physics.Bodies != null && physics.Bodies.Count > 0;
	/// <summary>Hard cap on any velocity we hand to ragdoll bodies (units/s). Tune.</summary>
	public const float MaxLaunchSpeed = 1200f;

	private static Vector3 ClampSpeed( Vector3 v ) =>
		v.Length > MaxLaunchSpeed ? v.Normal * MaxLaunchSpeed : v;

	/// <summary>Call once per ragdoll entry, after bodies exist.</summary>
	public static void Stabilize( ModelPhysics physics, float angularDamping = 3f )
	{
		if ( !BodiesReady( physics ) ) return;
		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() ) continue;
			rb.AngularDamping = angularDamping;
			rb.EnhancedCcd = true;
		}
	}

	/// <summary>
	/// Sets every bone body to the same linear velocity. Used right after ragdolling so the
	/// corpse keeps the momentum the living actor had (run, knockback, etc.).
	/// </summary>
	public static void SetVelocity( ModelPhysics physics, Vector3 velocity )
	{
		if ( !BodiesReady( physics ) ) return;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( rb.IsValid() ) rb.Velocity = ClampSpeed(velocity);
		}
	}

	/// <summary>Applies the full impulse to the ragdoll body nearest the hit point.</summary>
	public static bool TryApply( ModelPhysics physics, Vector3 impulse, Vector3? hitPoint = null )
	{
		if ( !BodiesReady( physics ) ) return false;

		var referencePoint = hitPoint ?? physics.GameObject.WorldPosition;
		Rigidbody nearest = null;
		float bestDistSq = float.MaxValue;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() || rb.Mass <= 0f ) continue;

			float distSq = (rb.WorldPosition - referencePoint).LengthSquared;
			if ( distSq < bestDistSq )
			{
				bestDistSq = distSq;
				nearest = rb;
			}
		}

		if ( nearest == null ) return false;

		float totalMass = 0f;
		foreach ( var body in physics.Bodies )
			if ( body.Component.IsValid() ) totalMass += body.Component.Mass;
		if ( totalMass <= 0f ) return false;

		// Physical deltaV = impulse / mass
		var rootBody = physics.Components.GetInAncestorsOrSelf<Rigidbody>();
		float mass = (rootBody != null && rootBody.Mass > 0f) ? rootBody.Mass : totalMass;
		var deltaV = ClampSpeed( impulse / mass );
		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( rb.IsValid() && rb.Mass > 0f )
				rb.ApplyImpulse( deltaV * rb.Mass );
		}

// A little extra on the struck bone so hits still read directionally. Tune or remove.
		nearest.ApplyImpulse( deltaV * nearest.Mass * 0.5f );
		return true;
	}
}
