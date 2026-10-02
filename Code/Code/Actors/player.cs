using Sandbox.Citizen;
using Sandbox.Code.Data;
using Sandbox.Code.Systems;
using HudPanel = Sandbox.Code.Presentation.UI;
namespace Sandbox.Code.Actors;
public sealed class Player : Actor
{
	
	public static Player Local { get; private set; }
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	public SlideControl Slide { get; private set; }
	public SprintControl Sprint { get; private set; }
	public HeldWeaponControl Held { get; private set; }
	public ChargeControl Charge { get; private set; }
	private PlayerController _playerController;
	
	// Load the "player" / hero stat preset from MobRegistry
	protected override string GetMobPresetId() => "player";

	// The player isn't destroyed on death — a respawn component teleports and restores it.
	protected override bool ShouldDestroyOnDeath => false;
	
	protected override void OnStart()
	{
		base.OnStart();
		MobRegistry.Initialize();
		Local = this;
		Combat = GameObject.Components.Get<CombatComponent>();
		Slide = Components.GetOrCreate<SlideControl>();
		Sprint = Components.GetOrCreate<SprintControl>();
		Charge = Components.GetOrCreate<ChargeControl>();
		_playerController = GameObject.Components.GetInAncestorsOrSelf<PlayerController>(  );
		BodyRenderer ??= Components.GetInChildren<SkinnedModelRenderer>();
		Held = Components.GetOrCreate<HeldWeaponControl>();
		Held.BodyRenderer ??= BodyRenderer;
		if ( BodyRenderer is null )
			Log.Warning( $"No BodyRenderer found on {GameObject.Name}" );
		foreach ( var r in Components.GetAll<SkinnedModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
		{
			Log.Info( $"Found renderer: {r.GameObject.Name}" );
		}
		Log.Info( $"Assigned renderer: {BodyRenderer?.GameObject?.Name ?? "NULL"}" );
		
	}

	protected override bool ShouldRegenerateStamina => Sprint == null || !Sprint.IsSprinting;

	protected override void OnUpdate()
	{
		base.OnUpdate();
		UpdatePlayerMovementStats();
		HandleFlaskHotkeys();
		if ( Combat.CanAttack )
		{
			HandleCombatInput();
		}
	}
	
	private void HandleFlaskHotkeys()
	{
		if ( Input.Keyboard.Pressed( "1" ) )
		{
			Presentation.UI.LocalInventory?.UseFlask( EquipmentSlot.Flask1 );
		}
		if ( Input.Keyboard.Pressed( "2" ) )
		{
			Presentation.UI.LocalInventory?.UseFlask( EquipmentSlot.Flask2 );
		}
	}

	public void OnEnemyKilled( Actor enemy )
	{
		Log.Info( $"[Player] Enemy slain: {enemy.GameObject.Name}. Refilling flasks!" );
		Presentation.UI.LocalInventory?.RefillFlasks( 1 );
	}

	public void ApplyFlaskEffect( ItemDef flaskDef )
	{
		if ( flaskDef?.Consumable == null || StatSheet == null ) return;

		var consumable = flaskDef.Consumable;
		if ( consumable.RestoreHealth > 0f )
		{
			float maxHp = StatSheet.MaxHealth?.Value ?? 100f;
			StatSheet.CurrentHealth = System.MathF.Min( maxHp, StatSheet.CurrentHealth + consumable.RestoreHealth );
			Log.Info( $"[Flask] Restored {consumable.RestoreHealth} HP -> {StatSheet.CurrentHealth}/{maxHp}" );
		}

		if ( consumable.RestoreEnergy > 0f )
		{
			float maxEnergy = StatSheet.MaxEnergy?.Value ?? 100f;
			StatSheet.CurrentEnergy = System.MathF.Min( maxEnergy, StatSheet.CurrentEnergy + consumable.RestoreEnergy );
			Log.Info( $"[Flask] Restored {consumable.RestoreEnergy} Mana -> {StatSheet.CurrentEnergy}/{maxEnergy}" );
		}

		if ( consumable.BuffEffect != null && Buffs != null )
		{
			Buffs.ApplyBuff( consumable.BuffEffect, StatSheet );
		}
	}

	public bool UsePotion( Presentation.InventoryItem item )
	{
		if ( item?.Definition?.Consumable == null || StatSheet == null ) return false;

		var consumable = item.Definition.Consumable;

		if ( consumable.BuffEffect != null && Buffs != null )
		{
			Buffs.ApplyBuff( consumable.BuffEffect, StatSheet );
			Log.Info( $"[Potion] Consumed '{item.Name}' — applied buff '{consumable.BuffEffect.DisplayName}'" );
		}

		if ( consumable.RestoreHealth > 0f )
		{
			float maxHp = StatSheet.MaxHealth?.Value ?? 100f;
			StatSheet.CurrentHealth = System.MathF.Min( maxHp, StatSheet.CurrentHealth + consumable.RestoreHealth );
		}

		if ( consumable.RestoreEnergy > 0f )
		{
			float maxEnergy = StatSheet.MaxEnergy?.Value ?? 100f;
			StatSheet.CurrentEnergy = System.MathF.Min( maxEnergy, StatSheet.CurrentEnergy + consumable.RestoreEnergy );
		}

		return true;
	}
	

	private void UpdatePlayerMovementStats()
	{
		if ( _playerController == null )
		{
			_playerController = Components.GetInAncestorsOrSelf<PlayerController>() ?? Components.Get<PlayerController>();
		}
		if ( _playerController != null && StatSheet != null )
		{
			float baseSpeed = StatSheet.MoveSpeed.Value + 100f;
			_playerController.WalkSpeed = baseSpeed;
			// Empty stamina collapses run into walk so holding sprint does nothing.
			bool canSprint = Sprint == null || Sprint.CanSprint;
			_playerController.RunSpeed = canSprint ? baseSpeed * 2f : baseSpeed;
			_playerController.JumpSpeed = StatSheet.JumpPower.Value * 3;
			if ( Slide == null || !Slide.IsSliding )
			{
				_playerController.DuckedSpeed = baseSpeed * 0.5f;
			}
		}
	}
	// Cached at charge-start so the swing that fires on release matches whatever weapon
	// was equipped when the hold began, even if the loadout changes mid-charge.
	private AttackDef _chargingWeaponAttack;

	private void HandleCombatInput()
	{
		if ( Combat == null )
			return;

		HandleWeaponSwingInput();

		// Tap-only for now — same hold pattern as the weapon swing below whenever these get charging too.
		if ( Input.Keyboard.Pressed( "F" ) )
		{
			TryPerformAttack( AttackData.Kick );
		}
		if ( Input.Keyboard.Pressed( "R" ) )
		{
			TryPerformAttack( AttackData.Shoot );
		}
	}

	private void HandleWeaponSwingInput()
	{
		if ( Charge == null ) return;

		bool held = Input.Keyboard.Down( "attack1" ) || Input.Keyboard.Down( "mouse1" );

		if ( Charge.IsCharging )
		{
			if ( held ) return; // still holding — keep ramping via ChargeControl.OnUpdate

			// Released. charge01 is 0 if let go instantly, which is exactly today's tap-attack case.
			float charge01 = Charge.ReleaseCharge();
			var attack = _chargingWeaponAttack;
			_chargingWeaponAttack = null;
			if ( attack == null ) return;

			TryPerformAttack( attack, charge01 );
			Held?.EnterUnarmedStance();
			BodyRenderer.Set( "b_attack", true );
			return;
		}

		if ( !held || !Charge.CanStartCharge() ) return;

		// Use equipped weapon attack; fall back to unarmed punch.
		var weaponAttack = Equipment?.GetWeaponAttackDef() ?? AttackData.Punch;

		float swiftness = StatSheet?.Swiftness.Value ?? 0f;
		float swiftnessScale = weaponAttack.Scaling?.SwiftnessToChargeSpeed ?? 0f;
		const float baseChargeSeconds = 1.0f; // TODO tune: time to max charge at 0 Swiftness scaling
		float chargeSeconds = baseChargeSeconds / (1f + swiftness * swiftnessScale / 100f);
		float rampRate = 1f / System.MathF.Max( 0.05f, chargeSeconds );

		// Charging has its own cost, additive to the swing's own cost paid as usual on release —
		// currently half the swing's stamina/energy cost, gradually over the hold. TODO tune.
		if ( Charge.StartCharge( rampRate, weaponAttack.StaminaCost * 0.5f, weaponAttack.EnergyCost * 0.5f ) )
		{
			_chargingWeaponAttack = weaponAttack;
		}
	}

	private void TryPerformAttack( AttackDef attack, float charge01 = 0f )
	{
		bool consumeBankedCharge = false;
		if ( Charge != null && Charge.TryGetBankedCharge( out var bankedCharge ) )
		{
			charge01 = System.Math.Clamp( charge01 + bankedCharge, 0f, 1f );
			consumeBankedCharge = true;
		}

		var facing = Scene.Camera?.WorldRotation ?? GameObject.WorldRotation;
		facing = Rotation.From( facing.Pitch(), facing.Yaw(), 0f );
		var request = new AttackRequest
		{
			Attacker = GameObject,
			Attack = attack,
			SourceItem = null,
			Origin = GameObject.WorldPosition,
			Facing = facing,
			AimDirection = facing.Forward,
			TargetPoint = null,
			Charge01 = charge01,
			AlternateUse = false,
			TriggerType = AttackTriggerType.PlayerInput
		};
		DebugAttackAnimation( attack );
		bool started = Combat.TryStartAttack( request );
		if ( started && consumeBankedCharge )
			Charge.TryConsumeBankedCharge( out _ );
		

	}
	private void DebugAttackAnimation( AttackDef attack )
	{
		if ( BodyRenderer == null )
		{
			Log.Warning( "Actor has no Renderer assigned." );
			return;
		}
		if ( attack == null )
		{
			Log.Warning( "Tried to play animation for null attack." );
			return;
		}
		if ( attack.AnimationName == null )
		{
			Log.Warning( $"Attack {attack.Id} has no AttackAnimation assigned." );
			return;
		}
		
		
	}
	private void DrawDebugStats()
	{
		Gizmo.Draw.ScreenText(
			$"Health: {StatSheet.CurrentHealth:F1}/{StatSheet.MaxHealth.Value:F1}",
			new Vector2( 10, 10 )
		);
		Gizmo.Draw.ScreenText(
			$"Energy: {StatSheet.CurrentEnergy:F1}/{StatSheet.MaxEnergy.Value:F1}",
			new Vector2( 10, 30 )
		);
		Gizmo.Draw.ScreenText(
			$"Stamina: {StatSheet.CurrentStamina:F1}/{StatSheet.MaxStamina.Value:F1}",
			new Vector2( 10, 50 )
		);
		Gizmo.Draw.ScreenText(
			$"Poise: {StatSheet.CurrentPoise:F1}/{StatSheet.MaxPoise.Value:F1}",
			new Vector2( 10, 70 )
		);
		Gizmo.Draw.ScreenText(
			$"StaminaRegen: {StatSheet.StaminaRegen.Value:F2}/s (Swft: {StatSheet.Swiftness.Value:F1})",
			new Vector2( 10, 90 )
		);
	}
}
