using System;
using Sandbox;
using Sandbox.Citizen;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public sealed class SlideControl : Component
{
	[Property] public float SlideDuration { get; set; } = 5f;
	[Property] public float BurstWindow { get; set; } = 0.2f;
	[Property] public float SpeedBoost { get; set; } = 750f;
	[Property] public float SlideDuckedSpeed { get; set; } = 10f;
	[Property] public float MinSpeedThreshold { get; set; } = 10f;
	[Property] public float Cooldown { get; set; } = 0.6f;
	[Property] public float StaminaCost { get; set; } = 0f;

	/// <summary>How long Foot IK is enabled after the slide ends to absorb the transition jerk.</summary>
	[Property] public float FootIkExitDuration { get; set; } = 1f;

	[Property] public bool IsSliding { get; private set; } = false;

	public TimeSince TimeSinceSlide { get; private set; } = 999f;
	public TimeUntil SlideEndTime { get; private set; }

	private PlayerController _playerController;
	private StatSheet _statSheet;
	private SkinnedModelRenderer _bodyRenderer;
	private CitizenAnimationHelper _animHelper;

	private Vector3 _slideDirection;
	private float _burstTargetSpeed;
	private float _burstElapsed;

	private TimeSince _timeSinceSlideEnd = 999f;
	private bool _isFootIkActive = false;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_playerController ??= Components.GetInAncestorsOrSelf<PlayerController>() ?? Components.Get<PlayerController>();
		_statSheet ??= Components.GetInAncestorsOrSelf<StatSheet>() ?? Components.Get<StatSheet>();
		_animHelper ??= Components.GetInChildren<CitizenAnimationHelper>() ?? Components.Get<CitizenAnimationHelper>();
		_bodyRenderer ??= Components.GetInChildren<SkinnedModelRenderer>() ?? Components.Get<SkinnedModelRenderer>();
	}

	protected override void OnUpdate()
	{
		if ( _playerController == null )
		{
			CacheComponents();
			if ( _playerController == null ) return;
		}

		// Handle the temporary Foot IK window on exit
		if ( _isFootIkActive )
		{
			if ( _timeSinceSlideEnd > FootIkExitDuration )
			{
				SetFootIk( false );
				_isFootIkActive = false;
			}
			else
			{
				SetFootIk( true );
			}
		}

		if ( IsSliding )
		{
			SetAnimationState( CitizenAnimationHelper.SpecialMoveStyle.Slide, 1f );

			var currentSpeed = _playerController.Velocity.WithZ( 0 ).Length;
			if ( SlideEndTime || currentSpeed < MinSpeedThreshold )
			{
				EndSlide();
			}
			return;
		}

		if ( CanSlide() && (Input.Pressed( "duck" ) || Input.Pressed( "crouch" )) )
		{
			StartSlide();
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( _playerController == null ) return;

		if ( IsSliding )
		{
			_playerController.IsDucking = true;

			if ( _burstElapsed < BurstWindow )
			{
				_burstElapsed += Time.Delta;
				_playerController.WishVelocity = _slideDirection * _burstTargetSpeed;
			}
		}
	}

	public bool CanSlide()
	{
		if ( IsSliding ) return false;
		if ( TimeSinceSlide < Cooldown ) return false;
		if ( _playerController == null ) return false;
		if ( !_playerController.IsOnGround ) return false;

		var horizontalSpeed = _playerController.Velocity.WithZ( 0 ).Length;
		if ( horizontalSpeed < MinSpeedThreshold ) return false;

		if ( StaminaCost > 0f && _statSheet != null && _statSheet.CurrentStamina < StaminaCost )
			return false;

		return true;
	}

	public void StartSlide()
	{
		IsSliding = true;
		TimeSinceSlide = 0f;
		SlideEndTime = SlideDuration;
		_burstElapsed = 0f;

		// Enable Foot IK for the duration of the slide
		_isFootIkActive = true;
		SetFootIk( true );

		var currentVel = _playerController.Velocity.WithZ( 0 );
		_slideDirection = currentVel.LengthSquared > 0.01f
			? currentVel.Normal
			: GameObject.WorldRotation.Forward.WithZ( 0 ).Normal;
		_burstTargetSpeed = currentVel.Length + SpeedBoost;

		if ( StaminaCost > 0f && _statSheet != null )
		{
			_statSheet.CurrentStamina = MathF.Max( 0f, _statSheet.CurrentStamina - StaminaCost );
		}

		_playerController.DuckedSpeed = SlideDuckedSpeed;
		_playerController.IsDucking = true;

		SetAnimationState( CitizenAnimationHelper.SpecialMoveStyle.Slide, 1f );
	}

	public void EndSlide()
	{
		IsSliding = false;

		// Toggle b_grounded on the player at the beginning of end slide
		if ( _bodyRenderer.IsValid() )
		{
			_bodyRenderer.Set( "b_grounded", false );
		}

		if ( _statSheet != null && _playerController != null )
		{
			_playerController.DuckedSpeed = (_statSheet.MoveSpeed.Value + 100f) * 0.5f;
		}

		// Reset animation back to normal locomotion / standing
		SetAnimationState( CitizenAnimationHelper.SpecialMoveStyle.None, 1f );

		// Foot IK remains active for FootIkExitDuration (already enabled during slide)
		_timeSinceSlideEnd = 0f;
		// _isFootIkActive stays true - will be disabled in OnUpdate after FootIkExitDuration

		// Hand ducking control straight back to normal input
		_playerController.IsDucking = Input.Down( "duck" ) || Input.Down( "crouch" );
	}

	private void SetFootIk( bool enabled )
	{
		if ( !_bodyRenderer.IsValid() ) return;

		_bodyRenderer.Set( "b_foot_ik", enabled );
		_bodyRenderer.Set( "ik.feet.enabled", enabled );
		_bodyRenderer.Set( "ik.foot_left.enabled", enabled );
		_bodyRenderer.Set( "ik.foot_right.enabled", enabled );
	}

	private void SetAnimationState( CitizenAnimationHelper.SpecialMoveStyle moveStyle, float duckLevel )
	{
		if ( _animHelper.IsValid() )
		{
			_animHelper.SpecialMove = moveStyle;
			_animHelper.DuckLevel = duckLevel;
		}
		else if ( _bodyRenderer.IsValid() )
		{
			_bodyRenderer.Set( "special_movement_states", (int)moveStyle );
			_bodyRenderer.Set( "duck", duckLevel );
		}
	}
}
