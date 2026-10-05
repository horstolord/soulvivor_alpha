using System.Collections.Generic;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>
/// What actually happened to a victim, after all mitigation. Raised by Actor.ApplyDamage (the only place
/// that knows the final number). Consumers: damage numbers now; hit flash, SFX, kill feed, stats later.
/// </summary>
public readonly struct DamageEvent
{
	public Actor Victim { get; init; }
	/// <summary>Attacker if the call site passed one (null otherwise).</summary>
	public GameObject Source { get; init; }
	/// <summary>Final health damage after resistances, armor and block.</summary>
	public float Amount { get; init; }
	public HashSet<AttackTag> Tags { get; init; }
	public bool IsCrit { get; init; }
	public bool Evaded { get; init; }
	public bool Blocked { get; init; }
	public bool Killed { get; init; }
}
