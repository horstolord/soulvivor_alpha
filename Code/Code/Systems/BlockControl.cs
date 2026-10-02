using System;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

public sealed class BlockControl : Component
{
	[Property] public float BlockReductionPercent { get; set; } = 65f;
	[Property] public float MoveSpeedPenaltyPercent { get; set; } = -40f;

	[Property] public bool IsBlocking { get; private set; } = false;

	private Actor _actor;
	private StatSheet _statSheet;

	private StatModifier _blockReductionModifier;
	private StatModifier _moveSpeedModifier;

	protected override void OnStart()
	{
		base.OnStart();
		CacheComponents();
	}

	private void CacheComponents()
	{
		_actor ??= Components.GetInAncestorsOrSelf<Actor>();
		_statSheet ??= _actor?.StatSheet ?? Components.GetInAncestorsOrSelf<StatSheet>();
	}

	protected override void OnUpdate()
	{
		if ( _actor == null || _statSheet == null )
		{
			CacheComponents();
			if ( _actor == null || _statSheet == null ) return;
		}

		if ( IsBlocking )
		{
			// Held-input release check. FLAG: confirm Input.Keyboard.Down exists/behaves
			// as expected for hold detection — only Pressed is proven-used elsewhere so far.
			if ( !Input.Keyboard.Down( "mouse2" ) )
			{
				StopBlock();
			}
			return;
		}

		if ( Input.Keyboard.Pressed( "mouse2" ) && CanBlock() )
		{
			StartBlock();
		}
	}

	public bool CanBlock()
	{
		if ( IsBlocking ) return false;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState != ActorStateType.Idle ) return false;

		return true;
	}

	public void StartBlock()
	{
		if ( !CanBlock() ) return;

		IsBlocking = true;

		if ( _actor?.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Blocking;

		if ( _statSheet?.BlockReduction != null )
		{
			_blockReductionModifier = new StatModifier( BlockReductionPercent, ModifierType.Flat, source: this );
			_statSheet.BlockReduction.AddModifier( _blockReductionModifier );
		}

		if ( _statSheet?.MoveSpeed != null )
		{
			_moveSpeedModifier = new StatModifier( MoveSpeedPenaltyPercent, ModifierType.Percent, source: this );
			_statSheet.MoveSpeed.AddModifier( _moveSpeedModifier );
		}

		Log.Info( $"[BlockControl] Block started. Reduction: {BlockReductionPercent}%, Speed penalty: {MoveSpeedPenaltyPercent}%" );
	}

	public void StopBlock()
	{
		if ( !IsBlocking ) return;

		IsBlocking = false;

		if ( _statSheet?.BlockReduction != null && _blockReductionModifier != null )
		{
			_statSheet.BlockReduction.RemoveModifier( _blockReductionModifier );
			_blockReductionModifier = null;
		}

		if ( _statSheet?.MoveSpeed != null && _moveSpeedModifier != null )
		{
			_statSheet.MoveSpeed.RemoveModifier( _moveSpeedModifier );
			_moveSpeedModifier = null;
		}

		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Blocking )
			_actor.StateComp.CurrentState = ActorStateType.Idle;

		Log.Info( "[BlockControl] Block ended." );
	}

	/// <summary>Placeholder for a future per-hit stamina drain while blocking. Not implemented for MVP.</summary>
	public void BlockCost() { }

	/// <summary>Placeholder for a future timing-based perfect-block bonus. Not implemented for MVP.</summary>
	public void PerfectBlockCheck() { }
}
