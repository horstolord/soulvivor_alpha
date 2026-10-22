using Sandbox.Citizen;
using Sandbox.Code.Systems;

namespace Sandbox.Code.Actors;

public sealed class PlayerRagdollHandler : Component, IRagdollHandler
{
	[Property] public ModelPhysics Physics { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	 
	[Property] public PlayerController Controller { get; set; }
	private bool _isRagdolled;

	protected override void OnStart()
	{
		if ( Physics != null  )
		{
			Physics.Renderer = Renderer; 
			Physics.Model = Renderer.Model;
			Physics.IgnoreRoot = true;
			Physics.Enabled = false;

		}
	}

	public void EnterRagdoll()
	{
		Log.Info( $"[Ragdoll] {GameObject.Name}: entering; wasRagdolled={_isRagdolled}, physicsEnabled={Physics?.Enabled}, input={Controller?.UseInputControls}, animGraph={Renderer?.UseAnimGraph}" );
		// Seed ModelPhysics from the live animated pose before it takes over the bones.
		Physics.CopyBonesFrom( Renderer, true );
		Controller.UseInputControls = false;
		Renderer.UseAnimGraph = false;
		Physics.Enabled = true;
		_isRagdolled = true;
		Log.Info( $"[Ragdoll] {GameObject.Name}: entered; wasRagdolled={_isRagdolled}, physicsEnabled={Physics?.Enabled}, input={Controller?.UseInputControls}, animGraph={Renderer?.UseAnimGraph}" );
	}

	public bool TryApplyImpulse( Vector3 impulse, Vector3? hitPoint = null )
	{
		return _isRagdolled && RagdollImpulseApplier.TryApply( Physics, impulse, hitPoint );
	}

	public void ExitRagdoll()
	{
		Log.Info( $"[Ragdoll] {GameObject.Name}: exiting; wasRagdolled={_isRagdolled}, physicsEnabled={Physics?.Enabled}, input={Controller?.UseInputControls}, animGraph={Renderer?.UseAnimGraph}, controller={Controller?.GameObject?.Name ?? "NULL"}, renderer={Renderer?.GameObject?.Name ?? "NULL"}" );
		Vector3? settle = null;
		if ( _isRagdolled && RagdollImpulseApplier.TryGetCenterOfMass( Physics, out var com ) )
			settle = RagdollImpulseApplier.SnapToGround( Scene, com, Controller.GameObject );

		_isRagdolled = false;
		Physics.Enabled = false;
		// ModelPhysics drives renderer bone transforms while active. Clear its overrides
		// before handing the skeleton back to the animation graph.
		Renderer.ClearPhysicsBones();
		Renderer.UseAnimGraph = true;
		Controller.UseInputControls = true;
		// Re-seed the Citizen locomotion graph immediately. Re-enabling the graph alone
		// can leave the renderer showing its last physics-driven ragdoll pose until the
		// controller next writes its animation parameters.
		Renderer.Set( "b_attack", false );
		Controller.UpdateAnimation( Renderer );

		if ( settle.HasValue ) Controller.GameObject.WorldPosition = settle.Value;
		Log.Info( $"[Ragdoll] {GameObject.Name}: exited; wasRagdolled={_isRagdolled}, physicsEnabled={Physics?.Enabled}, physicsBonesCleared=True, input={Controller?.UseInputControls}, animGraph={Renderer?.UseAnimGraph}, animationRefreshed=True, settled={settle.HasValue}" );
	}
}
