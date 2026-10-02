using Sandbox.Code.Actors;

namespace Sandbox.Code.Systems;

public class gizmoscomp : Component
{
	private Enemy _enemy;

	protected override void OnStart()
	{
		base.OnStart();
		_enemy = Components.Get<Enemy>();
	}

	protected override void OnUpdate()
	{
		base.OnUpdate();

		// Lazily find the enemy if we missed it on start
		if ( _enemy == null )
		{
			_enemy = Components.Get<Enemy>();
			return; // wait another frame
		}

		var sheet = _enemy.StatSheet;

		// StatSheet component exists but InitializeFromRegistry hasn't run yet
		if ( sheet == null || sheet.MaxHealth == null )
			return;

		DrawDebugStats( sheet );
	}

	private void DrawDebugStats( StatSheet s )
	{
		Gizmo.Draw.ScreenText(
			$"Health: {s.CurrentHealth:F1}/{s.MaxHealth.Value:F1}",
			new Vector2( 100, 10 )
		);
		Gizmo.Draw.ScreenText(
			$"Energy: {s.CurrentEnergy:F1}/{s.MaxEnergy.Value:F1}",
			new Vector2( 100, 30 )
		);
		Gizmo.Draw.ScreenText(
			$"Stamina: {s.CurrentStamina:F1}/{s.MaxStamina.Value:F1}",
			new Vector2( 100, 50 )
		);
		Gizmo.Draw.ScreenText(
			$"Stagger: {s.CurrentPoise:F1}/{s.MaxPoise.Value:F1}",
			new Vector2( 100, 70 )
		);
		Gizmo.Draw.ScreenText(
			$"StaminaRegen: {s.StaminaRegen.Value:F2}/s (Swiftness: {s.Swiftness.Value:F1})",
			new Vector2( 100, 90 )
		);
	}
}
