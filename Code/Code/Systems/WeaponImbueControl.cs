using System.Collections.Generic;
using Sandbox;

namespace Sandbox.Code.Systems;

public sealed class ImbueSpec
{
	public HashSet<AttackTag> Tags = new();
	public float BonusHealthDamage;
	public float BonusKnockback;
	public float Duration;
	public Material FxMaterial; // null = shell keeps its authored default material
}

/// <summary>
/// Timed weapon enchant (Ember Weapon etc). Lives next to Block/Dodge/ChargeControl on the actor.
/// Created on demand by SpellEffectApplier; CombatComponent.BuildContext calls Augment for weapon attacks.
/// </summary>
public sealed class WeaponImbueControl : Component
{
	public const string ShellTag = "imbue_shell";

	private ImbueSpec _spec;
	private float _expiresAt;
	private HeldWeaponControl _held;
	private GameObject _syncedModel;       // weapon model the shell state was last applied to
	private ModelRenderer _shellRenderer;

	public bool IsActive => _spec != null && Time.Now < _expiresAt;
	public float RemainingTime => IsActive ? _expiresAt - Time.Now : 0f;

	public void Apply( ImbueSpec spec )
	{
		Clear();
		if ( spec == null || spec.Duration <= 0f ) return;
		_spec = spec;
		_expiresAt = Time.Now + spec.Duration;
		SyncShell();
	}

	/// <summary>Adds the imbue to a weapon attack's damage profile. Imbue bonus is a single profile for now.</summary>
	public DamageProfileDef Augment( DamageProfileDef damage )
	{
		if ( !IsActive || damage == null ) return damage;
		var tags = new HashSet<AttackTag>( damage.Tags ?? new HashSet<AttackTag>() );
		foreach ( var tag in _spec.Tags ) tags.Add( tag );
		var result = damage.Clone();
		result.HealthDamage += _spec.BonusHealthDamage;
		result.KnockbackForce += _spec.BonusKnockback;
		result.Tags = tags;
		return result;
	}

	protected override void OnUpdate()
	{
		if ( _spec == null ) return;
		if ( !IsActive ) { Clear(); return; }

		// Re-applies after weapon swaps or a model that spawned late.
		SyncShell();
	}

	protected override void OnDestroy() => Clear();

	private void SyncShell()
	{
		_held ??= Components.GetInAncestorsOrSelf<HeldWeaponControl>();
		var model = _held?.HeldModel;
		if ( !model.IsValid() ) { _syncedModel = null; _shellRenderer = null; return; }
		if ( model == _syncedModel ) return;

		_syncedModel = model; // set first so a missing shell warns once per model, not per frame
		_shellRenderer = model.Components
			.GetAll<ModelRenderer>( FindMode.EverythingInSelfAndDescendants )
			.FirstOrDefault( r => r.GameObject.Tags.Has( ShellTag ) );

		if ( _shellRenderer == null )
		{
			Log.Warning( $"[Imbue] '{model.Name}' has no child tagged '{ShellTag}'." );
			return;
		}

		_shellRenderer.MaterialOverride = _spec.FxMaterial;
		_shellRenderer.GameObject.Enabled = true;
	}

	private void Clear()
	{
		if ( _shellRenderer.IsValid() )
		{
			_shellRenderer.MaterialOverride = null;
			_shellRenderer.GameObject.Enabled = false;
		}
		_shellRenderer = null;
		_syncedModel = null;
		_spec = null;
	}
}
