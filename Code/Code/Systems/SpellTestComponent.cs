using Sandbox;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public sealed class SpellTestComponent : Component
{
	[Property] public SpellComponent SpellComp { get; set; }
	[Property] public string CurrentPreset { get; set; } = "fireball";

	protected override void OnStart()
	{
		SpellComp ??= Components.GetOrCreate<SpellComponent>();
		LoadPreset( CurrentPreset );
	}

	public void LoadPreset( string presetName )
	{
		CurrentPreset = presetName;
		if ( SpellComp != null )
		{
			SpellComp.RuneSlots = RuneLibrary.GetPreset( presetName );
			Log.Info( $"[SpellTester] Equipped preset: '{presetName}' ({SpellComp.RuneSlots.Count} runes)" );
		}
	}

	protected override void OnUpdate()
	{
		// Hotkeys to switch presets on the fly
		if ( Input.Keyboard.Pressed( "1" ) ) LoadPreset( "fireball" );
		if ( Input.Keyboard.Pressed( "2" ) ) LoadPreset( "empowered_fireball" );
		if ( Input.Keyboard.Pressed( "3" ) ) LoadPreset( "dual_frost_beam" );
		if ( Input.Keyboard.Pressed( "4" ) ) LoadPreset( "cluster_bomb" );
		if ( Input.Keyboard.Pressed( "5" ) ) LoadPreset( "raw_force" );
		if ( Input.Keyboard.Pressed( "7" ) ) LoadPreset( "airball" );
		if ( Input.Keyboard.Pressed( "8" ) ) LoadPreset( "frostball" );
		if ( Input.Keyboard.Pressed( "9" ) ) LoadPreset( "earthball" );
		if ( Input.Keyboard.Pressed( "6" ) ) LoadPreset( "fleetness" );
		if ( Input.Keyboard.Pressed( "Q" ) ) LoadPreset( "ember_weapon" );
		if ( Input.Keyboard.Pressed( "E" ) ) LoadPreset( "launch" );
		if ( Input.Keyboard.Pressed( "R" ) ) LoadPreset( "shockwave" );
		if ( Input.Keyboard.Pressed( "B" ) ) LoadPreset( "blast" );

		// Cast on 'C' key
		if ( Input.Keyboard.Pressed( "C" )  )
		{
			CastCurrentSpell();
		}
	}

	public void CastCurrentSpell()
	{
		if ( SpellComp == null ) return;

		var cameraRot = Scene.Camera?.WorldRotation ?? GameObject.WorldRotation;
		var cameraPos = Scene.Camera?.WorldPosition ?? (GameObject.WorldPosition + Vector3.Up * 50f);
		var aimDir = cameraRot.Forward;

		bool castResult = SpellComp.CastSpell( cameraPos + aimDir * 30f, aimDir );
		if ( castResult )
		{
			Log.Info( $"[SpellTester] Started cast for preset '{CurrentPreset}'." );
		}
	}
}
