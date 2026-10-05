using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;
using Sandbox.Code.Presentation;
using Sandbox.Code.World;

namespace Sandbox.Code.Systems;

public sealed class SpellComponent : Component
{
	public CatalystDef Catalyst { get; set; }
	public List<RuneDef> RuneSlots { get; set; } = new();
	/// <summary>Optional hand/staff tip anchor; when assigned it overrides the requested cast origin.</summary>
	[Property] public GameObject CastAnchor { get; set; }

	/// <summary>Fraction of the original cast time added by each non-interrupting hit.</summary>
	[Property] public float HitCastTimePenaltyPercent { get; set; } = 0.1f;
	/// <summary>Fraction of the original cost added by each non-interrupting hit.</summary>
	[Property] public float HitCastCostPenaltyPercent { get; set; } = 0.1f;
	[Property] public float MaxHitPenaltyMultiplier { get; set; } = 2f;

	private float _rechargeTimer;
	private Actor _actor;
	private ChargeControl _chargeControl;
	private PendingCast _pendingCast;
	private bool _subscribedToActor;

	private sealed class PendingCast
	{
		public SpellContext CostContext;
		public List<SpellPayload> Payloads;
		public List<CastVisualInstance> Visuals = new();
		public float BaseDuration;
		public float Duration;
		public float Elapsed;
		public float CostMultiplier = 1f;
		public float VisualProgress;
	}

	private sealed class CastVisualInstance
	{
		public GameObject GameObject;
		public Vector3 TargetScale;
	}

	protected override void OnStart()
	{
		CacheComponents();
	}

	protected override void OnDestroy()
	{
		if ( _subscribedToActor && _actor != null ) _actor.OnHitReceived -= HandleCasterHit;
		DestroyCastVisuals();
	}

	protected override void OnUpdate()
	{
		CacheComponents();
		if ( _rechargeTimer > 0f )
			_rechargeTimer = MathF.Max( 0f, _rechargeTimer - Time.Delta );

		if ( _pendingCast == null ) return;

		if ( IsInterrupted() )
		{
			CancelCast();
			return;
		}

		_pendingCast.Elapsed += Time.Delta;
		float progress = _pendingCast.Duration > 0f
			? Math.Clamp( _pendingCast.Elapsed / _pendingCast.Duration, 0f, 1f )
			: 1f;
		_pendingCast.VisualProgress = MathF.Max( _pendingCast.VisualProgress, progress );
		UpdateCastVisuals( _pendingCast.VisualProgress );

		if ( _pendingCast.Elapsed >= _pendingCast.Duration )
			CompleteCast();
	}

	private void CacheComponents()
	{
		if ( _actor == null )
		{
			_actor = Components.GetInAncestorsOrSelf<Actor>();
			if ( _actor != null && !_subscribedToActor )
			{
				_actor.OnHitReceived += HandleCasterHit;
				_subscribedToActor = true;
			}
		}
		_chargeControl ??= Components.GetInAncestorsOrSelf<ChargeControl>();
	}

