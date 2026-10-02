using System;
using System.Linq;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

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
	// ============ TUNING ============
	/// <summary>Base credits earned per second at difficulty 1.0.</summary>
	[Property] public float BaseCreditRate   { get; set; } = 2f;

	/// <summary>Maximum credits that can be banked (prevents hoarding bursts).</summary>
	[Property] public float MaxCredits       { get; set; } = 150f;

	/// <summary>Minimum seconds between any two spawns.</summary>
	[Property] public float SpawnCooldown    { get; set; } = 1.5f;

	/// <summary>Distance from the player to spawn enemies.</summary>
	[Property] public float SpawnRadius      { get; set; } = 650f;

	/// <summary>Starting enemy cap. Grows by CapGrowthPerMin each minute.</summary>
	[Property] public int   BaseEnemyCap     { get; set; } = 8;

	/// <summary>Additional enemies allowed per minute of play time.</summary>
	[Property] public float CapGrowthPerMin  { get; set; } = 2f;

	// ============ RUNTIME ============
	private float _credits       = 0f;
	private float _elapsed       = 0f;   // total seconds since start
	private float _cooldownTimer = 0f;

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
		_elapsed += Time.Delta;
		_cooldownTimer -= Time.Delta;
		DrawGizmos();

		// Accumulate credits
		_credits = MathF.Min( _credits + BaseCreditRate * Difficulty * Time.Delta, MaxCredits );

		if ( _cooldownTimer <= 0f )
			TrySpawn();
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
			.Where( c => c.MinDifficulty <= Difficulty )
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

		// 1. Resolve this card's own prefab by path (no Editor drag needed — same
		//    convention as SoulOrb.OnStart / BeamDeliveryMethod.Deliver)
		if ( !ResourceLibrary.TryGet<PrefabFile>( card.PrefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[SpawnDirector] No prefab found at '{card.PrefabPath}' for card '{card.DisplayName}'!" );
			_cooldownTimer = SpawnCooldown;
			return;
		}

		// 2. Clone the prefab disabled using CloneConfig
		Vector3 spawnPos = PickSpawnPosition( player.GameObject.WorldPosition );
		var config = new CloneConfig( new Transform( spawnPos ), null, false );

		var spawnedGO = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
		spawnedGO.Name = card.DisplayName;

		// 3. Apply the stat preset BEFORE waking the enemy up
		var enemy = spawnedGO.Components.Get<Enemy>( FindMode.EverythingInSelfAndDescendants );
		if ( enemy != null )
		{
			enemy.PresetOverride = card.MobPresetId;
		}
		else
		{
			Log.Warning( $"[SpawnDirector] Could not find Enemy component on cloned prefab for {card.DisplayName}!" );
		}

		// 4. Now enable the cloned GameObject
		spawnedGO.Enabled = true;

		_credits -= card.Cost;
		_cooldownTimer = SpawnCooldown;

		Log.Info( $"[SpawnDirector] Spawned {card.DisplayName} (cost {card.Cost}) | credits left: {_credits:F1}" );
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
			$"Director | Diff: {Difficulty:F2} | Credits: {_credits:F1}/{MaxCredits} | Enemies: {Scene.GetAllComponents<Enemy>().Count()}/{EnemyCap}",
			new Vector2( 10, 120 )
		);
	}
}
