using Sandbox;
using System;
using System.Linq;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;
namespace Sandbox.Code.Actors;
public enum EnemyMoveMode { Walk, Climb, Drop }

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
	
	[Property] public bool UseNavMesh { get; set; } = true;   // false = run straight at the target, ignore edges
	[Property] public float BodyHeight { get; set; } = 72f;
	[Property] public float BodyRadius { get; set; } = 16f;
	[Property] public float Acceleration { get; set; } = 1500f;      // also how fast knockback bleeds off
	[Property] public float GroundProbeUp { get; set; } = 50f;
	[Property] public float GroundProbeDown { get; set; } = 100f;
	[Property] public float GroundedTolerance { get; set; } = 16f;
	[Property] public float WallCheckDistance { get; set; } = 30f;
	[Property] public float MaxWallNormalZ { get; set; } = 0.3f;     // |normal.z| above this = floor/slope, not climbable
	[Property] public float ClimbSpeedMultiplier { get; set; } = 0.4f;
	[Property] public float MinTargetHeightAbove { get; set; } = 20f;
	[Property] public float MantleBoost { get; set; } = 500f;
	[Property] public float TraversalStuckTime { get; set; } = 0.75f;
	[Property] public float ProgressSampleInterval { get; set; } = 0.25f;
	[Property] public float ProgressDistance { get; set; } = 2f;
	[Property] public bool DebugMovement { get; set; }
	[Property] public float MovementDebugInterval { get; set; } = 1f;


	public NavMeshAgent Agent { get; private set; }
	public SkinnedModelRenderer bodyRenderer { get; set; }
	private IEnemyBehavior _behavior;
	private Vector3 _knockbackVelocity;
	public EnemyMoveMode MoveMode { get; private set; } = EnemyMoveMode.Walk;
	public bool IsGrounded { get; private set; }

	private Rigidbody _body;
	private float _groundGap;
	private Vector3 _moveTarget;
	private bool _hasMoveTarget;
	private Vector3 _wallNormal;
	private Vector3 _lastProgressPosition;
	private float _progressSampleTimer;
	private float _stalledTime;
	private bool _dropLeftGround;
	private float _nextMovementDebugTime;

	/// <summary>How far below the feet still counts as standing on something.</summary>
	[Property] public float GroundCheckDistance { get; set; } = 14f;
	

	protected override void OnStart()
	{
		base.OnStart();
		Combat = Components.Get<CombatComponent>();

		_body = Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
		if ( _body.IsValid() )
			_body.Locking = new PhysicsLock { Pitch = true, Yaw = true, Roll = true }; // verify the type name in your build, or set it in the prefab
		_lastProgressPosition = GameObject.WorldPosition;

		if ( UseNavMesh )
		{
			Agent = Components.GetOrCreate<NavMeshAgent>();
			Agent.UpdatePosition = false;
			Agent.UpdateRotation = false;
			Agent.Acceleration = MathF.Max( Agent.Acceleration, 1000f );
			Agent.SetAgentPosition( GameObject.WorldPosition );
		}

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
		bodyRenderer?.Set( "b_hit", StateComp?.CurrentState == ActorStateType.Staggered );

		if ( Target == null )
		{
			FindPlayerTarget();
			return;
		}
	}
	protected override void OnFixedUpdate()
	{
		if ( !_body.IsValid() || !_body.Enabled ) return; // ragdoll handler disables the root body

		if ( !CanAct() )
		{
			SetMoveMode( EnemyMoveMode.Walk );
			ResetTraversalProgress();
			return;
		}
		if ( Target == null ) return;

		float dt = Time.Delta;
		float speed = StatSheet?.MoveSpeed?.Value ?? 120f;
		bool movementLocked = IsMovementLocked();

		_hasMoveTarget = false;
		ProbeGround();
		if ( !movementLocked )
			_behavior?.Tick( this, dt );
		else if ( MoveMode != EnemyMoveMode.Drop )
			SetMoveMode( EnemyMoveMode.Walk );

		var directWish = GetDirectWishDirection();
		var navMeshWish = !movementLocked && MoveMode == EnemyMoveMode.Walk
			? GetWishDirection( speed )
			: Vector3.Zero;

		UpdateTraversalProgress( dt, movementLocked );
		UpdateMoveMode( directWish );

		if ( MoveMode == EnemyMoveMode.Climb )
			ClimbStep( speed );
		else
			WalkStep( MoveMode == EnemyMoveMode.Drop ? directWish : navMeshWish, speed, dt );

		float planar = _body.Velocity.WithZ( 0 ).Length;
		bodyRenderer?.Set( "f_speed", planar > 5f ? 1f : 0f );
		bodyRenderer?.Set( "b_moving", planar > 5f );
		LogMovementDiagnostics( speed, planar );
	}

// ---- Behavior API (replaces direct Agent access) ----
public void MoveToward( Vector3 point ) { _moveTarget = point; _hasMoveTarget = true; }
public void StopMoving() => _hasMoveTarget = false;

