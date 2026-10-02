using System;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public sealed class DodgeControl : Component
{
	[Property] public float DashDuration { get; set; } = 0.2f;
	[Property] public float DashSpeed { get; set; } = 900f;
	[Property] public float Cooldown { get; set; } = 0.7f;
	[Property] public float StaminaCost { get; set; } = 15f;

	[Property] public bool IsDodging { get; private set; } = false;

	public TimeSince TimeSinceDodge { get; private set; } = 999f;
	public TimeUntil DodgeEndTime { get; private set; }

	private PlayerController _playerController;
	private Actor _actor;
	private StatSheet _statSheet;

	private Vector3 _dashDirection;
	private StatModifier _evasionModifier;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_playerController ??= Components.GetInAncestorsOrSelf<PlayerController>() ?? Components.Get<PlayerController>();
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		_statSheet ??= _actor?.StatSheet ?? Components.GetInAncestorsOrSelf<StatSheet>();
	}

	protected override void OnUpdate()
	{
		if ( _playerController == null || _actor == null )
		{
			CacheComponents();
			if ( _playerController == null || _actor == null ) return;
		}

		if ( IsDodging )
		{
			if ( DodgeEndTime )
			{
				EndDodge();
			}
			return;
		}

		if ( CanDodge() && Input.Keyboard.Pressed( "alt" ) )
		{
			TryDodge();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsDodging || _playerController == null ) return;

		// WishVelocity drives the controller's own physics step — setting it in
		// OnFixedUpdate (per s&box docs) so it isn't overwritten by the controller's
		// own input read that same tick. FLAG: verify in-editor that this actually
		// overrides player steering input during the dash rather than blending with it.
		_playerController.WishVelocity = _dashDirection * DashSpeed;
	}

	public bool CanDodge()
	{
		if ( IsDodging ) return false;
		if ( TimeSinceDodge < Cooldown ) return false;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState != ActorStateType.Idle ) return false;
		if ( _playerController == null || !_playerController.IsOnGround ) return false;
		if ( StaminaCost > 0f && _statSheet != null && _statSheet.CurrentStamina < StaminaCost ) return false;

		return true;
	}

	public bool TryDodge()
	{
		if ( !CanDodge() ) return false;

		// Dash in the direction we're currently moving; fall back to facing if standing still.
		var currentVel = _playerController.Velocity.WithZ( 0 );
		_dashDirection = currentVel.LengthSquared > 0.01f
			? currentVel.Normal
			: GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;

		IsDodging = true;
		TimeSinceDodge = 0f;
		DodgeEndTime = DashDuration;

		if ( StaminaCost > 0f && _statSheet != null )
		{
			_statSheet.CurrentStamina = MathF.Max( 0f, _statSheet.CurrentStamina - StaminaCost );
		}

		if ( _actor?.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Dodging;

		ApplyIFrame();
		_playerController.PreventGrounding( DashDuration );

		Log.Info( $"[DodgeControl] Dodge initiated! Duration: {DashDuration}s, Speed: {DashSpeed}" );
		return true;
	}

	private void ApplyIFrame()
	{
		if ( _statSheet?.Evasion == null ) return;

		// Large flat modifier guarantees the existing evasion roll in Actor.ApplyDamage
		// always avoids the hit — no separate invuln-frame system needed.
		_evasionModifier = new StatModifier( 1000f, ModifierType.Flat, source: this );
		_statSheet.Evasion.AddModifier( _evasionModifier );
	}

	private void EndDodge()
	{
		IsDodging = false;

		if ( _statSheet?.Evasion != null && _evasionModifier != null )
		{
			_statSheet.Evasion.RemoveModifier( _evasionModifier );
			_evasionModifier = null;
		}

		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Dodging )
			_actor.StateComp.CurrentState = ActorStateType.Idle;

		Log.Info( "[DodgeControl] Dodge ended." );
	}

	/// <summary>Placeholder for a future timing-based reward window. Not implemented for MVP.</summary>
	public void PerfectWindowCheck() { }
}
