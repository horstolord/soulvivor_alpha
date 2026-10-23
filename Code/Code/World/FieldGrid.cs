using System;
using System.Collections.Generic;

namespace Sandbox.Code.World;

/// <summary>
/// A point contribution to one channel (a torch, a cursed zone, ...). Plain data — the grid owns
/// invalidation. Negative Strength subtracts (darkness, drain).
/// </summary>
public sealed class FieldSource
{
	public FieldChannel Channel;
	public Vector3 Position;
	public float Radius = 512f;
	/// <summary>Contribution at the centre.</summary>
	public float Strength = 1f;
	/// <summary>1 = linear falloff, 2 = quadratic (tighter pool of light), &lt;1 = fatter.</summary>
	public float Falloff = 1f;

	// Footprint the grid last invalidated, so Refresh can clear exactly the old area too.
	internal Vector3 AppliedPosition;
	internal float AppliedRadius;

	public float Evaluate( Vector3 point )
	{
		float dist = (point - Position).Length;
		if ( dist >= Radius ) return 0f;
		return Strength * MathF.Pow( 1f - dist / Radius, MathF.Max( 0.01f, Falloff ) );
	}
}

/// <summary>
/// Sparse, chunked, uniform 3D grid of float channels. World-aligned at the origin, unbounded
/// (chunks are created on first touch). Cell value = channel ambient + sum of sources, clamped 0..1,
/// computed lazily and cached. Not a Component — <see cref="FieldSystem"/> owns the instance.
/// </summary>
public sealed class FieldGrid
{
	public float CellSize { get; }
	public int ChunkCount => _chunks.Count;

	private readonly Dictionary<CellCoord, FieldChunk> _chunks = new();
	private readonly float[] _ambient = new float[(int)FieldChannel.Count];
	private readonly List<FieldSource>[] _sources = new List<FieldSource>[(int)FieldChannel.Count];

	public FieldGrid( float cellSize )
	{
		CellSize = MathF.Max( 1f, cellSize );
		for ( int i = 0; i < _sources.Length; i++ )
			_sources[i] = new List<FieldSource>();
	}

	// ============ COORDINATES ============

	private static int Floor( float v ) => (int)MathF.Floor( v );

	public CellCoord WorldToCell( Vector3 p ) =>
		new( Floor( p.x / CellSize ), Floor( p.y / CellSize ), Floor( p.z / CellSize ) );

	public Vector3 CellCenter( CellCoord c ) =>
		new( (c.X + 0.5f) * CellSize, (c.Y + 0.5f) * CellSize, (c.Z + 0.5f) * CellSize );

	// Arithmetic shift floors for negatives, so chunk coords stay consistent across the origin.
	private static CellCoord ToChunk( CellCoord c ) =>
		new( c.X >> FieldChunk.Shift, c.Y >> FieldChunk.Shift, c.Z >> FieldChunk.Shift );

	// ============ READ ============

	/// <summary>Value of the cell containing worldPos. Steps at cell borders — use SampleSmooth for anything continuous.</summary>
	public float Sample( FieldChannel channel, Vector3 worldPos ) =>
		GetCell( channel, WorldToCell( worldPos ) );

	/// <summary>Trilinear blend between neighbouring cell centres. Use for anything that drives gameplay numbers.</summary>
	public float SampleSmooth( FieldChannel channel, Vector3 worldPos )
	{
		float fx = worldPos.x / CellSize - 0.5f;
		float fy = worldPos.y / CellSize - 0.5f;
		float fz = worldPos.z / CellSize - 0.5f;
		int x0 = Floor( fx ), y0 = Floor( fy ), z0 = Floor( fz );
		float tx = fx - x0, ty = fy - y0, tz = fz - z0;

		float G( int x, int y, int z ) => GetCell( channel, new CellCoord( x, y, z ) );

		float x00 = Mix( G( x0, y0, z0 ), G( x0 + 1, y0, z0 ), tx );
		float x10 = Mix( G( x0, y0 + 1, z0 ), G( x0 + 1, y0 + 1, z0 ), tx );
		float x01 = Mix( G( x0, y0, z0 + 1 ), G( x0 + 1, y0, z0 + 1 ), tx );
		float x11 = Mix( G( x0, y0 + 1, z0 + 1 ), G( x0 + 1, y0 + 1, z0 + 1 ), tx );
		return Mix( Mix( x00, x10, ty ), Mix( x01, x11, ty ), tz );
	}

