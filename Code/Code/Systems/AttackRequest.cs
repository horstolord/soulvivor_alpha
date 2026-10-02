namespace Sandbox.Code.Systems;
public enum AttackTriggerType
{
	PlayerInput,
	Ai,
	Proc,
	Trap,
	ProjectileImpact,
	ScriptedEvent
}
public class AttackRequest
{
	public GameObject Attacker;
	public AttackDef Attack;
	public GameObject? SourceItem;
	public Vector3 Origin;
	public Rotation Facing;
	public Vector3 AimDirection;
	public Vector3? TargetPoint;
	public float Charge01;
	public AttackTriggerType TriggerType;
	public bool AlternateUse;
}
