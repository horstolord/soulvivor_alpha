using Sandbox;

public sealed class TrueFirstPersonController : Component
{
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public CameraComponent Camera { get; set; }

	private int _headBoneIndex = -1;

	protected override void OnStart()
	{
		if ( BodyRenderer is null || BodyRenderer.Model is null ) return;

		// 1. Force the renderer to spawn GameObjects for each bone in the skeleton
		BodyRenderer.CreateBoneObjects = true;

		// 2. Loop through the model's bones to find the index of the "head" bone
		for ( int i = 0; i < BodyRenderer.Model.BoneCount; i++ )
		{
			if ( BodyRenderer.Model.GetBoneName( i ) == "head" )
			{
				_headBoneIndex = i;
				break;
			}
		}
	}

	// OnPreRender runs after animations have updated for the frame,
	// ensuring the camera tracks the eyes with zero latency.
	protected override void OnPreRender()
	{
		if ( Camera is null || BodyRenderer is null ) return;

		// 1. Move Camera to Eyes Attachment
		if ( BodyRenderer.GetAttachment( "eyes" ) is Transform eyeTx )
		{
			Camera.Transform.Position = eyeTx.Position;
		}

		// 2. Hide Head Mesh
		// Instead of SetBoneTransform, we directly scale the bone's spawned GameObject.
		if ( _headBoneIndex != -1 )
		{
			var headGo = BodyRenderer.GetBoneObject( _headBoneIndex );
			if ( headGo is not null )
			{
				// Shrinking the bone GameObject scales the head mesh vertices down to 0
				headGo.Transform.LocalScale = 0.001f;
			}
		}
	}
}
