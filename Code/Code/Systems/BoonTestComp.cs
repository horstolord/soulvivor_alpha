using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component for the boon system (no UI yet). Attach to any scene object.
/// K = queue an offer (logs the choices)   Z / X / V = pick choice 1 / 2 / 3   M = clear all boons
/// P = spawn a boon pickup (of TestRank) in front of the player
/// O = force one level-up (to watch the level-up roll and pity counter)
/// Safe to delete once the offer UI exists.
/// </summary>
public sealed class BoonTestComponent : Component
{
	[Property] public ItemRarity MinRarity { get; set; } = ItemRarity.Common;
	[Property] public int RarityRolls { get; set; } = 1;
	[Property] public int ChoiceCount { get; set; } = 3;
	[Property] public MobRank TestRank { get; set; } = MobRank.Normal;

	protected override void OnUpdate()
	{
		var player = Player.Local;
		var boons = player?.Boons;
		if ( boons == null ) return;

		if ( Input.Keyboard.Pressed( "K" ) ) QueueOffer( boons );

		if ( Input.Keyboard.Pressed( "M" ) )
		{
			boons.ClearAll();
			Log.Info( "[BoonTest] Cleared all boons." );
		}

		if ( Input.Keyboard.Pressed( "P" ) )
		{
			var position = player.GameObject.WorldPosition + player.GameObject.WorldRotation.Forward * 400f + Vector3.Up * 30f;
			BoonPickup.Spawn( Scene, position, BoonGenerator.RequestForDrop( TestRank ) );
			Log.Info( $"[BoonTest] Spawned a {TestRank} boon pickup." );
		}

		if ( Input.Keyboard.Pressed( "O" ) && player.Leveling != null )
			player.Leveling.GainSouls( player.Leveling.SoulThreshold );
	}

	private void QueueOffer( BoonControl boons )
	{
		var offer = BoonGenerator.BuildOffer( new BoonOfferRequest
		{
			ChoiceCount = ChoiceCount,
			RarityRolls = RarityRolls,
			MinRarity = MinRarity,
			SourceLabel = "Test"
		}, boons );

		if ( offer == null )
		{
			Log.Info( "[BoonTest] Nothing eligible to offer." );
			return;
		}

		boons.QueueOffer( offer );
		LogCurrentOffer( boons );
	}

	private void Choose( BoonControl boons, int index )
	{
		if ( !boons.HasPendingOffer )
		{
			Log.Info( "[BoonTest] No pending offer — press K first." );
			return;
		}

		if ( boons.ChooseBoon( index ) )
		{
			var active = string.Join( ", ", boons.Active.Select( b => $"{b.Def.DisplayName} x{b.Stacks}" ) );
			Log.Info( $"[BoonTest] Active: {active}" );
			if ( boons.HasPendingOffer ) LogCurrentOffer( boons );
		}
		else
		{
			Log.Info( $"[BoonTest] Couldn't take choice {index + 1} (invalid or maxed)." );
		}
	}

	private static void LogCurrentOffer( BoonControl boons )
	{
		var offer = boons.CurrentOffer;
		if ( offer == null ) return;

		var text = string.Join( " | ", offer.Choices.Select( ( b, i ) => $"{i + 1}: {b.DisplayName} [{b.Rarity}] (have x{boons.GetStacks( b.Id )})" ) );
		Log.Info( $"[BoonTest] [{offer.SourceLabel}] Offer ({boons.PendingOfferCount} pending): {text}" );
	}
}
