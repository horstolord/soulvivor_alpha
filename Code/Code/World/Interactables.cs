using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;

public sealed class DamageTrigger : Component, Component.ITriggerListener
{
	[Property] public float DamageAmount { get; set; } = 10.0f;

	// Diese Methode wird von s&box automatisch aufgerufen, wenn ein Collider eintritt
	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		// Sicherstellen, dass das andere Objekt existiert
		if ( other == null || other.GameObject == null ) return;

		Log.Info( $"{other.GameObject.Name} hat den Trigger betreten!" );

		// Rufe ApplyDamage-Methode auf und übergib das getroffene GameObject
		ApplyDamage( other.GameObject );
	}

	// Leere Implementierung, da das Interface beide Methoden verlangt
	void Component.ITriggerListener.OnTriggerExit( Collider other ) { }

	private void ApplyDamage( GameObject target )
	{
		// Variante A: Falls dein Ziel das s&box-eigene IDamageable-Interface nutzt
		if ( target.Components.TryGet<IDamageable>( out var damageable ) )
		{
		}
	}
}
