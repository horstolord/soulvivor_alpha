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
		Controller.UseInputControls = false;
		Renderer.UseAnimGraph = false;
		Physics.Enabled = true;
		_isRagdolled = true;
	}

	public bool TryApplyImpulse( Vector3 impulse )
	{
		return _isRagdolled && RagdollImpulseApplier.TryApply( Physics, impulse );
	}

	public void ExitRagdoll()
	{
		_isRagdolled = false;
		Physics.Enabled = false;
		Renderer.UseAnimGraph = true;
		Controller.UseInputControls = true;
	}
}
