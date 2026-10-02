using System;

namespace Sandbox.Code.Systems;

public static class RagdollImpulseApplier
{
	public static bool BodiesReady( ModelPhysics physics ) =>
		physics != null && physics.Bodies != null && physics.Bodies.Count > 0;

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
			if ( rb.IsValid() ) rb.Velocity = velocity;
		}
	}

	/// <summary>
	/// Applies a knockback impulse to the whole ragdoll. Every bone gets the same velocity change
	/// (impulse / totalMass), so the knockback feels the same as it did on the living actor.
	/// If hitPoint is given, the bone nearest to it gets an extra kick of
	/// focusBoost * that velocity change, which is what makes the body tumble/spin instead of
	/// sliding as one rigid lump. focusBoost = 0 gives the old uniform behaviour.
	/// </summary>
	public static bool TryApply( ModelPhysics physics, Vector3 impulse, Vector3? hitPoint = null, float focusBoost = 1.5f )
	{
		if ( !BodiesReady( physics ) ) return false;

		float totalMass = 0f;
		Rigidbody nearest = null;
		float bestDistSq = float.MaxValue;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( !rb.IsValid() || rb.Mass <= 0f ) continue;

			totalMass += rb.Mass;

			if ( hitPoint.HasValue )
			{
				float distSq = (rb.WorldPosition - hitPoint.Value).LengthSquared;
				if ( distSq < bestDistSq )
				{
					bestDistSq = distSq;
					nearest = rb;
				}
			}
		}

		if ( totalMass <= 0f ) return false;

		var deltaV = impulse / totalMass;

		foreach ( var body in physics.Bodies )
		{
			var rb = body.Component;
			if ( rb.IsValid() && rb.Mass > 0f )
				rb.ApplyImpulse( deltaV * rb.Mass );
		}

		if ( nearest != null && focusBoost > 0f )
			nearest.ApplyImpulse( deltaV * nearest.Mass * focusBoost );

		return true;
	}
}
