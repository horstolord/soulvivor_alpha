using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>
/// Channel form: holds ChannelControl open and, every tick, hits everything in a radius around the caster
/// (Whirlwind). The origin is read live each tick, so it follows the caster as they move.
/// Other channel shapes (forward beam, cone) would get their own handler; the form/executor split stays the same.
/// </summary>
public sealed class ChannelExecutor : ITechniqueExecutor
{
	private sealed class InitialChannelCost : ICostable
	{
		public float HealthCost { get; set; }
		public float StaminaCost { get; set; }
		public float EnergyCost { get; set; }
	}

	public bool TryExecute( TechniqueContext ctx, CombatComponent combat )
	{
		var spec = ctx?.Technique?.Channel;
		if ( spec == null || ctx.Actor == null )
		{
			Log.Warning( $"[Technique] '{ctx?.Technique?.DisplayName}' is a Channel form but has no ChannelSpec." );
			return false;
		}

		var channel = ctx.Actor.Components.GetOrCreate<ChannelControl>();

		// Weapon base speed (already in TimeMultiplier) and the live AttackSpeed stat both speed the spin up;
		// per-SECOND costs stay the same, so a faster spin costs the same stamina and deals more damage per second.
		float interval = MathF.Max( 0.05f, spec.TickInterval * ctx.TimeMultiplier / ctx.LiveAttackSpeed );
		var initialCost = new InitialChannelCost
		{
			HealthCost = ctx.HealthCost + spec.HealthPerSecond * interval * ctx.CostMultiplier,
			StaminaCost = ctx.StaminaCost + spec.StaminaPerSecond * interval * ctx.CostMultiplier,
			EnergyCost = ctx.EnergyCost + spec.EnergyPerSecond * interval * ctx.CostMultiplier
		};
		if ( !ctx.Actor.CanPayCost( initialCost ) )
		{
			Log.Info( $"[Technique] Not enough resources to start channel '{ctx.Technique.DisplayName}' including its first tick." );
			return false;
		}

		if ( !channel.Begin( spec, new RadialHandler( ctx ), interval, ctx.CostMultiplier ) )
			return false;

		// Flat start cost (TechniqueDef.StaminaCost etc.), separate from the per-second drain.
		ctx.Actor.PayCost( ctx );
		return true;
	}

	private sealed class RadialHandler : IChannelHandler
	{
		private readonly TechniqueContext _ctx;
		private DamageProfileDef _damage;

		public RadialHandler( TechniqueContext ctx )
		{
			_ctx = ctx;
		}

		public void OnChannelStart()
		{
			// Snapshot once: stats can change mid-channel, but damage per tick stays predictable.
			_damage = _ctx.BuildHitDamage();
		}

		public void OnChannelTick( int tickIndex )
		{
			var caster = _ctx.Caster;
			if ( !caster.IsValid() || _damage == null ) return;

			var origin = caster.WorldPosition + Vector3.Up * 40f;
			float radius = _ctx.Technique.Radius * _ctx.AreaMultiplier;

			var casterActor = CombatMath.ResolveActor( caster );
			var hits = caster.Scene.Trace
				.Sphere( radius, origin, origin )
				.IgnoreGameObjectHierarchy( caster )
				.RunAll();

			var alreadyHit = new HashSet<GameObject>();
			foreach ( var hit in hits )
			{
				if ( !hit.GameObject.IsValid() ) continue;

				// Actors only: the floor/walls overlap the sphere (log spam), and re-hitting a corpse would re-run
				// Actor.OnKilled (duplicate soul orbs / loot) — repeating ticks make that a real risk.
				var actor = CombatMath.ResolveActor( hit.GameObject );
				if ( actor == null || actor == casterActor ) continue;
				if ( actor.StateComp?.CurrentState == ActorStateType.Dead ) continue;
				if ( actor.StatSheet != null && actor.StatSheet.CurrentHealth <= 0f ) continue;

				// Flat direction away from the caster (ApplyKnockback adds the upward bias).
				var direction = (actor.GameObject.WorldPosition - caster.WorldPosition).WithZ( 0f );
				HitResolver.Apply( caster, hit.GameObject, _damage, direction, alreadyHit, hit.HitPosition );
			}
		}

		public void OnChannelEnd( ChannelEndReason reason )
		{
			Log.Info( $"[Technique] '{_ctx.Technique.DisplayName}' channel ended: {reason}" );
		}
	}
}