private Vector3 GetWishDirection( float speed )
{
	if ( !_hasMoveTarget ) return Vector3.Zero;

	if ( UseNavMesh && Agent != null )
	{
		Agent.MaxSpeed = speed;
		Agent.SetAgentPosition( GameObject.WorldPosition );
		Agent.MoveTo( _moveTarget );
		var v = Agent.Velocity.WithZ( 0 );
		return v.LengthSquared > 1f ? v.Normal : Vector3.Zero;
	}

	var d = (_moveTarget - GameObject.WorldPosition).WithZ( 0 );
	return d.LengthSquared > 1f ? d.Normal : Vector3.Zero;
}

private Vector3 GetDirectWishDirection()
{
	if ( !_hasMoveTarget ) return Vector3.Zero;

	var direction = (_moveTarget - GameObject.WorldPosition).WithZ( 0f );
	if ( direction.LengthSquared <= 1f )
		direction = GameObject.WorldRotation.Forward.WithZ( 0f );

	return direction.LengthSquared > 1f ? direction.Normal : Vector3.Zero;
}

private void UpdateTraversalProgress( float dt, bool movementLocked )
{
	var position = GameObject.WorldPosition;
	if ( !UseNavMesh || MoveMode != EnemyMoveMode.Walk || !_hasMoveTarget || movementLocked )
	{
		_stalledTime = 0f;
		_progressSampleTimer = 0f;
		_lastProgressPosition = position;
		return;
	}

	_progressSampleTimer += dt;
	if ( _progressSampleTimer < MathF.Max( 0.05f, ProgressSampleInterval ) )
		return;

	float planarProgress = (position - _lastProgressPosition).WithZ( 0f ).Length;
	if ( planarProgress >= MathF.Max( 0.1f, ProgressDistance ) )
		_stalledTime = 0f;
	else
		_stalledTime += _progressSampleTimer;

	_progressSampleTimer = 0f;
	_lastProgressPosition = position;
}

private void ResetTraversalProgress()
{
	_stalledTime = 0f;
	_progressSampleTimer = 0f;
	_lastProgressPosition = GameObject.WorldPosition;
}

private void LogMovementDiagnostics( float speed, float planarSpeed )
{
	if ( !DebugMovement || Time.Now < _nextMovementDebugTime )
		return;

	_nextMovementDebugTime = Time.Now + MathF.Max( 0.1f, MovementDebugInterval );
	float navMeshSpeed = Agent != null ? Agent.Velocity.WithZ( 0f ).Length : 0f;
	float swiftness = StatSheet?.Swiftness?.Value ?? 0f;
	Log.Info( $"[EnemyMove] {GameObject.Name}: preset={PresetOverride}, mode={MoveMode}, navmesh={UseNavMesh}, state={StateComp?.CurrentState}, " +
		$"swiftness={swiftness:F1}, moveStat={speed:F1}, agentSpeed={navMeshSpeed:F1}, bodySpeed={planarSpeed:F1}, " +
		$"grounded={IsGrounded}, stalled={_stalledTime:F2}s" );
}

private void ProbeGround()
{
	var pos = GameObject.WorldPosition;
	var tr = Scene.Trace
		.Ray( pos + Vector3.Up * GroundProbeUp, pos + Vector3.Down * GroundProbeDown )
		.IgnoreGameObjectHierarchy( GameObject )
		.Run();

	bool valid = tr.Hit && !CombatMath.ResolveActorRoot( tr.GameObject ).IsValid(); // don't stand on heads
	_groundGap = valid ? pos.z - tr.HitPosition.z : float.MaxValue;
	IsGrounded = valid && _groundGap <= GroundedTolerance;
}

private void WalkStep( Vector3 wishDir, float speed, float dt )
{
	_body.Gravity = true;
	var vel = _body.Velocity;

	// Keep horizontal control responsive while falling as well as on the ground.
	var horizontal = vel.WithZ( 0 );
	horizontal += (wishDir * speed - horizontal).ClampLength( Acceleration * dt );
	vel = horizontal.WithZ( vel.z );

	// Sink / hover correction through velocity, never teleport. Skip while being launched upward.
	if ( IsGrounded && vel.z < 20f )
		vel.z = Math.Clamp( -_groundGap * 10f, -200f, 200f );

	_body.Velocity = vel;
}

private void ClimbStep( float speed )
{
	_body.Gravity = false;
	_body.Velocity = Vector3.Up * speed * ClimbSpeedMultiplier + -_wallNormal * 40f; // 40 = keep pressed to the wall
	GameObject.WorldRotation = Rotation.LookAt( -_wallNormal, Vector3.Up );          // after Tick, so it wins over FaceTarget
}

