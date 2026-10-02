using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Holds a preferred range band: backs off if the target gets too close,
/// closes in if the target is too far, otherwise holds ground and shoots.
/// Always faces the target (not the movement direction) so it can keep
/// aiming while retreating — combat execution itself needs nothing new,
/// CombatComponent.SpawnProjectile already handles any AttackDef with a
/// ProjectileTemplate (see AttackData.Shoot for reference).
/// </summary>
public sealed class RangedKiteBehavior : Component, IEnemyBehavior
{
	[Property] public float PreferredMinRange { get; set; } = 350f;
	[Property] public float PreferredMaxRange { get; set; } = 650f;
	[Property] public float RetreatDistance { get; set; } = 200f;

	protected override void OnStart()
	{
		// AttackDef has no inspector picker (see class comment above), so this component
		// supplies its archetype's default in code instead of leaving Enemy.AttackType
		// unset — which would silently fall back to AttackData.Punch via
		// Enemy.GetAttackType() and the goblin would just melee at point-blank range.
		// Only sets it if nothing's there yet, so it won't clobber a real per-mob
		// AttackDef once you've authored one (e.g. a proper goblin bolt).
		var enemy = Components.Get<Enemy>();
		if ( enemy != null  )
			enemy.AttackType = AttackData.Shoot;
	}

	public void Tick( Enemy self, float dt )
	{
		self.FaceTarget();

		float dist = self.DistanceToTarget;

		if ( dist < PreferredMinRange )
		{
			var away = (self.GameObject.WorldPosition - self.Target.WorldPosition).WithZ( 0 ).Normal;
			self.Agent.MoveTo( self.GameObject.WorldPosition + away * RetreatDistance );
		}
		else if ( dist > PreferredMaxRange )
		{
			self.Agent.MoveTo( self.Target.WorldPosition );
		}
		else
		{
			self.Agent.Stop(); // TODO: strafe instead of standing still, once basic kiting feels right
		}

		if ( dist <= PreferredMaxRange && self.Combat?.CanAttack == true && self.HasLineOfSight() )
			self.TryAttack( self.GetAttackType() );
	}
}
