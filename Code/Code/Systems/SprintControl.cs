using System;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Drains stamina while the player is actually sprinting (run held + grounded + moving).
/// PlayerController has no IsRunning flag — we infer sprint from AltMoveButton + WishVelocity.
/// </summary>
public sealed class SprintControl : Component
{
	[Property] public float StaminaCostPerSecond { get; set; } = 22f;

	/// <summary>Minimum horizontal wish speed before sprint stamina starts draining.</summary>
	[Property] public float MinWishSpeed { get; set; } = 10f;

	[Property] public bool IsSprinting { get; private set; }

	/// <summary>False when stamina is empty — callers should clamp RunSpeed to WalkSpeed.</summary>
	public bool CanSprint => _statSheet == null || _statSheet.CurrentStamina > 0.05f;

	private PlayerController _playerController;
	private StatSheet _statSheet;
	private SlideControl _slide;
	private DodgeControl _dodge;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_playerController ??= Components.GetInAncestorsOrSelf<PlayerController>() ?? Components.Get<PlayerController>();
		_statSheet ??= Components.GetInAncestorsOrSelf<StatSheet>() ?? Components.Get<StatSheet>();
		_slide ??= Components.GetInAncestorsOrSelf<SlideControl>() ?? Components.Get<SlideControl>();
		_dodge ??= Components.GetInAncestorsOrSelf<DodgeControl>() ?? Components.Get<DodgeControl>();
	}

	protected override void OnUpdate()
	{
		IsSprinting = false;

		if ( _playerController == null || _statSheet == null )
		{
			CacheComponents();
			if ( _playerController == null || _statSheet == null ) return;
		}

		if ( !WantsSprint() ) return;
		if ( !_playerController.IsOnGround ) return;
		if ( _playerController.IsDucking ) return;
		if ( _slide != null && _slide.IsSliding ) return;
		if ( _dodge != null && _dodge.IsDodging ) return;

		var wishSpeed = _playerController.WishVelocity.WithZ( 0 ).Length;
		if ( wishSpeed < MinWishSpeed ) return;

		if ( !CanSprint ) return;

		IsSprinting = true;
		_statSheet.CurrentStamina = MathF.Max( 0f, _statSheet.CurrentStamina - StaminaCostPerSecond * Time.Delta );
	}

	private bool WantsSprint()
	{
		var button = string.IsNullOrWhiteSpace( _playerController.AltMoveButton )
			? "run"
			: _playerController.AltMoveButton;

		bool altHeld = Input.Down( button );
		// RunByDefault inverts the button: held = walk, released = run.
		return _playerController.RunByDefault ? !altHeld : altHeld;
	}
}
