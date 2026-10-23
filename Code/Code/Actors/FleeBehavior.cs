namespace Sandbox.Code.Actors;

/// <summary>
/// Temporary IEnemyBehavior installed by the Fear status (Enemy.SetBehaviorOverride). Runs away from
/// the fear source; never attacks. Removed again when Fear expires.
/// </summary>
public sealed class FleeBehavior : Component, IEnemyBehavior
{
	[Property] public float FleeDistance { get; set; } = 400f;
	public GameObject From { get; set; }

	public void Tick( Enemy self, float dt )
	{
		var away = self.Target;
		var origin = From.IsValid() ? From : away;
		if ( !origin.IsValid() ) { self.StopMoving(); return; }

		var dir = (self.GameObject.WorldPosition - origin.WorldPosition).WithZ( 0f );
		dir = dir.LengthSquared > 1f ? dir.Normal : Vector3.Backward;
		self.MoveToward( self.GameObject.WorldPosition + dir * FleeDistance );
	}
}
