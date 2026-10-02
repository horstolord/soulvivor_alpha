namespace Sandbox.Code.Systems;

public interface IRagdollHandler
{
	void EnterRagdoll();
	void ExitRagdoll();
	bool TryApplyImpulse( Vector3 impulse, Vector3? hitPoint = null );
}
