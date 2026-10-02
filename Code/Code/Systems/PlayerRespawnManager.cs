using System;
using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

/// <summary>
/// Attach this to a scene object (a spawn point / checkpoint).
/// Whenever the local player dies, wait <see cref="RespawnDelay"/> seconds, then
/// teleport them to this object's position and restore their health/stamina/energy.
///
/// The player GameObject is NOT destroyed on death (see <see cref="Actor.ShouldDestroyOnDeath"/>),
/// so we can safely reposition and revive the same object here.
/// </summary>
public sealed class PlayerRespawnManager : Component
{
	/// <summary>How long (seconds) after death before the player is teleported back.</summary>
	[Property] public float RespawnDelay { get; set; } = 2.5f;

	/// <summary>
	/// Vertical uplift applied on top of this object's position so the player doesn't
	/// spawn clipping into the floor if this object sits on it.
	/// </summary>
	[Property] public float SpawnHeightOffset { get; set; } = 20f;

	/// <summary>Whether to refill flasks after respawning.</summary>
	[Property] public bool RefillFlasksOnRespawn { get; set; } = true;

	/// <summary>If the respawn object's rotation should also be applied to the player.</summary>
	[Property] public bool MatchRotation { get; set; } = true;

	// The player we're currently watching, so we only (re)subscribe when it changes.
	private Player _watchedPlayer;
	// Remaining time until we teleport the player back.
	private float _respawnTimer;
	// True between a death being observed and the actual respawn.
	private bool _pendingRespawn;

	protected override void OnStart()
	{
		base.OnStart();
		FollowPlayer();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Keep tracking the (possibly respawned / re-placed) player.
		if ( _watchedPlayer != Player.Local )
			FollowPlayer();

		if ( !_pendingRespawn ) return;

		_respawnTimer -= Time.Delta;
		if ( _respawnTimer <= 0f )
		{
			_pendingRespawn = false;
			Respawn();
		}
	}

	/// <summary>Subscribes to the current player's death event (replacing any prior one).</summary>
	private void FollowPlayer()
	{
		if ( _watchedPlayer != null )
			_watchedPlayer.OnDeath -= HandlePlayerDeath;

		_watchedPlayer = Player.Local;

		if ( _watchedPlayer != null )
			_watchedPlayer.OnDeath += HandlePlayerDeath;
	}

	private void HandlePlayerDeath()
	{
		if ( !_pendingRespawn )
		{
			_pendingRespawn = true;
			_respawnTimer = RespawnDelay;
			Log.Info( $"[Respawn] Player died — respawning at '{GameObject.Name}' in {RespawnDelay:F1}s." );
		}
	}

	/// <summary>
	/// Teleports the player to this object and restores them. Safe to call from anywhere
	/// (for example bound to a skill, trigger, or debug key) — not only after a death.
	/// </summary>
	public void Respawn()
	{
		var player = Player.Local;
		if ( player == null || !player.IsValid() )
		{
			Log.Warning( "[Respawn] No local player to respawn." );
			return;
		}

		// 1. Move to the spawn object (with a small lift so we don't clip the floor).
		var position = GameObject.WorldPosition;
		player.GameObject.WorldPosition = position + Vector3.Up * SpawnHeightOffset;

		if ( MatchRotation )
			player.GameObject.WorldRotation = GameObject.WorldRotation;

		// 2. Reset any leftover controller velocity.
		var controller = player.Components.Get<CharacterController>( FindMode.EverythingInSelfAndDescendants );
		if ( controller != null )
			controller.Velocity = Vector3.Zero;

		// 3. Restore state and resource pools.
		if ( player.StateComp != null )
			player.StateComp.CurrentState = ActorStateType.Idle;
		//4. Restore from ragdoll
		var ragdoll = player.Components.GetInChildrenOrSelf<IRagdollHandler>();
		ragdoll?.ExitRagdoll();

		player.StatSheet?.FillCurrentPoolsToMax();
		player.StatSheet?.RecalculateDerivedStats();

		// 5. Refill flasks if configured.
		if ( RefillFlasksOnRespawn )
			Presentation.UI.LocalInventory?.RefillFlasks( 99 );

		Log.Info( $"[Respawn] Player restored at '{GameObject.Name}'." );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Green.WithAlpha( 0.6f );
		Gizmo.Draw.LineSphere( GameObject.WorldPosition, 24f );
		Gizmo.Draw.Line( GameObject.WorldPosition - Vector3.Up * 20f, GameObject.WorldPosition + Vector3.Up * 20f );
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.ScreenText( "PlayerRespawn", new Vector2( 10, 90 ) );
	}
}
