using Sandbox.Code.Actors;

namespace Sandbox.Code.World;

/// <summary>
/// Scene-level owner of the <see cref="FieldGrid"/>. Place exactly one in the scene (same convention
/// as SpawnDirector). Consumers read through <c>FieldSystem.Current?.Grid</c>.
/// CellSize is read once when the component enables — change it, then restart play.
/// </summary>
public sealed class FieldSystem : Component
{
	public static FieldSystem Current { get; private set; }

	[Property] public float CellSize { get; set; } = 256f;

	/// <summary>Map-wide base light. 0 = pitch dark, 1 = fully lit.</summary>
	[Property, Range( 0f, 1f )] public float AmbientLight { get; set; } = 0.15f;

	[Property, Group( "Debug" )] public bool ShowDebug { get; set; }
	[Property, Group( "Debug" )] public FieldChannel DebugChannel { get; set; } = FieldChannel.Light;
	[Property, Group( "Debug" )] public int DebugRadiusCells { get; set; } = 8;

	public FieldGrid Grid { get; private set; }

	private float _appliedAmbient;

	protected override void OnEnabled()
	{
		Grid = new FieldGrid( CellSize );
		Current = this;
		ApplyAmbient();
	}

	protected override void OnDisabled()
	{
		if ( Current == this ) Current = null;
		Grid = null;
	}

	protected override void OnUpdate()
	{
		if ( Grid == null ) return;

		if ( _appliedAmbient != AmbientLight )
			ApplyAmbient();

		if ( ShowDebug )
			DrawDebug();
	}

	private void ApplyAmbient()
	{
		_appliedAmbient = AmbientLight;
		Grid.SetAmbient( FieldChannel.Light, AmbientLight );
	}

	/// <summary>
	/// Flat slice of the debug channel at the player's height. Drawn from OnUpdate, mirroring the
	/// SpawnDirector gizmo pattern.
	/// </summary>
	private void DrawDebug()
	{
		var focus = Player.Local?.GameObject.WorldPosition ?? Scene.Camera?.WorldPosition;
		if ( focus == null ) return;

		var origin = focus.Value;
		var center = Grid.WorldToCell( origin );
		float half = Grid.CellSize * 0.5f * 0.95f;
		var dark = new Color( 0.05f, 0.05f, 0.25f );
		var bright = new Color( 1f, 0.9f, 0.4f );

		for ( int dy = -DebugRadiusCells; dy <= DebugRadiusCells; dy++ )
		for ( int dx = -DebugRadiusCells; dx <= DebugRadiusCells; dx++ )
		{
			var cell = new CellCoord( center.X + dx, center.Y + dy, center.Z );
			float value = Grid.GetCell( DebugChannel, cell );
			var c = Grid.CellCenter( cell ).WithZ( origin.z );

			Gizmo.Draw.Color = Color.Lerp( dark, bright, value ).WithAlpha( 0.35f );
			Gizmo.Draw.SolidBox( new BBox( c - new Vector3( half, half, 1f ), c + new Vector3( half, half, 1f ) ) );
		}

		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.ScreenText(
			$"Field {DebugChannel}: {Grid.SampleSmooth( DebugChannel, origin ):F2} | chunks: {Grid.ChunkCount}",
			new Vector2( 10, 150 ) );
	}
}
