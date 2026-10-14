using System;
using Sandbox.Code.Systems;

namespace Sandbox.Code.Presentation;

public static class SFXControl
{
	public static void PlaySwing( AttackContext ctx )
	{
		var path = ctx.Attack.SwingSound;
		if ( string.IsNullOrWhiteSpace( path ) )
			return;

		HitShapeDef sourceShape = null;
		float reach = 0f;
		foreach ( var phase in ctx.HitPhases )
		{
			foreach ( var shape in phase.Shapes )
			{
				sourceShape ??= shape;
				reach = MathF.Max( reach, (shape.LocalOffset + shape.SweepOffset * 0.5f).Length );
			}
		}

		if ( sourceShape == null )
			return;

		float reachScale = Math.Clamp( reach / 150f, 0.5f, 2f );
		var position = ctx.Origin + ctx.Facing * sourceShape.LocalOffset;
		var handle = Sound.Play( path, position );
		handle.Pitch = Random.Shared.Float( 0.92f, 1.08f ) / MathF.Sqrt( reachScale );
		handle.Volume = Math.Clamp( 0.6f + 0.25f * reachScale + 0.3f * ctx.Charge01, 0f, 1.5f );
	}
}
