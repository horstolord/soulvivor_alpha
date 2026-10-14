using Sandbox.Code.Actors;

namespace Sandbox.Code.Presentation;

public sealed class HitFlashComp : Component
{
	[Property] public Color FlashColor { get; set; } = new Color( 1f, 0.2f, 0.15f );
	[Property] public float Duration { get; set; } = 0.12f;

	private Actor _actor;
	private readonly List<(ModelRenderer Renderer, Color Original)> _captured = new();
	private float _timer;

	protected override void OnStart()
	{
		_actor = Components.GetInAncestorsOrSelf<Actor>();
		if ( _actor != null ) _actor.OnHitReceived += Flash;
	}

	protected override void OnDestroy()
	{
		if ( _actor != null ) _actor.OnHitReceived -= Flash;
		Restore();
	}

	private void Flash()
	{
		// Only capture originals on the first hit of a flash, so overlapping
		// hits refresh the timer instead of recording the flash colour as "original".
		if ( _captured.Count == 0 )
		{
			foreach ( var r in Components.GetAll<ModelRenderer>( FindMode.EnabledInSelfAndDescendants ) )
				_captured.Add( (r, r.Tint) );
		}
		_timer = Duration;
	}

	protected override void OnUpdate()
	{
		if ( _captured.Count == 0 ) return;

		_timer -= Time.Delta;
		if ( _timer <= 0f ) { Restore(); return; }

		float t = _timer / Duration; // 1 -> 0, eased out
		t *= t;
		foreach ( var (r, original) in _captured )
			if ( r.IsValid() ) r.Tint = Color.Lerp( original, FlashColor, t );
	}

	private void Restore()
	{
		foreach ( var (r, original) in _captured )
			if ( r.IsValid() ) r.Tint = original;
		_captured.Clear();
	}
}
