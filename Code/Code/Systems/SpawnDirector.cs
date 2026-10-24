using System;
using System.Linq;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

/// <summary>
/// Risk-of-Rain style spawn director.
/// Accumulates credits over time and spends them on affordable SpawnCards,
/// spawning enemies at a random point on a ring around the player.
///
/// Place exactly one of these anywhere in the scene. As of the multi-archetype
/// AI pass, each SpawnCard carries its own PrefabPath — there is no longer a
/// single shared EnemyPrefab field to wire up in the Inspector.
/// </summary>
public class SpawnDirector : Component
{
	public enum RunPhase { WaitingForStart, Running, BossFight, Victory }

	[Property] public float BossSpawnTime { get; set; } = 600f;
	[Property] public float BossSpawnRetryInterval { get; set; } = 3f;
	[Property] public string ExitPortalPrefabPath { get; set; } = "run_portal.prefab";

	// ============ TUNING ============
	/// <summary>Base credits earned per second at difficulty 1.0.</summary>
	[Property] public float BaseCreditRate   { get; set; } = 2f;

	/// <summary>Maximum credits that can be banked (prevents hoarding bursts).</summary>
	[Property] public float MaxCredits       { get; set; } = 150f;

	/// <summary>Minimum seconds between any two spawns.</summary>
	[Property] public float SpawnCooldown    { get; set; } = 0.1f;

	/// <summary>Distance from the player to spawn enemies.</summary>
	[Property] public float SpawnRadius      { get; set; } = 650f;

	/// <summary>Starting enemy cap. Grows by CapGrowthPerMin each minute.</summary>
	[Property] public int   BaseEnemyCap     { get; set; } = 8;

	/// <summary>Additional enemies allowed per minute of play time.</summary>
	[Property] public float CapGrowthPerMin  { get; set; } = 2f;
	[Property] public float EliteChance { get; set; } = 5f;

	// ============ RUNTIME ============
	private float _credits       = 0f;
	private float _elapsed       = 0f;   // total seconds since start
	private float _cooldownTimer = 0f;
	private float _bossRetryTimer;
	private float _runSoulsStart;
	private static float PlayerSoulsGained => Player.Local?.Leveling?.TotalSoulsGained ?? 0f;
	private float RunSouls => MathF.Max( 0f, PlayerSoulsGained - _runSoulsStart );
	private RunPhase _phase = RunPhase.WaitingForStart;
	private Enemy _boss;
	private PlayerRespawnManager _spawnPoint;
	private Vector3 _spawnPosition;
	private Rotation _spawnRotation;
	private bool _bossDeathHandled;

	public RunPhase Phase => _phase;
	public bool IsRunActive => _phase is RunPhase.Running or RunPhase.BossFight;
	public bool CanStartRun => _phase == RunPhase.WaitingForStart;

	/// <summary>
	/// Difficulty scalar. Starts at 1.0 and ramps linearly.
	/// Difficulty 1.5 is reached at ~5 min; 2.0 at ~10 min.
	/// </summary>
	public float Difficulty => 1f + (_elapsed / 60f) * 0.1f;

	/// <summary>Live enemy cap, grows with time.</summary>
	public int EnemyCap => BaseEnemyCap + (int)(_elapsed / 60f * CapGrowthPerMin);

	// ============ TICK ============
	protected override void OnUpdate()
	{
		base.OnUpdate();
		DrawGizmos();
		if ( !IsRunActive ) return;

		_elapsed += Time.Delta;
		_cooldownTimer -= Time.Delta;
		_bossRetryTimer -= Time.Delta;

		// Accumulate credits
		_credits = MathF.Min( _credits + BaseCreditRate * Difficulty * Time.Delta, MaxCredits );

		if ( _phase == RunPhase.Running && _elapsed >= BossSpawnTime && _bossRetryTimer <= 0f )
			TrySpawnBoss();

		if ( _cooldownTimer <= 0f )
			TrySpawn();
	}

