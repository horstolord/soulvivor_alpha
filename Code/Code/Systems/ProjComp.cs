namespace Sandbox.Code.Systems;

using Sandbox.Code.Actors;
using Sandbox.Code.Systems;

public enum ProjectileTerminationType { Timeout, FirstHit, PierceCount, Infinite }

public class ProjectileTemplate
{
    public float Lifetime = 5f;
    public ProjectileTerminationType Termination = ProjectileTerminationType.FirstHit;
    public int PierceCount = 1;
    public bool StickOnHit;
    /// <summary>
    /// A hit that kills its target (or lands on an already-dead actor) doesn't use up a pierce, so the
    /// projectile carries on until it hits a survivor or a wall. Off by default so spell projectiles
    /// still stop on their first hit.
    /// </summary>
    public bool KillsRefundPierce;
    public Vector3 CollisionBoxSize = new Vector3( 12f, 12f, 12f );
    public float Speed = 1000f; // motion components read this too
    public ProjectileTemplate Clone() => new ProjectileTemplate
    {
	    Lifetime = Lifetime,
	    Termination = Termination,
	    PierceCount = PierceCount,
	    StickOnHit = StickOnHit,
	    KillsRefundPierce = KillsRefundPierce,
	    CollisionBoxSize = CollisionBoxSize,
	    Speed = Speed
    };

    /// <summary>
    /// Adds extra targets the projectile can pass through (ProjectilePierce stat, spell BonusPierce).
    /// A FirstHit template is upgraded to PierceCount; Timeout/Infinite already pass through everything.
    /// </summary>
    public void AddPierce( int extra )
    {
	    if ( extra <= 0 ) return;
	    if ( Termination == ProjectileTerminationType.FirstHit )
	    {
		    Termination = ProjectileTerminationType.PierceCount;
		    PierceCount = 1;
	    }
	    if ( Termination == ProjectileTerminationType.PierceCount )
		    PierceCount += extra;
    }
}

/// <summary>Shared numbers for the projectile-count and echo mods; one place to tune them.</summary>
public static class ProjectileTuning
{
	/// <summary>Degrees between neighbouring projectiles in a fan (extra projectiles).</summary>
	public const float FanAngle = 8f;
	/// <summary>Seconds between a shot/cast and its echo.</summary>
	public const float EchoDelay = 0.25f;
	/// <summary>Damage of an echo relative to the original.</summary>
	public const float EchoDamageMultiplier = 0.6f;

	/// <summary>Yaw offset for projectile <paramref name="index"/> of <paramref name="count"/>, centred on the aim.</summary>
	public static float FanYaw( int index, int count ) => (index - (count - 1) * 0.5f) * FanAngle;

	/// <summary>True when a 0-100 chance roll succeeds.</summary>
	public static bool Roll( float chancePercent )
		=> chancePercent > 0f && Random.Shared.NextSingle() * 100f < chancePercent;
}

public class ProjectilePayload
{
    public GameObject Caster;
    public DamageProfileDef Damage;
    public HashSet<AttackTag> AttackTags = new();
    public object SourceContext; // AttackContext or SpellContext, for override lookups later?
}

public sealed class Projectile : Component
{
    public ProjectileTemplate Template { get; set; }
    public ProjectilePayload Payload { get; set; }

    private float _age;
    private int _hitCount;
    private Vector3 _lastPosition;
    private readonly HashSet<GameObject> _hitTargets = new();

    public bool IsStuck { get; private set; }

    protected override void OnStart()
    {
        base.OnStart();
        _lastPosition = GameObject.WorldPosition;
    }

