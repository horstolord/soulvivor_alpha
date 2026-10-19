using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Temporary test component: registers one proc on its actor.
/// - ExampleId set (vampiric / volatile / bloodlust / cascade): registers that ProcExamples entry.
/// - ExampleId empty: registers a configurable logging proc from the inspector values below.
/// Put it on the player for hit/crit/kill procs, or on a mob prefab for Death/HitTaken.
/// Inspector values are read once when the proc is registered — restart play after changing them.
/// Safe to delete once real procs exist.
/// </summary>
public sealed class ProcTestComponent : Component
{
	[Property] public string ExampleId { get; set; } = "";
	[Property] public float Potency { get; set; } = 1f;

	[Property] public ProcTrigger Trigger { get; set; } = ProcTrigger.HitDealt;
	[Property, Range( 0, 100 )] public float Chance { get; set; } = 50f;
	[Property] public float InternalCooldown { get; set; } = 0.5f;
	[Property] public float IntervalSeconds { get; set; } = 3f;
	[Property] public bool RequireCrit { get; set; } = false;
	[Property] public int MaxProcDepth { get; set; } = 0;

	private Actor _actor;
	private bool _registered;

	protected override void OnUpdate()
	{
		if ( _registered ) return;

		// Actor creates its ProcControl in OnStart; wait until it exists (component start order isn't guaranteed).
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		if ( _actor?.Procs == null ) return;

		var def = string.IsNullOrWhiteSpace( ExampleId ) ? null : ProcExamples.Get( ExampleId );
		if ( def == null && !string.IsNullOrWhiteSpace( ExampleId ) )
			Log.Warning( $"[ProcTest] Unknown ExampleId '{ExampleId}' — falling back to the configurable log proc." );

		def ??= new ProcDef
		{
			Id = $"test_{Trigger}",
			DisplayName = "Test Proc",
			Trigger = Trigger,
			Chance = Chance,
			InternalCooldown = InternalCooldown,
			IntervalSeconds = IntervalSeconds,
			MaxProcDepth = MaxProcDepth,
			Condition = new ProcCondition { RequireCrit = RequireCrit },
			Effects = new List<IProcEffect> { new LogProcEffect() }
		};

		_actor.Procs.AddProc( def, source: this, potency: Potency );
		_registered = true;
		Log.Info( $"[ProcTest] Registered '{def.Id}' on {_actor.GameObject.Name} (chance={def.Chance}%, icd={def.InternalCooldown}s, potency={Potency})" );
	}

	protected override void OnDestroy()
	{
		if ( _registered && _actor.IsValid() )
			_actor.Procs?.RemoveProcsFromSource( this );
	}
}