	public bool StartRun( GameObject interactor, PlayerRespawnManager spawnPoint )
	{
		if ( !CanStartRun || interactor?.Components.GetInAncestorsOrSelf<Player>() == null ) return false;

		_spawnPoint = spawnPoint;
		var anchor = spawnPoint?.GameObject;
		_spawnPosition = anchor?.WorldPosition ?? interactor.WorldPosition;
		_spawnRotation = anchor?.WorldRotation ?? interactor.WorldRotation;
		_elapsed = 0f;
		_credits = 0f;
		_cooldownTimer = 0f;
		_bossRetryTimer = 0f;
		_runSoulsStart = PlayerSoulsGained;
		_boss = null;
		_bossDeathHandled = false;
		_phase = RunPhase.Running;
		Log.Info( "[SpawnDirector] Run started." );
		return true;
	}
	
	protected override void OnDestroy()
	{
		if ( _boss != null ) _boss.OnDeath -= HandleBossDeath;
	}

	// ============ SPAWNING ============
	private void TrySpawn()
	{
		int liveEnemies = Scene
			.GetAllComponents<Enemy>()
			.Count( e => e.StateComp?.CurrentState != ActorStateType.Dead );
		if ( liveEnemies >= EnemyCap ) return;

		// Filter to difficulty eligible cards
		var eligible = SpawnCardRegistry.All
			.Where( c => c.Rank != MobRank.Boss && c.MinDifficulty <= Difficulty )
			.ToList();

		if ( eligible.Count == 0 ) return;

		var card = WeightedRandom( eligible );

		// Find the player
		var player = Scene.GetAllComponents<Player>().FirstOrDefault();
		if ( player == null ) return;

		// Wait and save credits if we can't afford the rolled card
		if ( _credits < card.Cost )
		{
			_cooldownTimer = SpawnCooldown;
			return;
		}

		Vector3 spawnPos = PickSpawnPosition( player.GameObject.WorldPosition );
 
		// Elite roll: costs the regular card price for now. MobSpawner applies preset, scale and affixes before enabling.
		List<EliteAffixDef> affixes = null;
		if ( Random.Shared.NextSingle() * 100f < EliteChance )
			affixes = EliteAffixLibrary.RollAffixes();
 
		var spawnedGo = MobSpawner.Spawn( Scene, new MobSpawnRequest { Card = card, Position = spawnPos, Affixes = affixes } );
		if ( spawnedGo == null )
		{
			_cooldownTimer = SpawnCooldown; // MobSpawner already logged why (missing prefab etc.)
			return;
		}

		_credits -= card.Cost;
		_cooldownTimer = SpawnCooldown;

		Log.Info( $"[SpawnDirector] Spawned {card.DisplayName} (cost {card.Cost}) | credits left: {_credits:F1}" );
	}

	private void TrySpawnBoss()
	{
		_bossRetryTimer = MathF.Max( 0.25f, BossSpawnRetryInterval );
		var card = SpawnCardRegistry.All.FirstOrDefault( c => c.Rank == MobRank.Boss );
		if ( card == null )
		{
			Log.Error( "[SpawnDirector] No Boss-rank SpawnCard is registered." );
			return;
		}

		var player = Scene.GetAllComponents<Player>().FirstOrDefault();
		if ( player == null ) return;

		var spawned = MobSpawner.Spawn( Scene, new MobSpawnRequest
		{
			Card = card,
			Position = PickSpawnPosition( player.GameObject.WorldPosition )
		} );
		if ( spawned == null ) return; // e.g. no grounded location; retry on the next interval

		_boss = spawned.Components.Get<Enemy>( FindMode.EverythingInSelfAndDescendants );
		if ( _boss == null )
		{
			spawned.Destroy();
			Log.Error( $"[SpawnDirector] Boss prefab '{card.PrefabPath}' has no Enemy component." );
			return;
		}

		_boss.OnDeath += HandleBossDeath;
		_phase = RunPhase.BossFight;
		Log.Info( $"[SpawnDirector] Boss spawned after {_elapsed:F0}s: {card.DisplayName}." );
	}

