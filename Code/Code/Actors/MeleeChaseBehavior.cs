namespace Sandbox.Code.Actors;

using Sandbox.Code.Systems;

/// <summary>
/// Closes to melee range and swings. Behavior parity with the old inline
/// Enemy.OnFixedUpdate logic, just routed through NavMeshAgent for pathing.
/// </summary>
public sealed class MeleeChaseBehavior : Component, IEnemyBehavior
{
	[Property] public float AttackRange { get; set; } = 80f;
 
	/// <summary>Don't walk directly inside the target once close enough to swing.</summary>
	[Property] public float StopRange { get; set; } = 50f;
 
	public void Tick( Enemy self, float dt )
	{
		self.FaceTarget();
 
		float rangeScale = System.MathF.Max( 0.1f, (self.StatSheet?.Range?.Value ?? 100f) / 100f );
		float dist = self.DistanceToTarget;
		if ( dist > StopRange * rangeScale ) self.MoveToward( self.Target.WorldPosition );
		else self.StopMoving();
		
 
		if ( dist <= AttackRange * rangeScale && self.Combat?.CanAttack == true )
			self.TryAttack( self.GetAttackType() );
	}
}