	private static float Mix( float a, float b, float t ) => a + (b - a) * t;

	public float GetCell( FieldChannel channel, CellCoord cell )
	{
		var chunk = GetOrCreateChunk( ToChunk( cell ) );
		int index = FieldChunk.LocalIndex( cell );
		if ( chunk.TryGet( channel, index, out var cached ) )
			return cached;

		float value = Compute( channel, cell );
		chunk.Set( channel, index, value );
		return value;
	}

	private float Compute( FieldChannel channel, CellCoord cell )
	{
		var center = CellCenter( cell );
		float value = _ambient[(int)channel];
		var list = _sources[(int)channel];
		for ( int i = 0; i < list.Count; i++ )
			value += list[i].Evaluate( center );

		// 0..1 for every channel for now — revisit when a channel needs another range (e.g. unbounded mana).
		return Math.Clamp( value, 0f, 1f );
	}

	private FieldChunk GetOrCreateChunk( CellCoord key )
	{
		if ( !_chunks.TryGetValue( key, out var chunk ) )
		{
			chunk = new FieldChunk();
			_chunks[key] = chunk;
		}
		return chunk;
	}

	// ============ WRITE ============

	/// <summary>Baseline value of a channel everywhere (e.g. map-wide darkness).</summary>
	public void SetAmbient( FieldChannel channel, float value )
	{
		_ambient[(int)channel] = value;
		foreach ( var chunk in _chunks.Values )
			chunk.InvalidateChannel( channel );
	}

	public void AddSource( FieldSource source )
	{
		if ( source == null ) return;
		_sources[(int)source.Channel].Add( source );
		source.AppliedPosition = source.Position;
		source.AppliedRadius = source.Radius;
		InvalidateFootprint( source.Channel, source.Position, source.Radius );
	}

	public void RemoveSource( FieldSource source )
	{
		if ( source == null ) return;
		if ( _sources[(int)source.Channel].Remove( source ) )
			InvalidateFootprint( source.Channel, source.AppliedPosition, source.AppliedRadius );
	}

	/// <summary>Call after changing a registered source's Position/Radius/Strength/Falloff.</summary>
	public void Refresh( FieldSource source )
	{
		if ( source == null ) return;
		InvalidateFootprint( source.Channel, source.AppliedPosition, source.AppliedRadius );
		source.AppliedPosition = source.Position;
		source.AppliedRadius = source.Radius;
		InvalidateFootprint( source.Channel, source.Position, source.Radius );
	}

	/// <summary>
	/// Clears cached cells in the source's bounding box. Never allocates chunks. Cost is
	/// (2r/cell)^3 flag writes — fine for torches; revisit if huge radii move every frame.
	/// </summary>
	private void InvalidateFootprint( FieldChannel channel, Vector3 position, float radius )
	{
		var r = new Vector3( MathF.Max( 0f, radius ) );
		var min = WorldToCell( position - r );
		var max = WorldToCell( position + r );

		for ( int z = min.Z; z <= max.Z; z++ )
		for ( int y = min.Y; y <= max.Y; y++ )
		for ( int x = min.X; x <= max.X; x++ )
		{
			var cell = new CellCoord( x, y, z );
			if ( _chunks.TryGetValue( ToChunk( cell ), out var chunk ) )
				chunk.Invalidate( channel, FieldChunk.LocalIndex( cell ) );
		}
	}
}
