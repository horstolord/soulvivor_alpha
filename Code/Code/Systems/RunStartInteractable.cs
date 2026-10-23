using System.Linq;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>Attach to the visible start object near the run's respawn point.</summary>
public sealed class RunStartInteractable : Component, IInteractable
{
	[Property] public SpawnDirector Director { get; set; }
	[Property] public PlayerRespawnManager SpawnPoint { get; set; }

	private SpawnDirector ResolveDirector() => Director ??= Scene?.GetAllComponents<SpawnDirector>().FirstOrDefault();
	private PlayerRespawnManager ResolveSpawnPoint() => SpawnPoint ??= Scene?.GetAllComponents<PlayerRespawnManager>().FirstOrDefault();

	public bool CanInteract( GameObject interactor ) =>
		interactor?.Components.GetInAncestorsOrSelf<Player>() != null && ResolveDirector()?.CanStartRun == true;

	public void OnInteract( GameObject interactor )
	{
		var director = ResolveDirector();
		if ( director?.StartRun( interactor, ResolveSpawnPoint() ) == true )
			Log.Info( "[RunStart] The run has begun." );
	}
}
