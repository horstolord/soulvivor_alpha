using Sandbox;

namespace Sandbox.Code.Systems;

/// <summary>
/// Runs one form of technique (swing, later leap/channel). Same interface-swap pattern as ISpellDeliveryMethod:
/// forms are picked by TechniqueDef.Form, never by subclassing.
/// </summary>
public interface ITechniqueExecutor
{
	/// <summary>Returns true if the technique actually started (costs paid, state taken).</summary>
	bool TryExecute( TechniqueContext ctx, CombatComponent combat );
}

/// <summary>
/// Swing form: compiles the context to an AttackDef and hands it to CombatComponent, which then owns
/// cost, attack-speed scaling, hit phases, charge and the Attacking state exactly like a normal swing.
/// </summary>
public sealed class SwingExecutor : ITechniqueExecutor
{
	public bool TryExecute( TechniqueContext ctx, CombatComponent combat )
	{
		if ( ctx == null || combat == null || !ctx.Caster.IsValid() ) return false;

		var request = new AttackRequest
		{
			Attacker = ctx.Caster,
			Attack = ctx.BuildAttackDef(),
			SourceItem = null,
			Origin = ctx.Origin,
			Facing = ctx.Facing,
			AimDirection = ctx.AimDirection,
			TargetPoint = null,
			Charge01 = ctx.Charge01,
			AlternateUse = false,
			TriggerType = ctx.TriggerType
		};

		return combat.TryStartAttack( request );
	}
}

public static class TechniqueExecutors
{
	// new instances rather than static fields — avoids static-initializer staleness after hotreload.
	public static ITechniqueExecutor For( TechniqueForm form ) => form switch
	{
		TechniqueForm.Swing => new SwingExecutor(),
		TechniqueForm.Channel => new ChannelExecutor(),
		// Leap has no executor: the launch + touchdown live in TechniqueComponent (it's an impulse and a state check),
		// and the landing slam is just a SwingExecutor swing built from the same definition.
		_ => null
	};
}
