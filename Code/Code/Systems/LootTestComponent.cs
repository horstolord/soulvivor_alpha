using System.Linq;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component for verifying the loot generation and affix rolling math.
/// Attach to any GameObject in the scene and press 'L' (or trigger from inspector) to test.
/// Safe to delete once loot drops are integrated into the enemy kill pipeline.
/// </summary>
public sealed class LootTestComponent : Component
{
	[Property] public int SourceLevel { get; set; } = 1;
	[Property] public float MobValue { get; set; } = 10f;
	[Property] public bool ForceGuaranteedRoll { get; set; } = false;

	protected override void OnUpdate()
	{
		if ( Input.Keyboard.Pressed( "L" ) )
		{
			TestRoll();
		}
	}

	public void TestRoll()
	{
		ItemInstance item;

		if ( ForceGuaranteedRoll )
		{
			var list = LootGenerator.RollMultiple( SourceLevel, MobValue, 1 );
			item = list.FirstOrDefault();
		}
		else
		{
			item = LootGenerator.RollDrop( SourceLevel, MobValue );
		}

		if ( item == null )
		{
			Log.Info( $"[LootTest] RollDrop failed drop-chance check (Level={SourceLevel}, MobValue={MobValue:F1})." );
			return;
		}

		var modsStr = item.RolledMods.Count > 0
			? string.Join( ", ", item.RolledMods.Select( m => $"{m.StatName}: +{m.Value:F2} ({m.Type})" ) )
			: "None (Common / 0 Affixes)";

		var implicitStr = string.Join( ", ", item.ImplicitMods.Select( m => $"{m.StatName}: +{m.Value:F2}" ) );
		Log.Info( $"[LootTest] Rolled {item.Rarity} '{item.Definition?.Name ?? "Unknown"}' implicits=[{implicitStr}] (Level={SourceLevel}, MobValue={MobValue:F1}) | Affixes count={item.RolledMods.Count}: [{modsStr}]" );
	}
}
