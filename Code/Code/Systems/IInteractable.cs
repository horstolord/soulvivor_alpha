namespace Sandbox.Code.Systems;

/// <summary>
/// Defines a contract for objects in the game world that can be interacted with.
/// </summary>
public interface IInteractable
{
	/// <summary>
	/// Determines whether the given interactor is allowed to interact with this object.
	/// </summary>
	bool CanInteract( GameObject interactor );

	/// <summary>
	/// Called when the interactor interacts with this object.
	/// </summary>
	void OnInteract( GameObject interactor );
}
