using System;
using Sandbox.Code.Actors;
using Sandbox.Code.Data;

namespace Sandbox.Code.Systems;

/// <summary>
/// Holds the actor's run-scoped boons and the queue of pending boon offers.
/// Boons apply their stat modifiers through StatSheet and their procs through ProcControl, both keyed
/// by the boon's entry as the source, so removal is always clean. Cleared when the actor dies
/// (until a run loop exists to own that). Also rolls the level-up boon offer, with pity.
/// </summary>
public sealed class BoonControl : Component
{
	private sealed class BoonEntry
	{
		public BoonDef Def;
		public int Stacks;
		public List<(Stat Stat, StatModifier Modifier)> AppliedModifiers = new();
	}

	// ============ LEVEL-UP ROLL (placeholders — tune freely) ============

	/// <summary>Base percent chance that a level-up grants a boon offer.</summary>
	[Property] public float LevelUpOfferChance { get; set; } = 15f;

	/// <summary>Pity: extra percent chance added per consecutive level-up without an offer.</summary>
	[Property] public float PityChancePerMiss { get; set; } = 5f;

	/// <summary>Pity: after this many consecutive misses the next level-up is guaranteed. 0 = no guarantee.</summary>
	[Property] public int PityGuaranteeAfter { get; set; } = 8;

	/// <summary>Consecutive level-ups so far without a boon offer (resets on an offer and on ClearAll).</summary>
	public int LevelUpMisses => _levelUpMisses;

	/// <summary>Fired when a boon's stack count changes. Stacks = 0 means it was removed.</summary>
	public event Action<BoonDef, int> OnBoonChanged;

	/// <summary>Fired when the offer queue changes (queued, resolved, skipped, cleared). UI hook.</summary>
	public event Action OnOffersChanged;

	private readonly Dictionary<string, BoonEntry> _boons = new();
	private readonly Queue<BoonOffer> _offers = new();
	private Actor _actor;
	private bool _subscribed;
	private bool _levelSubscribed;
	private int _levelUpMisses;

	// ============ QUERIES ============

	public BoonOffer CurrentOffer => _offers.Count > 0 ? _offers.Peek() : null;
	public bool HasPendingOffer => _offers.Count > 0;
	public int PendingOfferCount => _offers.Count;

	public IEnumerable<(BoonDef Def, int Stacks)> Active => _boons.Values.Select( e => (e.Def, e.Stacks) );

	public int GetStacks( string id ) => id != null && _boons.TryGetValue( id, out var entry ) ? entry.Stacks : 0;

	public bool CanAdd( BoonDef def )
	{
		if ( def == null ) return false;
		return def.MaxStacks <= 0 || GetStacks( def.Id ) < def.MaxStacks;
	}

	// ============ BOONS ============

	/// <summary>Adds one stack of the boon. Returns false if it is maxed or the actor isn't ready yet.</summary>
	public bool AddBoon( BoonDef def )
	{
		if ( def == null || !CanAdd( def ) ) return false;

		CacheActor();
		var sheet = _actor?.StatSheet;
		if ( sheet == null || sheet.Armor == null || _actor.Procs == null )
		{
			Log.Warning( $"[BoonControl] {GameObject.Name} isn't initialized yet — can't add '{def.DisplayName}'." );
			return false;
		}

		bool isNew = !_boons.TryGetValue( def.Id, out var entry );
		if ( isNew )
		{
			entry = new BoonEntry { Def = def };
			_boons[def.Id] = entry;
		}

		entry.Stacks++;
		ApplyEntry( entry, sheet, isNew );

		Log.Info( $"[BoonControl] '{def.DisplayName}' is now x{entry.Stacks}." );
		OnBoonChanged?.Invoke( def, entry.Stacks );
		return true;
	}

	/// <summary>Strips every boon, drops any pending offers and resets pity.</summary>
	public void ClearAll()
	{
		var sheet = _actor?.StatSheet;
		bool touchedAttribute = false;
		var removed = new List<BoonDef>();

		foreach ( var entry in _boons.Values )
		{
			StatModApplier.Remove( entry.AppliedModifiers );
			_actor?.Procs?.RemoveProcsFromSource( entry );

			if ( StatModApplier.TouchesAttribute( entry.Def.Modifiers ) ) touchedAttribute = true;
			removed.Add( entry.Def );
		}

		bool hadOffers = _offers.Count > 0;
		_boons.Clear();
		_offers.Clear();
		_levelUpMisses = 0;

		if ( touchedAttribute && sheet != null )
			sheet.RecalculateDerivedStats();

		foreach ( var def in removed )
			OnBoonChanged?.Invoke( def, 0 );
		if ( hadOffers )
			OnOffersChanged?.Invoke();
	}