	private void HandleBossDeath()
	{
		if ( _bossDeathHandled || _phase != RunPhase.BossFight ) return;
		_bossDeathHandled = true;
		_phase = RunPhase.Victory; // stops all further enemy spawns immediately
		if ( _boss != null ) _boss.OnDeath -= HandleBossDeath;
		float banked = RunSouls;
		MetaProgression.DepositSouls( banked );
		SpawnExitPortal();
		Log.Info( $"[SpawnDirector] Run won. Banked {banked:F0} run souls." );
	}

	private void SpawnExitPortal()
	{
		if ( string.IsNullOrWhiteSpace( ExitPortalPrefabPath ) ||
			!ResourceLibrary.TryGet<PrefabFile>( ExitPortalPrefabPath, out var prefab ) )
		{
			Log.Error( $"[SpawnDirector] Exit portal prefab '{ExitPortalPrefabPath}' was not found." );
			return;
		}

		var position = _boss?.GameObject.WorldPosition ?? _spawnPosition;
		var portal = SceneUtility.GetPrefabScene( prefab ).Clone(
			new CloneConfig( new Transform( position, Rotation.Identity ), null, false ) );
		var component = portal.Components.Get<RunExitPortal>( FindMode.EverythingInSelfAndDescendants );
		if ( component == null )
		{
			portal.Destroy();
			Log.Error( $"[SpawnDirector] Exit portal prefab '{ExitPortalPrefabPath}' has no RunExitPortal component." );
			return;
		}

		component.Initialize( this, _spawnPoint );
		portal.Enabled = true;
	}

	public void ReturnToSpawn()
	{
		if ( _phase != RunPhase.Victory ) return;
		if ( _spawnPoint != null && _spawnPoint.IsValid() )
			_spawnPoint.Respawn();
		else
			Log.Warning( "[SpawnDirector] No PlayerRespawnManager was captured; cannot return to the run start." );

		_phase = RunPhase.WaitingForStart;
		_elapsed = 0f;
		_credits = 0f;
		_cooldownTimer = 0f;
		_boss = null;
		_spawnPoint = null;
	}

	// ============ HELPERS ============
	/// Returns a point on a ring around the player at SpawnRadius distance,
	/// with ±15 % jitter so spawns aren't perfectly equidistant.

	private Vector3 PickSpawnPosition( Vector3 playerPos )
	{
		float angle  = Random.Shared.NextSingle() * MathF.Tau;
		float jitter = 0.85f + Random.Shared.NextSingle() * 0.3f;
		float r      = SpawnRadius * jitter;
		return playerPos + new Vector3( MathF.Cos( angle ) * r, MathF.Sin( angle ) * r, 0f );
	}

	/// <summary>Picks a card with probability proportional to its Weight.</summary>
	private SpawnCard WeightedRandom( List<SpawnCard> cards )
	{
		float totalWeight = 0f;
		foreach ( var c in cards ) totalWeight += c.Weight;

		float roll = Random.Shared.NextSingle() * totalWeight;
		float acc  = 0f;
		foreach ( var c in cards )
		{
			acc += c.Weight;
			if ( roll <= acc ) return c;
		}
		return cards[^1];
	}

	// ============ DEBUG GIZMOS ============
	protected override void DrawGizmos()
	{
		var player = Scene.GetAllComponents<Player>().FirstOrDefault();
		if ( player == null ) return;

		Gizmo.Draw.Color = Color.Cyan.WithAlpha( 0.3f );
		Gizmo.Draw.LineSphere( player.GameObject.WorldPosition, SpawnRadius );

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.ScreenText(
			$"Run: {_phase} | Diff: {Difficulty:F2} | Credits: {_credits:F1}/{MaxCredits} | Enemies: {Scene.GetAllComponents<Enemy>().Count()}/{EnemyCap}",
			new Vector2( 10, 120 )
		);
	}
}
