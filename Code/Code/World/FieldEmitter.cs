namespace Sandbox.Code.World;

/// <summary>
/// Puts a point contribution into the FieldGrid. Put it on torches, braziers, cursed shrines, or on
/// a spawned spell prefab. Which <see cref="Channel"/> it writes to decides what it means:
/// Light = counts for world light (threat scaling), LightDynamic = visual-only.
/// Registers lazily, so load order against FieldSystem doesn't matter.
/// </summary>
public sealed class FieldEmitter : Component
{
	[Property] public FieldChannel Channel { get; set; } = FieldChannel.Light;
	[Property] public float Radius { get; set; } = 512f;
	/// <summary>Contribution at the centre. Negative = darkness / drain.</summary>
	[Property, Range( -1f, 1f )] public float Strength { get; set; } = 0.8f;
	[Property, Range( 0.25f, 4f )] public float Falloff { get; set; } = 1f;

	// Moves smaller than this don't re-invalidate cells (a 256-unit cell / 16).
	private const float MoveThreshold = 16f;

	private FieldGrid _grid;
	private FieldSource _source;

	protected override void OnUpdate()
	{
		var grid = FieldSystem.Current?.Grid;
		if ( grid == null ) return;

		if ( _grid != grid || _source == null || _source.Channel != Channel )
		{
			Unregister();
			Register( grid );
			return;
		}

		var pos = GameObject.WorldPosition;
		bool moved = (pos - _source.Position).LengthSquared > MoveThreshold * MoveThreshold;
		bool changed = _source.Radius != Radius || _source.Strength != Strength || _source.Falloff != Falloff;
		if ( !moved && !changed ) return;

		_source.Position = pos;
		_source.Radius = Radius;
		_source.Strength = Strength;
		_source.Falloff = Falloff;
		_grid.Refresh( _source );
	}

	protected override void OnDisabled() => Unregister();
	protected override void OnDestroy() => Unregister();

	private void Register( FieldGrid grid )
	{
		_grid = grid;
		_source = new FieldSource
		{
			Channel = Channel,
			Position = GameObject.WorldPosition,
			Radius = Radius,
			Strength = Strength,
			Falloff = Falloff
		};
		_grid.AddSource( _source );
	}

	private void Unregister()
	{
		if ( _grid != null && _source != null )
			_grid.RemoveSource( _source );
		_grid = null;
		_source = null;
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected ) return;
		Gizmo.Draw.Color = Color.Yellow.WithAlpha( 0.4f );
		Gizmo.Draw.LineSphere( Vector3.Zero, Radius );
	}
}
