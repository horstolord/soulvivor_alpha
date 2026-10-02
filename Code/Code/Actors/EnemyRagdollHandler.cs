using Sandbox.Code.Systems;

namespace Sandbox.Code.Actors;

public sealed class EnemyRagdollHandler : Component, IRagdollHandler
{
	[Property] public ModelPhysics Physics { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public NavMeshAgent Agent { get; set; }
	[Property] public Actor Enemy { get; set; }
	[Property] public ModelCollider ModelCollider { get; set; }

	/// <summary>Extra velocity (as a multiple of the uniform knockback) given to the bone nearest the hit.</summary>
	[Property] public float FocusBoost { get; set; } = 1.5f;

	private bool _modelColliderWasEnabled;
	private bool _isRagdolled;

	// Cached while enabled: lookups skip disabled components, so resolving these in
	// EnterRagdoll/ExitRagdoll (after we've disabled them) would come back empty.
	private CharacterController _controller;
	private Rigidbody _rootBody;

	// Velocity/impulse that arrived before the ModelPhysics bodies existed (applied in OnUpdate).
	private bool _hasPending;
	private bool _hasPendingVelocity;
	private Vector3 _pendingVelocity;
	private Vector3 _pendingImpulse;
	private Vector3? _pendingPoint;

	protected override void OnStart()
	{
		Renderer ??= Components.GetInAncestorsOrSelf<SkinnedModelRenderer>() ?? Components.GetInChildren<SkinnedModelRenderer>();
		Physics ??= Components.GetInAncestorsOrSelf<ModelPhysics>() ?? Components.GetInChildren<ModelPhysics>();
		Agent ??= Components.GetInAncestorsOrSelf<NavMeshAgent>() ?? Components.GetInChildren<NavMeshAgent>();
		Enemy ??= Components.GetInAncestorsOrSelf<Actor>() ?? Components.GetInChildren<Actor>();
		ModelCollider ??= Components.GetInAncestorsOrSelf<ModelCollider>() ?? Components.GetInChildren<ModelCollider>();
		_modelColliderWasEnabled = ModelCollider?.Enabled ?? false;

		_controller = Components.GetInAncestorsOrSelf<CharacterController>() ?? Components.GetInChildren<CharacterController>();
		_rootBody = Components.GetInAncestorsOrSelf<Rigidbody>() ?? Components.GetInChildren<Rigidbody>();

		if ( Physics != null )
		{
			if ( Renderer != null )
			{
				Physics.Renderer = Renderer;
				Physics.Model = Renderer.Model;
			}
			// A stale prefab can serialize PhysicsWereCreated=true without serializing
			// the generated body list. ModelPhysics then skips CreatePhysics on enable,
			// leaving no bodies to drive the renderer after its animation graph stops.
			if ( Physics.PhysicsWereCreated && (Physics.Bodies == null || Physics.Bodies.Count == 0) )
				Physics.PhysicsWereCreated = false;
			Physics.IgnoreRoot = false;
			Physics.Enabled = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( _isRagdolled && _hasPending )
			FlushPending();
	}

	public void EnterRagdoll()
	{
		if ( _isRagdolled ) return; // re-entering would re-snap the pose and re-read a dead velocity

		// Read the living velocity BEFORE anything below gets disabled.
		var inherited = ReadMoveVelocity();

		// Cache the animated pose before stopping the graph. ModelPhysics applies this
		// pose to its generated bone bodies when it is enabled.
		if ( Physics != null && Renderer != null )
			Physics.CopyBonesFrom( Renderer, true );

		// Enemy creates its NavMeshAgent at runtime, after our OnStart.
		Agent ??= (Enemy as Enemy)?.Agent;
		if ( Agent != null ) Agent.Enabled = false;
		if ( Renderer != null ) Renderer.UseAnimGraph = false;
		// ModelCollider is one whole-model collider on the renderer object; it does not
		// follow individual bones and can obstruct the bone-level ragdoll colliders.
		if ( ModelCollider != null ) ModelCollider.Enabled = false;

		if ( _controller != null ) _controller.Enabled = false;
		if ( _rootBody != null ) _rootBody.Enabled = false;

		if ( Physics != null )
			Physics.Enabled = true;

		_isRagdolled = true;

		// Velocity first, impulses (arriving later) stack on top of it.
		_pendingVelocity = inherited;
		_hasPendingVelocity = true;
		_hasPending = true;
		FlushPending();
	}

	/// <summary>Best available "how fast was this thing moving" before ragdolling.</summary>
	private Vector3 ReadMoveVelocity()
	{
		if ( _controller != null )
			return _controller.Velocity;

		// No controller: Enemy moves via WorldPosition, so the Rigidbody doesn't see that motion
		// but does carry any knockback applied before this hit. The agent velocity covers the walk.
		var v = _rootBody != null ? _rootBody.Velocity : Vector3.Zero;
		var agent = Agent ?? (Enemy as Enemy)?.Agent;
		if ( agent != null ) v += agent.Velocity;
		return v;
	}

	private void FlushPending()
	{
		if ( !RagdollImpulseApplier.BodiesReady( Physics ) ) return;

		if ( _hasPendingVelocity )
			RagdollImpulseApplier.SetVelocity( Physics, _pendingVelocity );

		if ( _pendingImpulse.LengthSquared > 0.0001f )
			RagdollImpulseApplier.TryApply( Physics, _pendingImpulse, _pendingPoint, FocusBoost );

		_hasPending = false;
		_hasPendingVelocity = false;
		_pendingVelocity = Vector3.Zero;
		_pendingImpulse = Vector3.Zero;
		_pendingPoint = null;
	}

	public bool TryApplyImpulse( Vector3 impulse, Vector3? hitPoint = null )
	{
		if ( !_isRagdolled ) return false;

		// Bodies not built yet (or velocity still waiting): queue it so a killing blow isn't lost.
		if ( _hasPending || !RagdollImpulseApplier.BodiesReady( Physics ) )
		{
			_pendingImpulse += impulse;
			_pendingPoint = hitPoint ?? _pendingPoint;
			_hasPending = true;
			return true;
		}

		return RagdollImpulseApplier.TryApply( Physics, impulse, hitPoint, FocusBoost );
	}

	public void ExitRagdoll()
	{
		_isRagdolled = false;
		_hasPending = false;
		if ( Physics != null ) Physics.Enabled = false;
		if ( ModelCollider != null ) ModelCollider.Enabled = _modelColliderWasEnabled;
		if ( Renderer != null ) Renderer.UseAnimGraph = true;

		if ( _controller != null ) _controller.Enabled = true;
		if ( _rootBody != null ) _rootBody.Enabled = true;

		if ( Enemy != null ) Enemy.Enabled = true;
		if ( Agent != null ) Agent.Enabled = true;
	}
}