private void UpdateMoveMode( Vector3 directWish )
{
	var pos = GameObject.WorldPosition;
	var destination = _hasMoveTarget ? _moveTarget : Target.WorldPosition;
	float targetDz = destination.z - pos.z;

	if ( MoveMode == EnemyMoveMode.Walk )
	{
		if ( UseNavMesh && _hasMoveTarget && directWish != Vector3.Zero
			&& targetDz < -MinTargetHeightAbove && HasDropAhead( directWish ) )
		{
			SetMoveMode( EnemyMoveMode.Drop );
			return;
		}

		if ( !UseNavMesh || !_hasMoveTarget || _stalledTime < TraversalStuckTime )
			return;

		if ( targetDz > MinTargetHeightAbove
			&& directWish != Vector3.Zero
			&& TryFindWall( pos + Vector3.Up * BodyHeight * 0.5f, directWish, out var normal ) )
		{
			_wallNormal = normal;
			SetMoveMode( EnemyMoveMode.Climb );
		}
		else if ( targetDz < -MinTargetHeightAbove )
			SetMoveMode( EnemyMoveMode.Drop );

		return;
	}

	if ( MoveMode == EnemyMoveMode.Climb )
	{
		if ( _hasMoveTarget && targetDz < -MinTargetHeightAbove )
		{
			SetMoveMode( EnemyMoveMode.Drop );
			return;
		}
		if ( !_hasMoveTarget || targetDz < MinTargetHeightAbove )
		{
			SetMoveMode( EnemyMoveMode.Walk );
			return;
		}

		if ( TryFindWall( pos + Vector3.Up * BodyHeight * 0.5f, -_wallNormal, out var nextNormal ) )
		{
			_wallNormal = nextNormal;
			return;
		}

		// Chest cleared the lip: hop over it and resume navmesh movement.
		SetMoveMode( EnemyMoveMode.Walk );
		_body.Velocity = -_wallNormal * (StatSheet?.MoveSpeed?.Value ?? 120f) + Vector3.Up * MantleBoost;
		return;
	}

	// Drop without checking for a safe landing. Resume navmesh steering once grounded again.
	if ( !_dropLeftGround && !IsGrounded )
		_dropLeftGround = true;

	if ( _dropLeftGround && IsGrounded )
		SetMoveMode( EnemyMoveMode.Walk );
	else if ( !_dropLeftGround && targetDz >= -MinTargetHeightAbove )
		SetMoveMode( EnemyMoveMode.Walk );
}

private bool HasDropAhead( Vector3 direction )
{
	var ahead = GameObject.WorldPosition + direction * (BodyRadius + WallCheckDistance);
	var trace = Scene.Trace
		.Ray( ahead + Vector3.Up * GroundProbeUp, ahead - Vector3.Up * GroundProbeDown )
		.IgnoreGameObjectHierarchy( GameObject )
		.Run();

	if ( !trace.Hit )
		return true;

	if ( CombatMath.ResolveActorRoot( trace.GameObject ).IsValid() )
		return false;

	return GameObject.WorldPosition.z - trace.HitPosition.z > GroundedTolerance;
}

private bool TryFindWall( Vector3 from, Vector3 dir, out Vector3 normal )
{
	normal = default;
	var tr = Scene.Trace
		.Sphere( BodyRadius * 0.5f, from, from + dir * (BodyRadius + WallCheckDistance) )
		.IgnoreGameObjectHierarchy( GameObject )
		.Run();

	if ( !tr.Hit || CombatMath.ResolveActorRoot( tr.GameObject ).IsValid() ) return false;
	if ( MathF.Abs( tr.Normal.z ) > MaxWallNormalZ ) return false;

	normal = tr.Normal.WithZ( 0 ).Normal;
	return true;
}

private void SetMoveMode( EnemyMoveMode mode )
{
	if ( MoveMode == mode ) return;

	var previousMode = MoveMode;
	MoveMode = mode;
	if ( _body.IsValid() )
		_body.Gravity = mode != EnemyMoveMode.Climb;

	if ( DebugMovement )
	{
		float targetHeightDelta = Target != null
			? Target.WorldPosition.z - GameObject.WorldPosition.z
			: 0f;
		Log.Info( $"[EnemyMove] {GameObject.Name}: {previousMode} -> {mode}, targetDz={targetHeightDelta:F1}" );
	}

	if ( mode == EnemyMoveMode.Drop )
		_dropLeftGround = false;
	else if ( mode == EnemyMoveMode.Walk )
	{
		_dropLeftGround = false;
		ResetTraversalProgress();
		if ( UseNavMesh && Agent != null )
			Agent.SetAgentPosition( GameObject.WorldPosition );
	}
}

	private bool CheckGrounded()
	{
		var from = GameObject.WorldPosition + Vector3.Up * 6f;
		var tr = Scene.Trace
			.Ray( from, from + Vector3.Down * (6f + GroundCheckDistance) )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();
		return tr.Hit;
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
		bool prefersRanged = AttackType?.Tags?.Contains( AttackTag.Ranged ) == true;
		var weaponAttack = prefersRanged
			? Equipment?.GetRangedWeaponAttackDef() ?? Equipment?.GetWeaponAttackDef()
			: Equipment?.GetWeaponAttackDef() ?? Equipment?.GetRangedWeaponAttackDef();
		if ( weaponAttack != null )
			return weaponAttack;

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
}
