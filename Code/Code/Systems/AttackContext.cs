using Sandbox.MovieMaker;

namespace Sandbox.Code.Systems;

public class AttackContext
{
	public AttackRequest Request;
	public AttackDef Attack;
	public GameObject Attacker;
	public GameObject? SourceItem;
	public Vector3 Origin;
	public Rotation Facing;
	public Vector3 AimDirection;
	public Vector3? TargetPoint;
	public float Charge01;
	public bool AlternateUse;
	public AttackTriggerType TriggerType;
	public float StartupTime;
	public float RecoveryTime;
	public float CooldownTime;
	public float HealthCost;
	public float StaminaCost;
	public float EnergyCost;
	public DamageProfileDef Damage;
	public AttributeScalingDef Scaling;
	public List<HitPhaseDef> HitPhases = new();
	public HashSet<AttackTag> Tags = new();
	public string AnimationName;
	public bool LockFacing;
	public bool CanMoveDuringStartup;
	public bool CanMoveDuringRecovery;
}