	// ============ OFFERS ============

	public void QueueOffer( BoonOffer offer )
	{
		if ( offer == null || offer.Choices.Count == 0 ) return;
		_offers.Enqueue( offer );
		OnOffersChanged?.Invoke();
	}

	/// <summary>Picks choice <paramref name="index"/> of the current offer. False if invalid or the boon got maxed meanwhile.</summary>
	public bool ChooseBoon( int index )
	{
		var offer = CurrentOffer;
		if ( offer == null || index < 0 || index >= offer.Choices.Count ) return false;

		if ( !AddBoon( offer.Choices[index] ) ) return false;

		_offers.Dequeue();
		OnOffersChanged?.Invoke();
		return true;
	}

	/// <summary>Discards the current offer without picking (for when every choice is unavailable).</summary>
	public void SkipOffer()
	{
		if ( _offers.Count == 0 ) return;
		_offers.Dequeue();
		OnOffersChanged?.Invoke();
	}

	// ============ LEVEL-UP ROLL ============

	private void HandleLevelUp( int newLevel )
	{
		bool guaranteed = PityGuaranteeAfter > 0 && _levelUpMisses >= PityGuaranteeAfter;
		float chance = LevelUpOfferChance + PityChancePerMiss * _levelUpMisses;

		if ( guaranteed || Random.Shared.NextSingle() * 100f < chance )
		{
			Log.Info( $"[BoonControl] Level {newLevel}: boon offer! (chance {chance:F0}%, misses before: {_levelUpMisses}{(guaranteed ? ", pity guarantee" : "")})" );
			_levelUpMisses = 0;
			QueueOffer( BoonGenerator.BuildOffer( BoonGenerator.RequestForLevelUp(), this ) );
		}
		else
		{
			_levelUpMisses++;
			Log.Info( $"[BoonControl] Level {newLevel}: no boon (chance {chance:F0}%, misses now: {_levelUpMisses})" );
		}
	}

	// ============ INTERNALS ============

	private void ApplyEntry( BoonEntry entry, StatSheet sheet, bool isNew )
	{
		// Stat modifiers: Stat caches its value and RemoveModifier/AddModifier mark it dirty, so rebuild
		// them at value x stacks rather than mutating StatModifier.Value in place.
		StatModApplier.Remove( entry.AppliedModifiers );
		StatModApplier.Apply( sheet, entry.Def.Modifiers, entry.Stacks, entry, entry.AppliedModifiers, entry.Def.DisplayName );

		// Procs: registered once; later stacks only raise their potency (keeps ICDs and interval timers running).
		if ( isNew )
		{
			foreach ( var proc in entry.Def.Procs )
				_actor.Procs.AddProc( proc, source: entry, potency: entry.Stacks );
		}
		else
		{
			_actor.Procs.SetPotencyForSource( entry, entry.Stacks );
		}

		if ( StatModApplier.TouchesAttribute( entry.Def.Modifiers ) )
			sheet.RecalculateDerivedStats();
	}

	private void HandleDeath()
	{
		ClearAll();
	}

	private void CacheActor()
	{
		if ( _actor != null ) return;

		_actor = Components.GetInAncestorsOrSelf<Actor>();
		if ( _actor != null && !_subscribed )
		{
			// Death procs fire in Actor.ApplyDamage before OnDeath, so "on death" boons still get their shot.
			_actor.OnDeath += HandleDeath;
			_subscribed = true;
		}
	}

	protected override void OnStart()
	{
		CacheActor();
	}

	protected override void OnUpdate()
	{
		if ( _levelSubscribed ) return;

		// Actor creates its LevelComponent asynchronously after OnStart, so subscribe once it exists.
		// Subscribing after Initialize also means starting levels (InitialLevel > 1) never roll offers.
		CacheActor();
		if ( _actor?.Leveling == null ) return;

		_actor.Leveling.OnLevelUp += HandleLevelUp;
		_levelSubscribed = true;
	}

	protected override void OnDestroy()
	{
		if ( _subscribed && _actor != null )
			_actor.OnDeath -= HandleDeath;
		if ( _levelSubscribed && _actor?.Leveling != null )
			_actor.Leveling.OnLevelUp -= HandleLevelUp;
	}
}
