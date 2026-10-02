namespace Sandbox.Code.Systems;
using Actors;


public class MovementControl : Component
{
	// Jump()
	// StaminaCost()
	[Property] public float MoveSpeed { get; set; } = 200f;
	[Property] public float Accel { get; set; } = 200f;

	private Vector3 Velocity;
	
	public Vector3 UpdateVelocity( Vector3 currentVelocity, Vector3 inputDir ) 
	{
		var wishDir = inputDir.Normal;
		var wishVel = wishDir * MoveSpeed;
		return Vector3.Lerp( currentVelocity, wishVel, Time.Delta * Accel  );
		
		
	}

	
	
}
