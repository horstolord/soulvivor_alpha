using System.Linq;

namespace Sandbox.Code.Systems;

/// <summary>
/// Handles player interaction with world objects via an input-triggered sphere trace.
/// Attach to the player GameObject.
/// </summary>
public sealed class InteractControl : Component
{
	[Property] public float InteractRange { get; set; } = 120f;
	[Property] public float InteractRadius { get; set; } = 40f;

	private PlayerController _playerController;
	private CameraComponent _camera;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_playerController ??= Components.GetInAncestorsOrSelf<PlayerController>() ?? Components.Get<PlayerController>();
		_camera ??= Scene.Camera;
	}

	protected override void OnUpdate()
	{
		if ( _playerController == null || _camera == null )
		{
			CacheComponents();
		}

		if ( Input.Pressed( "interact" ) || Input.Pressed( "Interact" ) || Input.Keyboard.Pressed( "E" ) )
		{
			TryInteract();
		}
	}

	private void TryInteract()
	{
		var camera = _camera ?? Scene.Camera;
		var start = camera?.WorldPosition ?? (GameObject.WorldPosition + Vector3.Up * 64f);
		var forward = camera?.WorldRotation.Forward ?? GameObject.WorldRotation.Forward;
		var end = start + forward * InteractRange;

		var hits = Scene.Trace
			.Sphere( InteractRadius, start, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.RunAll()
			.OrderBy( h => h.Distance );

		foreach ( var hit in hits )
		{
			var target = hit.GameObject;
			if ( target == null )
				continue;

			var interactable = target.Components.Get<IInteractable>( FindMode.EverythingInSelfAndAncestors )
				?? target.Components.Get<IInteractable>( FindMode.EverythingInSelfAndDescendants )
				?? target.Components.GetInAncestorsOrSelf<IInteractable>()
				?? target.Parent?.Components.Get<IInteractable>()
				?? target.Components.Get<IInteractable>();
			if ( interactable == null )
				continue;

			if ( interactable.CanInteract( GameObject ) )
			{
				interactable.OnInteract( GameObject );
				break;
			}
		}
	}

	protected override void DrawGizmos()
	{
		var camera = _camera ?? Scene.Camera;
		var start = camera?.WorldPosition ?? (GameObject.WorldPosition + Vector3.Up * 64f);
		var forward = camera?.WorldRotation.Forward ?? GameObject.WorldRotation.Forward;
		var end = start + forward * InteractRange;

		Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.4f );
		Gizmo.Draw.LineSphere( start, InteractRadius );
		Gizmo.Draw.Line( start, end );
		Gizmo.Draw.LineSphere( end, InteractRadius );
	}
}
