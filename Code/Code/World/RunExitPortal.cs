using Sandbox.Code.Actors;
using Sandbox.Code.Systems;

namespace Sandbox.Code.World;

/// <summary>Interactable exit portal spawned when the timed boss dies.</summary>
public sealed class RunExitPortal : Component, IInteractable
{
	private SpawnDirector _director;
	private PlayerRespawnManager _spawnPoint;

	public void Initialize( SpawnDirector director, PlayerRespawnManager spawnPoint )
	{
		_director = director;
		_spawnPoint = spawnPoint;
	}

	public bool CanInteract( GameObject interactor ) =>
		_director?.Phase == SpawnDirector.RunPhase.Victory &&
		_spawnPoint != null && _spawnPoint.IsValid() &&
		interactor?.Components.GetInAncestorsOrSelf<Player>() != null;

	public void OnInteract( GameObject interactor )
	{
		if ( !CanInteract( interactor ) ) return;
		_director.ReturnToSpawn();
		GameObject.Destroy();
	}
}
