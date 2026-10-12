using Sandbox;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test harness: slots two techniques and binds them to configurable keys.
/// Attach to the PLAYER GameObject (same object as Player/CombatComponent) — it creates the TechniqueComponent there.
/// Defaults avoid keys already in use (1/2 flasks, F kick, R shoot, E interact, C/Q/B/3-9 SpellTestComponent).
/// Safe to delete once the hotbar/tablet slots exist.
/// </summary>
public sealed class TechniqueTestComponent : Component
{
	[Property] public string Key1 { get; set; } = "Z";
	[Property] public string Key2 { get; set; } = "X";
	[Property] public string Key3 { get; set; } = "V";
	[Property] public string Key4 { get; set; } = "T";

	private TechniqueComponent _techniques;

	protected override void OnStart()
	{
		_techniques = Components.GetOrCreate<TechniqueComponent>();
		_techniques.SetSlot( 0, TechniqueLibrary.CrossSlash );
		_techniques.SetSlot( 1, TechniqueLibrary.MortalBlow );
		_techniques.SetSlot( 2, TechniqueLibrary.Whirlwind );
		_techniques.SetSlot( 3, TechniqueLibrary.LeapAttack );
	}

	protected override void OnUpdate()
	{
		if ( _techniques == null ) return;

		HandleKey( Key1, 0 );
		HandleKey( Key2, 1 );
		HandleKey( Key3, 2 );
		HandleKey( Key4, 3 );
	}

	private void HandleKey( string key, int slot )
	{
		if ( Input.Keyboard.Pressed( key ) ) Use( slot );
		// Channeled techniques run while the key is held; no-op for any other slot.
		if ( !Input.Keyboard.Down( key ) ) _techniques.ReleaseSlot( slot );
	}

	private void Use( int slot )
	{
		var result = _techniques.TryUse( slot );
		if ( result != TechniqueUseResult.Started )
			Log.Info( $"[TechniqueTest] Slot {slot}: {result}" );
	}
}
