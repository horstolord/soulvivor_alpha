using Sandbox;
using System;
namespace Sandbox.Code.Presentation;
public sealed class SpellVisuals : Component
{
	[Property] public ModelRenderer MainRenderer { get; set; }
	[Property] public ParticleSystem Effect { get; set; }
	[Property] public PointLight Light { get; set; }
	[Header( "Settings" )]
	[Property] public float GrowthDuration { get; set; } = 0.25f;
	[Property] public string DissolveParameter { get; set; } = "Appearance"; // Der Name im Shader Graph
	private float _targetScale = 1.0f;
	private float _elapsed = 0f;
	private bool _isInitialized = false;
	/// <summary>
	/// Initialisiert die Optik basierend auf der Logik (Größe, Farbe)
	/// </summary>
	public void Initialize( float radius, Color color )
	{
		_targetScale = radius;
		
		// 1. Mesh Skalierung & Farbe (Element-Zuordnung)
		if ( MainRenderer.IsValid() )
		{
			MainRenderer.Tint = color;
		}
		// 2. Licht & Partikel "aufwecken"
		if ( Light.IsValid() ) Light.LightColor = color;
		
		if ( Effect.IsValid() )
		{
			Enabled = true;
			// Hier könnten wir via Effect.Set("color", color) auch die Partikelfarbe steuern,
			// sofern der Particle Graph einen entsprechenden Parameter hat. Effect. buggy?
		}
		// Initial fast unsichtbar starten für den Lerp-Effekt
		GameObject.LocalScale = 0.01f;
		_elapsed = 0f;
		_isInitialized = true;
	}
	protected override void OnUpdate()
	{
		if ( !_isInitialized ) return;
		if ( _elapsed < GrowthDuration )
		{
			_elapsed += Time.Delta;
			float progress = Math.Clamp( _elapsed / GrowthDuration, 0, 1 );
			// 2. Meshes "lerpen" (Growth)
			// Koppelt die visuelle Größe an den Fortschritt bis zur Hitbox-Größe
			GameObject.LocalScale = _targetScale * progress;
			// 2.1 Dissolve/Growth Shader Integration
			// Wenn dein Material im Shader Graph den Parameter (z.B. "Appearance") hat,
			// steuert dieser den Aufbau-Effekt des Materials.
			if ( MainRenderer.IsValid() )
			{
				// Sicherstellen, dass wir den Wert als float übergeben
				MainRenderer.Attributes.Set( DissolveParameter, (float)progress );
			}
		}
	}
}
