namespace Sandbox.Code.Systems;

public interface IProjectileMotion
{
	void Tick( Projectile owner, float dt );
}

public class BallMotion : Component, IProjectileMotion
{
	public void Tick( Projectile owner, float dt )
	{
		owner.GameObject.WorldPosition += owner.GameObject.WorldRotation.Forward * owner.Template.Speed * dt;
	}
}
public class ArrowMotion : Component, IProjectileMotion
{
	[Property] public float Gravity { get; set; } = 750f;

	private Vector3 _velocity;
	private bool _initialized;

	public void Tick( Projectile self, float dt )
	{
		if ( !_initialized )
		{
			_velocity = self.GameObject.WorldRotation.Forward * self.Template.Speed;
			_initialized = true;
		}

		_velocity += Vector3.Down * Gravity * dt;
		self.GameObject.WorldPosition += _velocity * dt;

		//  makes the drop arc visible
		if ( _velocity.LengthSquared > 0.01f )
			self.GameObject.WorldRotation = Rotation.LookAt( _velocity.Normal, Vector3.Up );
	}
}
