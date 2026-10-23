using Sandbox.Code.Actors;
using Sandbox.Code.Systems;
 
namespace Sandbox.Code.World;
 
/// <summary>
/// A shrine where the player spends banked souls on permanent soul levels (see MetaProgression).
/// Attach to any world GameObject that has a collider (InteractControl sphere-traces for it).
/// The panel itself lives in Presentation/UI.razor (UiPanel.Shrine); this component only opens it.
/// </summary>
public sealed class SoulShrine : Component, IInteractable
{
	/// <summary>The panel auto-closes when the player walks farther than this from the shrine.</summary>
	[Property] public float MaxUseDistance { get; set; } = 300f;
 
	public bool CanInteract( GameObject interactor )
	{
		return interactor?.Components.GetInAncestorsOrSelf<Player>() != null
		       && Presentation.UI.Current != null;
	}
 
	public void OnInteract( GameObject interactor )
	{
		Presentation.UI.Current?.OpenShrine( this );
	}
}
