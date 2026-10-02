namespace Sandbox.Code.Systems;

// Anything that can be paid for out of an Actor's resource pools —
// AttackDef and RuneDef both implement this so Actor.CanPayCost/PayCost
// don't need to know which pipeline they're being called from.
public interface ICostable
{
	float HealthCost { get; }
	float StaminaCost { get; }
	float EnergyCost { get; }
}
