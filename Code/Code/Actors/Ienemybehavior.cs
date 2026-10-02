namespace Sandbox.Code.Actors;
 

 
/// <summary>
/// Decides where an Enemy wants to be and when it should attack.
/// Attach exactly one implementation per enemy prefab. Enemy fetches it at
/// runtime via Components.Get&lt;IEnemyBehavior&gt;() — same pattern as
/// IProjectileMotion (BallMotion/ArrowMotion). No subclassing Enemy, ever;
/// archetype variance comes from which behavior component sits on the prefab.
/// </summary>
public interface IEnemyBehavior
{
	void Tick( Enemy self, float dt );
	bool Enabled {get; set;}
}
