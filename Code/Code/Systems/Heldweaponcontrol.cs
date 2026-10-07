using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Owns what an actor visibly holds: spawns/removes the main-hand weapon model on the hand bone
/// and drives the citizen animgraph's "holdtype" from equipment state.
///
/// Listens to EquipmentControl.OnEquipped / OnUnequipped for the melee slot. Data comes from
/// ItemDef.Equipment.WeaponVisual. Works on any Actor with a citizen-graph SkinnedModelRenderer.
///
/// Requires CreateBoneObjects = true on the body SkinnedModelRenderer, otherwise the hold bone
/// can't be resolved and no model will be attached (a warning is logged once per equip).
///
/// Later: sheathing = reparent HeldModel between the hold bone and a hip/back bone and switch
/// holdtype to "none" — SpawnModel / ApplyHoldType are the two places that would change.
/// </summary>
public sealed class HeldWeaponControl : Component
{
	/// <summary>The body renderer that carries the citizen animgraph. Resolved from children if unset.</summary>
	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }

	/// <summary>Holdtype used while nothing is held. "none" = relaxed idle stance.</summary>
	[Property] public string UnarmedHoldType { get; set; } = "none";

	/// <summary>Holdtype entered when punching unarmed.</summary>
	[Property] public string UnarmedCombatHoldType { get; set; } = "melee_punch";

	/// <summary>The spawned weapon model, or null.</summary>
	public GameObject HeldModel { get; private set; }

	/// <summary>True while an equipped melee item has WeaponVisual data.</summary>
	public bool HasWeaponVisual => _visual != null;

	private const float RetryInterval = 0.25f;

	private EquipmentControl _equipment;
	private WeaponVisualDef _visual;
	private string _itemName;

	private string _desiredHoldType;
	private string _appliedHoldType;

	private bool _spawnSettled;          // true once spawned, or failed in a way retrying can't fix
	private bool _warnedNoBone;
	private TimeSince _timeSinceSpawnAttempt = 999f;

	// ============ LIFECYCLE ============

	protected override void OnStart()
	{
		ResolveRenderer();

		_equipment = Components.GetOrCreate<EquipmentControl>();
		_equipment.OnEquipped += HandleEquipped;
		_equipment.OnUnequipped += HandleUnequipped;

		// Pick up anything equipped before we started listening.
		Refresh( _equipment.GetEquippedWeapon() );
	}

	protected override void OnDestroy()
	{
		if ( _equipment != null )
		{
			_equipment.OnEquipped -= HandleEquipped;
			_equipment.OnUnequipped -= HandleUnequipped;
		}

		DestroyModel();
	}

	protected override void OnUpdate()
	{
		// Holdtype couldn't be applied yet (renderer not ready) or was lost — try again.
		if ( _desiredHoldType != null && _desiredHoldType != _appliedHoldType )
			ApplyHoldType();

		// Model missing (bone objects not built yet, or destroyed with a rebuilt bone hierarchy).
		if ( NeedsModel() && !HeldModel.IsValid() && _timeSinceSpawnAttempt > RetryInterval )
			TrySpawnModel();
	}

	// ============ PUBLIC API ============

	/// <summary>
	/// Called by the attack input when punching. Enters the unarmed combat stance —
	/// no-op while a weapon visual is equipped, since that stance is set on equip.
	/// </summary>
	public void EnterUnarmedStance()
	{
		if ( _visual != null ) return;
		SetHoldType( UnarmedCombatHoldType );
	}

	// ============ EQUIPMENT EVENTS ============

	private void HandleEquipped( EquipmentSlot slot, ItemInstance instance )
	{
		if ( slot != EquipmentSlot.Melee ) return;
		Refresh( instance );
	}

	private void HandleUnequipped( EquipmentSlot slot, ItemInstance instance )
	{
		if ( slot != EquipmentSlot.Melee ) return;
		Refresh( null );
	}

	/// <summary>Rebuilds the held state from the given melee item (null = empty hand).</summary>
	private void Refresh( ItemInstance weapon )
	{
		DestroyModel();

		_visual = weapon?.Definition?.Equipment?.WeaponVisual;
		_itemName = weapon?.Definition?.Name;
		_spawnSettled = false;
		_warnedNoBone = false;

		if ( _visual == null )
		{
			SetHoldType( UnarmedHoldType );
			return;
		}

		SetHoldType( _visual.HoldType );

		if ( NeedsModel() )
			TrySpawnModel();
	}

	// ============ MODEL ============

	private bool NeedsModel() =>
		_visual != null && !_spawnSettled && !string.IsNullOrWhiteSpace( _visual.PrefabPath );

	private void TrySpawnModel()
	{
		_timeSinceSpawnAttempt = 0f;

		ResolveRenderer();
		if ( BodyRenderer == null )
			return; // retry later

		var bone = BodyRenderer.GetBoneObject( _visual.HoldBone );
		if ( bone == null )
		{
			if ( !_warnedNoBone )
			{
				Log.Warning( $"[HeldWeapon] Bone '{_visual.HoldBone}' not found on {BodyRenderer.GameObject.Name}. " +
				             "Is CreateBoneObjects enabled on the body renderer? Retrying..." );
				_warnedNoBone = true;
			}
			return; // retry later
		}

		if ( !ResourceLibrary.TryGet<PrefabFile>( _visual.PrefabPath, out var prefabFile ) )
		{
			Log.Warning( $"[HeldWeapon] Could not find prefab '{_visual.PrefabPath}' for '{_itemName}'." );
			_spawnSettled = true; // retrying won't fix a bad path
			return;
		}

		var offset = new Transform( _visual.LocalPosition, Rotation.From( _visual.LocalRotation ), _visual.Scale );
		var go = SceneUtility.GetPrefabScene( prefabFile ).Clone( new CloneConfig( offset, bone, true, $"Held_{_itemName}" ) );
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;

		HeldModel = go;
		_spawnSettled = true;
		Log.Info( $"[HeldWeapon] Attached '{_itemName}' to '{_visual.HoldBone}'." );
	}

	private void DestroyModel()
	{
		if ( HeldModel.IsValid() )
			HeldModel.Destroy();

		HeldModel = null;
	}

	// ============ ANIMGRAPH ============

	private void SetHoldType( string holdType )
	{
		_desiredHoldType = holdType;
		ApplyHoldType();
	}

	private void ApplyHoldType()
	{
		if ( string.IsNullOrEmpty( _desiredHoldType ) )
		{
			_appliedHoldType = _desiredHoldType;
			return;
		}

		ResolveRenderer();
		if ( BodyRenderer == null ) return;

		// String option names are stored by the renderer and re-applied if its scene model is rebuilt.
		BodyRenderer.Set( "holdtype", _desiredHoldType );
		_appliedHoldType = _desiredHoldType;
	}

	private void ResolveRenderer()
	{
		BodyRenderer ??= Components.GetInChildren<SkinnedModelRenderer>();
	}
}
