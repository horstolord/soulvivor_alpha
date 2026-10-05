using System.Collections.Generic;
using Sandbox;

namespace Sandbox.Code.Systems;

public sealed class ImbueSpec
{
	public HashSet<AttackTag> Tags = new();
	public float BonusHealthDamage;
	public float BonusKnockback;
	public float Duration;
	public string FxPrefabPath;
	public Material FxMaterial;
}

/// <summary>
/// Timed weapon enchant (Ember Weapon etc). Lives next to Block/Dodge/ChargeControl on the actor.
/// Created on demand by SpellEffectApplier; CombatComponent.BuildContext calls Augment for weapon attacks.
/// </summary>
public sealed class WeaponImbueControl : Component
{
	private ImbueSpec _spec;
	private float _expiresAt;
	private GameObject _fx;

	public bool IsActive => _spec != null && Time.Now < _expiresAt;
	public float RemainingTime => IsActive ? _expiresAt - Time.Now : 0f;

	public void Apply( ImbueSpec spec )
	{
		Clear();
		if ( spec == null || spec.Duration <= 0f ) return;
		_spec = spec;
		_expiresAt = Time.Now + spec.Duration;
		var weapon = GetWeaponModel();
		_fx = SpellVfx.Spawn( spec.FxPrefabPath, (weapon ?? GameObject).WorldPosition, (weapon ?? GameObject).WorldRotation,
			Vector3.One, spec.FxMaterial, weapon ?? GameObject, autoBurst: false );
	}

	/// <summary>Adds the imbue to a weapon attack's damage profile. Imbue bonus is a single profile for now.</summary>
	public DamageProfileDef Augment( DamageProfileDef damage )
	{
		if ( !IsActive || damage == null ) return damage;
		var tags = new HashSet<AttackTag>( damage.Tags ?? new HashSet<AttackTag>() );
		foreach ( var tag in _spec.Tags ) tags.Add( tag );
		return new DamageProfileDef
		{
			HealthDamage = damage.HealthDamage + _spec.BonusHealthDamage,
			StaminaDamage = damage.StaminaDamage,
			KnockbackForce = damage.KnockbackForce + _spec.BonusKnockback,
			Tags = tags,
			IsCrit = damage.IsCrit
		};
	}

	protected override void OnUpdate()
	{
		if ( _spec == null ) return;
		if ( !IsActive ) { Clear(); return; }

		// Follow the weapon if it was swapped or only appeared after the cast.
		var weapon = GetWeaponModel();
		if ( _fx.IsValid() && weapon.IsValid() && _fx.Parent != weapon )
			_fx.SetParent( weapon, false );
	}

	protected override void OnDestroy() => Clear();

	private GameObject GetWeaponModel()
	{
		var held = Components.GetInAncestorsOrSelf<HeldWeaponControl>();
		return held != null && held.HeldModel.IsValid() ? held.HeldModel : null;
	}

	private void Clear()
	{
		if ( _fx.IsValid() ) _fx.Destroy();
		_fx = null;
		_spec = null;
	}
}
