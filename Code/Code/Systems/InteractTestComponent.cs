namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component for verifying the interaction system.
/// Attach to any world GameObject (e.g. a test cube) to confirm the trace and keybind work in-editor.
/// This component is temporary and safe to delete once concrete interactables are implemented.
/// </summary>
public sealed class InteractTestComponent : Component, IInteractable
{
	public bool CanInteract( GameObject interactor )
	{
		return true;
	}

	public void OnInteract( GameObject interactor )
	{
		Log.Info( $"[InteractTestComponent] Interacted with {GameObject.Name} by {interactor?.Name ?? "Unknown"}!" );
	}
}
