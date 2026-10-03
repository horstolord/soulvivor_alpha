namespace Sandbox.Code.Systems;

using Sandbox.Code.Actors;
using Sandbox.Code.Systems;

public enum ProjectileTerminationType { Timeout, FirstHit, PierceCount, Infinite }

public class ProjectileTemplate
{
    public float Lifetime = 5f;
    public ProjectileTerminationType Termination = ProjectileTerminationType.FirstHit;
    public int PierceCount = 1;
    public Vector3 CollisionBoxSize = new Vector3( 12f, 12f, 12f );
    public float Speed = 1000f; // motion components read this too
    public ProjectileTemplate Clone() => new ProjectileTemplate
    {
	    Lifetime = Lifetime,
	    Termination = Termination,
	    PierceCount = PierceCount,
	    CollisionBoxSize = CollisionBoxSize,
	    Speed = Speed
    };
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

    public bool IsStuck { get; private set; }

    protected override void OnStart()
    {
        base.OnStart();
        _lastPosition = GameObject.WorldPosition;
    }

    protected override void OnFixedUpdate()
    {
        if ( Template == null || Payload == null || IsStuck ) return;

        _age += Time.Delta;
        if ( Template.Termination != ProjectileTerminationType.Infinite && _age >= Template.Lifetime )
        {
	        GameObject.Destroy();
	        return;
        }

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
	        .RunAll();


        foreach ( var hit in hits )
        {
            if ( hit.GameObject == null || hit.GameObject.Tags.Has( "noarrow" )  ) continue; 
            OnHit( hit.GameObject );
            if ( ShouldTerminateAfterHit() && GameObject.Tags.Has( "noarrow" ) )
            {
                StickTo( hit.GameObject );
                break;
            }
            GameObject.Destroy();
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

    private void OnHit( GameObject target )
    {
	    // HitResolver applies crit + PhysicalForce per hit (correct for piercing), deduplicates,
	    // and calls ApplyDamage + ApplyKnockback. Pass raw Forward — ApplyKnockback adds upward bias.
	    HitResolver.Apply( Payload.Caster, target, Payload.Damage,
		    GameObject.WorldRotation.Forward, alreadyHit: null, hitPoint: GameObject.WorldPosition );

	    SpellEffectApplier.Apply( Payload?.SourceContext as SpellContext, target, GameObject.WorldPosition );
 
	    _hitCount++;
 
	    // Trigger Rune Sub-Spell Execution
	    if ( Payload?.SourceContext is SpellContext spellCtx && spellCtx.TriggerPayloadRunes != null && spellCtx.TriggerPayloadRunes.Count > 0 )
	    {
		    var hitPos = GameObject.WorldPosition;
		    var hitNormal = -GameObject.WorldRotation.Forward;
		    RuneEvaluator.ExecuteTriggerPayload( spellCtx, hitPos, hitNormal, target );
	    }
 
	    // TODO: MaterialData / InteractionOverrides lookup goes here later
    }

    private bool ShouldTerminateAfterHit()
    {
        return Template.Termination switch
        {
            ProjectileTerminationType.FirstHit => _hitCount >= 1,
            ProjectileTerminationType.PierceCount => _hitCount >= Template.PierceCount,
            _ => false
        };
    }
}
