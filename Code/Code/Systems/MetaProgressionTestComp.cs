using System.Linq;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component for the meta-progression service (pass 1, logic only).
/// Attach to any GameObject. P = deposit, O = buy a level into <see cref="Attribute"/>, T = spend a test talent point, I = log state, R = reset.
/// Safe to delete once the shrine UI exists.
/// </summary>
public sealed class MetaProgressionTestComponent : Component
{
	[Property] public float DepositAmount { get; set; } = 100f;
	[Property] public string Attribute { get; set; } = "Might";

	protected override void OnUpdate()
	{
		if ( Input.Keyboard.Pressed( "P" ) ) { MetaProgression.DepositSouls( DepositAmount ); LogState(); }
		if ( Input.Keyboard.Pressed( "O" ) )
		{
			bool ok = MetaProgression.TryLevelUp( Attribute );
			Log.Info( ok ? $"[MetaTest] Levelled {Attribute}" : $"[MetaTest] Level-up refused (cost {MetaProgression.NextLevelCost:F0}, have {MetaProgression.Souls:F0})" );
			LogState();
		}
		if ( Input.Keyboard.Pressed( "T" ) )
			Log.Info( MetaProgression.TrySpendTalentPoint( "test_talent", 3 ) ? "[MetaTest] Talent rank up" : "[MetaTest] Talent refused" );
		if ( Input.Keyboard.Pressed( "I" ) ) LogState();
		if ( Input.Keyboard.Pressed( "R" ) ) { MetaProgression.ResetProfile(); LogState(); }
	}

	private static void LogState()
	{
		Log.Info( $"[MetaTest] SoulLevel={MetaProgression.SoulLevel} Souls={MetaProgression.Souls:F0} NextCost={MetaProgression.NextLevelCost:F0} " +
		          $"AttrPts={MetaProgression.AttributePointsSpent} TalentPts={MetaProgression.TalentPointsSpent}/{MetaProgression.TalentPointsTotal} " +
		          $"[{string.Join( ", ", MetaProgression.Attributes.Select( a => $"{a}+{MetaProgression.GetAttributeBonus( a )}" ) )}]" );
	}
}