    protected override void OnFixedUpdate()
    {
        if ( Template == null || Payload == null ) return;

        // Keep aging after impact so stuck projectiles still reach their lifetime cleanup.
        _age += Time.Delta;
        if ( Template.Termination != ProjectileTerminationType.Infinite && _age >= Template.Lifetime )
        {
	        GameObject.Destroy();
	        return;
        }

        if ( IsStuck ) return;

        Components.Get<IProjectileMotion>()?.Tick( this, Time.Delta );

        // Face the direction actually traveled this tick — works for straight,
        // arcing, or any future curving/homing motion, without motion-specific code.
        var moveDelta = GameObject.WorldPosition - _lastPosition;
        if ( moveDelta.LengthSquared > 0.01f )
	        GameObject.WorldRotation = Rotation.LookAt( moveDelta.Normal, Vector3.Up );

        // Sweep-trace from last position to current, so fast projectiles can't tunnel
        var hits = Scene.Trace
	        .Box( Template.CollisionBoxSize, _lastPosition, GameObject.WorldPosition )
	        .IgnoreGameObjectHierarchy( Payload.Caster )
	        .IgnoreGameObjectHierarchy( GameObject )
	        .RunAll();

        // Nearest first: a wall behind an enemy must not end the projectile before the enemy is hit,
        // and the pierce budget has to be spent on the closest targets.
        foreach ( var hit in hits.OrderBy( h => h.Distance ) )
        {
            var target = hit.GameObject;
            if ( target == null || !target.IsValid() || target.Tags.Has( "noarrow" ) )
                continue;

            var actor = CombatMath.ResolveActor( target );
            bool solid = actor == null; // walls, props, terrain: anything that isn't an actor
            bool wasDead = IsDead( actor );

            if ( !OnHit( target ) )
                continue; // this actor was already hit by this projectile

            // Pierce is only spent on living actors that survive (when KillsRefundPierce is set).
            // Walls never count: they always stop the projectile instead.
            bool refunded = !solid && Template.KillsRefundPierce && (wasDead || IsDead( actor ));
            if ( !solid && !refunded )
                _hitCount++;

            if ( ShouldTerminateAfterHit( solid ) )
            {
                if ( Template.StickOnHit )
                    StickTo( target );
                else
                    GameObject.Destroy();
                return;
            }
        }

        _lastPosition = GameObject.WorldPosition;
    }

    public void StickTo( GameObject target )
    {
        if ( IsStuck ) return;
        IsStuck = true;

        // Penetrate slightly into hit surface
        GameObject.WorldPosition += GameObject.WorldRotation.Forward * 3f;

        // Disable physics/colliders if attached to projectile prefab
        if ( Components.TryGet<Rigidbody>( out var rb ) ) rb.MotionEnabled = false;
        if ( Components.TryGet<Collider>( out var col ) ) col.Enabled = false;

        // Attach to the hit object (environment or enemy)
        if ( target.IsValid() )
        {
            GameObject.SetParent( target, true );
        }
    }

    private bool OnHit( GameObject target )
    {
	    // HitResolver applies crit + PhysicalForce per hit (correct for piercing), deduplicates,
	    // and calls ApplyDamage + ApplyKnockback. Pass raw Forward — ApplyKnockback adds upward bias.
	    if ( !HitResolver.Apply( Payload.Caster, target, Payload.Damage,
		    GameObject.WorldRotation.Forward, alreadyHit: _hitTargets, hitPoint: GameObject.WorldPosition ) )
		    return false;

	    SpellEffectApplier.Apply( Payload?.SourceContext as SpellContext, target, GameObject.WorldPosition );

	    // Trigger Rune Sub-Spell Execution
	    if ( Payload?.SourceContext is SpellContext spellCtx && spellCtx.TriggerPayloadRunes != null && spellCtx.TriggerPayloadRunes.Count > 0 )
	    {
		    var hitPos = GameObject.WorldPosition;
		    var hitNormal = -GameObject.WorldRotation.Forward;
		    RuneEvaluator.ExecuteTriggerPayload( spellCtx, hitPos, hitNormal, target );
	    }
  
	    // TODO: MaterialData / InteractionOverrides lookup goes here later
	    return true;
    }

    private static bool IsDead( Actor actor ) => actor?.StateComp?.CurrentState == ActorStateType.Dead;

    private bool ShouldTerminateAfterHit( bool hitSolid )
    {
        // Timeout / Infinite projectiles pass through everything until their lifetime ends.
        if ( Template.Termination is ProjectileTerminationType.Timeout or ProjectileTerminationType.Infinite )
            return false;

        // Pierce only applies to actors; a wall ends FirstHit and PierceCount projectiles alike.
        if ( hitSolid )
            return true;

        return Template.Termination switch
        {
            ProjectileTerminationType.FirstHit => _hitCount >= 1,
            ProjectileTerminationType.PierceCount => _hitCount >= Template.PierceCount,
            _ => false
        };
    }
}