	public bool CanCast()
	{
		CacheComponents();
		if ( _pendingCast != null || _rechargeTimer > 0f ) return false;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState != ActorStateType.Idle ) return false;
		return true;
	}

	/// <summary>The runes this cast will use: directly slotted runes first, then the catalyst's.</summary>
	public List<RuneDef> GetActiveRunes() => RuneSlots != null && RuneSlots.Count > 0
		? RuneSlots
		: (Catalyst?.EquippedRunes ?? new List<RuneDef>());

	public ChargeScalingDef GetChargeScaling() => RuneEvaluator.ResolveChargeScaling( GetActiveRunes() );

	/// <summary>Ramp rate for the general ChargeControl, resolved from the active method and Acuity.</summary>
	public float GetChargeRampRate()
	{
		CacheComponents();
		var scaling = GetChargeScaling();
		float acuity = _actor?.StatSheet?.Acuity?.Value ?? 0f;
		float seconds = scaling.BaseChargeSeconds / MathF.Max( 0.01f, 1f + acuity * scaling.AcuityToChargeSpeed / 100f );
		return 1f / MathF.Max( 0.05f, seconds );
	}

	/// <summary>Cheap preflight for a general charge action; the final charged cost is still checked on cast completion.</summary>
	public bool CanBeginCharge()
	{
		if ( !CanCast() ) return false;
		var runes = GetActiveRunes();
		if ( runes.Count == 0 ) return false;
		CacheComponents();
		if ( _actor == null ) return true;

		var preview = RuneEvaluator.EvaluateSequence(
			runes, GameObject, GameObject.WorldPosition, GameObject.WorldRotation.Forward );
		return preview.Success && _actor.CanPayCost( preview.ConsolidatedContext );
	}

	/// <summary>
	/// Starts a cast wind-up. If charge01 is zero and useBankedCharge is true, any banked charge is
	/// applied and consumed only after the cast has passed validation and started successfully.
	/// Costs are checked and paid at completion.
	/// </summary>
	public bool CastSpell( Vector3 origin, Vector3 aimDirection, Vector3? targetPoint = null, float charge01 = 0f, bool useBankedCharge = true )
	{
		if ( !CanCast() ) return false;

		var runes = GetActiveRunes();
		if ( runes.Count == 0 ) return false;

		CacheComponents();
		bool consumeBank = false;
		float resolvedCharge = Math.Clamp( charge01, 0f, 1f );
		if ( useBankedCharge && _chargeControl != null
			&& _chargeControl.TryGetBankedCharge( out var bankedCharge ) )
		{
			resolvedCharge = Math.Clamp( resolvedCharge + bankedCharge, 0f, 1f );
			consumeBank = true;
		}
		if ( CastAnchor.IsValid() ) origin = CastAnchor.WorldPosition;

		var evalResult = RuneEvaluator.EvaluateSequence(
			runes, GameObject, origin, aimDirection, targetPoint, charge01: resolvedCharge );
		if ( !evalResult.Success ) return false;
		ResolveProjectileSpread( evalResult.Payloads );

		float castDelay = evalResult.ConsolidatedContext.TotalCastDelay + (Catalyst?.BaseCastDelay ?? 0.1f);
		float castSpeedMultiplier = MathF.Max( 0.01f, (_actor?.StatSheet?.CastSpeed?.Value ?? 100f) / 100f );
		float baseDuration = MathF.Max( 0f, castDelay / castSpeedMultiplier );

		_pendingCast = new PendingCast
		{
			CostContext = evalResult.ConsolidatedContext.Clone(),
			Payloads = evalResult.Payloads,
			BaseDuration = baseDuration,
			Duration = baseDuration
		};

		if ( consumeBank && !_chargeControl.TryConsumeBankedCharge( out _ ) )
		{
			_pendingCast = null;
			return false;
		}

		if ( _actor?.StateComp != null )
			_actor.StateComp.CurrentState = ActorStateType.Casting;

		CreateCastVisuals();
		return true;
	}

	private static void ResolveProjectileSpread( List<SpellPayload> payloads )
	{
		foreach ( var payload in payloads )
		{
			var ctx = payload.Context;
			if ( payload.DeliveryType != RuneDeliveryType.Projectile || ctx == null || ctx.SpreadAngle <= 0.01f )
				continue;

			var rotation = Rotation.LookAt( ctx.AimDirection );
			float yaw = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			float pitch = Random.Shared.Float( -ctx.SpreadAngle, ctx.SpreadAngle );
			ctx.AimDirection = (rotation * Rotation.From( pitch, yaw, 0f )).Forward;
			ctx.SpreadAngle = 0f;
		}
	}

	private bool IsInterrupted()
	{
		if ( _actor == null ) return false;
		var state = _actor.StateComp?.CurrentState;
		return state is ActorStateType.Staggered or ActorStateType.Stunned or ActorStateType.Dead
			|| (state.HasValue && state.Value != ActorStateType.Casting);
	}

	private void HandleCasterHit()
	{
		if ( _pendingCast == null ) return;
		if ( IsInterrupted() )
		{
			CancelCast();
			return;
		}

		float cap = Math.Clamp( MaxHitPenaltyMultiplier, 1f, 2f );
		float timeCap = _pendingCast.BaseDuration * cap;
		float timeIncrement = _pendingCast.BaseDuration * MathF.Max( 0f, HitCastTimePenaltyPercent );
		_pendingCast.Duration = MathF.Min( timeCap, _pendingCast.Duration + timeIncrement );

		_pendingCast.CostMultiplier = MathF.Min(
			cap,
			_pendingCast.CostMultiplier + MathF.Max( 0f, HitCastCostPenaltyPercent ) );
	}

	private void CompleteCast()
	{
		var cast = _pendingCast;
		if ( cast == null ) return;

		var finalCost = cast.CostContext.Clone();
		finalCost.TotalHealthCost *= cast.CostMultiplier;
		finalCost.TotalStaminaCost *= cast.CostMultiplier;
		finalCost.TotalEnergyCost *= cast.CostMultiplier;

		if ( _actor != null && !_actor.CanPayCost( finalCost ) )
		{
			CancelCast();
			return;
		}

		DestroyCastVisuals();
		_pendingCast = null;

		if ( _actor != null ) _actor.PayCost( finalCost );
		foreach ( var payload in cast.Payloads )
			RuneEvaluator.ExecuteDelivery( payload );

		_rechargeTimer = Catalyst?.BaseRechargeTime ?? 1f;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Casting )
			_actor.StateComp.CurrentState = ActorStateType.Idle;
	}

	private void CancelCast()
	{
		DestroyCastVisuals();
		_pendingCast = null;
		if ( _actor?.StateComp != null && _actor.StateComp.CurrentState == ActorStateType.Casting )
			_actor.StateComp.CurrentState = ActorStateType.Idle;
	}

	private void CreateCastVisuals()
	{
		if ( _pendingCast == null ) return;

		foreach ( var payload in _pendingCast.Payloads )
		{
			if ( (payload.DeliveryType != RuneDeliveryType.Projectile && payload.DeliveryType != RuneDeliveryType.Blast)
				|| string.IsNullOrWhiteSpace( payload.PrefabPath ) )
				continue;
			if ( !ResourceLibrary.TryGet<PrefabFile>( payload.PrefabPath, out var prefabFile ) )
				continue;

			var ctx = payload.Context;
			var rotation = Rotation.LookAt( ctx.AimDirection );
			var config = new CloneConfig
			{
				Transform = new Transform( ctx.Origin, rotation ),
				Parent = CastAnchor.IsValid() ? CastAnchor : null,
				StartEnabled = false
			};
			var visual = SceneUtility.GetPrefabScene( prefabFile ).Clone( config );
			visual.Name = "SpellCastVisual";
			visual.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
			visual.WorldPosition = ctx.Origin;
			visual.WorldRotation = rotation;

			var projectile = visual.Components.Get<Projectile>( FindMode.EverythingInSelfAndDescendants );
			if ( projectile != null ) projectile.Enabled = false;
			var motion = visual.Components.Get<IProjectileMotion>( FindMode.EverythingInSelfAndDescendants );
			if ( motion is Component motionComponent ) motionComponent.Enabled = false;
			foreach ( var collider in visual.Components.GetAll<Collider>( FindMode.EverythingInSelfAndDescendants ) )
				collider.Enabled = false;
			foreach ( var soundPoint in visual.Components.GetAll<SoundPointComponent>( FindMode.EverythingInSelfAndDescendants ) )
				soundPoint.Enabled = false;

			if ( ctx.VisualMaterial != null )
			{
				var renderer = visual.Components.GetInChildren<ModelRenderer>( true );
				if ( renderer != null ) renderer.MaterialOverride = ctx.VisualMaterial;
			}

			var targetScale = visual.LocalScale;
			visual.LocalScale = targetScale * 0.01f;
			visual.Enabled = true;
			_pendingCast.Visuals.Add( new CastVisualInstance { GameObject = visual, TargetScale = targetScale } );
		}
	}

	private void UpdateCastVisuals( float progress )
	{
		if ( _pendingCast == null ) return;
		Vector3? liveOrigin = CastAnchor.IsValid() ? CastAnchor.WorldPosition : null;
		if ( liveOrigin.HasValue )
		{
			foreach ( var payload in _pendingCast.Payloads )
				payload.Context.Origin = liveOrigin.Value;
		}

		foreach ( var visual in _pendingCast.Visuals )
		{
			if ( visual.GameObject.IsValid() )
			{
				if ( liveOrigin.HasValue ) visual.GameObject.WorldPosition = liveOrigin.Value;
				visual.GameObject.LocalScale = visual.TargetScale * MathF.Max( 0.01f, progress );
			}
		}
	}

	private void DestroyCastVisuals()
	{
		if ( _pendingCast == null ) return;
		foreach ( var visual in _pendingCast.Visuals )
		{
			if ( visual.GameObject.IsValid() ) visual.GameObject.Destroy();
		}
		_pendingCast.Visuals.Clear();
	}
}
