using Sandbox;
using System;
using System.Linq;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;
namespace Sandbox.Code.Actors;
public sealed class Enemy : Actor
{
	/// <summary>
	/// Set by SpawnDirector before OnStart to select which MobRegistry preset to load.
	/// Defaults to goblin so manually placed enemies work without configuration.
	/// </summary>
	[Property] public string PresetOverride { get; set; } = "goblin";
	protected override string GetMobPresetId() => PresetOverride;

	[Property] public GameObject Target { get; set; }
	[Property] public AttackDef AttackType { get; set; } // The attack type this enemy uses

	/// <summary>
	/// Navmesh-aware velocity solver only — UpdatePosition is off, so Enemy applies its
	/// velocity to the GameObject and syncs the simulated agent position after movement.
	/// Behaviors call Agent.MoveTo()/Stop(); Enemy bridges the result into movement each tick.
	/// </summary>
	public NavMeshAgent Agent { get; private set; }
	public SkinnedModelRenderer bodyRenderer { get; set; }
	private CharacterController _controller;
	private IEnemyBehavior _behavior;
	private Vector3 _knockbackVelocity;

	protected override void OnStart()
	{
		base.OnStart();
		// Fetch standard components
		_controller = Components.Get<CharacterController>();
		Combat = Components.Get<CombatComponent>();

		Agent = Components.GetOrCreate<NavMeshAgent>();
		Agent.UpdatePosition = false; // Enemy applies movement and syncs AgentPosition manually
		Agent.UpdateRotation = false; // Enemy.FaceTarget() handles facing
		Agent.Acceleration = MathF.Max( Agent.Acceleration, 1000f ); // snappy — Facepunch recommends accel >= max speed
		Agent.SetAgentPosition( GameObject.WorldPosition );

		bodyRenderer ??= Components.GetInAncestorsOrSelf<SkinnedModelRenderer>() ?? Components.GetInChildren<SkinnedModelRenderer>();

		_behavior = Components.Get<IEnemyBehavior>();
		if ( _behavior == null )
			Log.Warning( $"[Enemy] {GameObject.Name} has no IEnemyBehavior component attached — it will stand still." );

		// Fallback target find
		if ( Target == null )
		{
			FindPlayerTarget();
		}
	}
	protected override void OnUpdate()
	{
		base.OnUpdate();
		if ( Target == null )
		{
			FindPlayerTarget();
			return;
		}
	}
	protected override void OnFixedUpdate()
	{
		if ( !CanAct() )
		{
			Agent?.Stop();
			if ( _controller != null ) _controller.Velocity = Vector3.Zero;
			return;
		}

		if ( Agent == null ) return;

		// UpdatePosition is disabled because movement is applied below. Keep the
		// NavMeshAgent's simulated position aligned with the actual GameObject so
		// paths and avoidance don't continue from a stale position.
		Agent.SetAgentPosition( GameObject.WorldPosition );

		if ( Target == null ) return;

		Agent.MaxSpeed = StatSheet?.MoveSpeed?.Value ?? 120f;

		// Don't let AI reposition mid-swing — CombatComponent already tracks per-attack
		// CanMoveDuringStartup/Recovery, this just wasn't being read before.
		if ( IsMovementLocked() )
		{
			Agent.Stop();
			if ( _controller != null ) _controller.Velocity = Vector3.Zero;
			Agent.SetAgentPosition( GameObject.WorldPosition );
			return;
		}

		_behavior?.Tick( this, Time.Delta );
		
		float speed = Agent.Velocity.WithZ( 0 ).Length;
		bodyRenderer?.Set( "f_speed", speed > 5f ? 1f : 0f );
		bodyRenderer?.Set( "b_moving", speed > 5f );

		if ( _controller != null )
		{
			_controller.Velocity = Agent.Velocity;
		}
		else
		{
			// Fallback direct movement in case CharacterController is missing
			GameObject.WorldPosition += (Agent.Velocity + _knockbackVelocity) * Time.Delta;
		}

		Agent.SetAgentPosition( GameObject.WorldPosition );
	}

	/// <summary>
	/// Rotates to face the target regardless of movement direction. Melee wants this
	/// (movement direction ≈ target direction anyway), ranged needs it explicitly since
	/// it can be retreating while still wanting to aim at the target.
	/// </summary>
	public void FaceTarget()
	{
		if ( Target == null ) return;
		var direction = (Target.WorldPosition - GameObject.WorldPosition).WithZ( 0 ).Normal;
		if ( direction.LengthSquared > 0.01f )
		{
			var rot = Rotation.LookAt( direction, Vector3.Up );
			GameObject.WorldRotation = rot;
			//if ( GameObject.Parent != null && GameObject.Parent != Scene )
			//{
			//	GameObject.Parent.WorldRotation = rot;
			//}
		}
	}

	public float DistanceToTarget => Target == null
		? float.MaxValue
		: (Target.WorldPosition - GameObject.WorldPosition).Length;

	/// <summary>Simple ray check so ranged behaviors don't fire through walls.</summary>
	public bool HasLineOfSight()
	{
		if ( Target == null ) return false;
		var tr = Scene.Trace
			.Ray( GameObject.WorldPosition + Vector3.Up * 40f, Target.WorldPosition + Vector3.Up * 40f )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
		return !tr.Hit || tr.GameObject == Target;
	}

	public AttackDef GetAttackType()
	{
		return IsConfiguredAttack( AttackType ) ? AttackType : AttackData.Punch;
	}

	/// <summary>Called by IEnemyBehavior implementations. Was private TryPerformAttack — made
	/// public and moved the attack-def fallback out so behaviors don't need to know about it.</summary>
	public bool TryAttack( AttackDef attack )
	{
		if ( Combat == null || attack == null ) return false;
		var facing = GameObject.WorldRotation;
		var request = new AttackRequest
		{
			Attacker = GameObject,
			Attack = attack,
			SourceItem = null,
			Origin = GameObject.WorldPosition,
			Facing = facing,
			AimDirection = facing.Forward,
			TargetPoint = null,
			Charge01 = 0f,
			AlternateUse = false,
			TriggerType = AttackTriggerType.Ai
		};
		if ( Combat.TryStartAttack( request ) )
		{
			// Cooldown is now fully managed by CombatComponent — no manual timer needed.
			var bodyRenderer = Components.GetInParentOrSelf<SkinnedModelRenderer>();
			bodyRenderer?.Set( "b_attack", true );
			return true;
		}
		return false;
	}

	private bool IsMovementLocked()
	{
		var atk = Combat?.CurrentAttack;
		if ( atk == null ) return false;
		return !atk.CanMoveDuringStartup && !atk.CanMoveDuringRecovery;
	}

	private void FindPlayerTarget()
	{
		var player = Scene.GetAllComponents<Player>().FirstOrDefault();
		if ( player != null )
		{
			Target = player.GameObject;
		}
	}

	private static bool IsConfiguredAttack( AttackDef attack )
	{
		return attack != null
		       && !string.IsNullOrWhiteSpace( attack.Id )
		       && attack.Damage != null
		       && attack.Scaling != null;
	}

	/// <summary>
	/// Custom knockback receiver. 
	/// for a manually-applied (non-Punch) knockback path.
	/// s&box physics handle knockback for now
	/// </summary>
	public void ReceiveKnockback( Vector3 attackerPosition, float force )
	{
		if ( force <= 0f ) return;
		Vector3 knockbackDir = (GameObject.WorldPosition - attackerPosition).WithZ(0).Normal;
		_knockbackVelocity = knockbackDir * force;
	}
}
