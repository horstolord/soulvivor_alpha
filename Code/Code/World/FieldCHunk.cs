namespace Sandbox.Code.World;

/// <summary>Integer cell (or chunk) coordinate in grid space — not world space.</summary>
public readonly record struct CellCoord( int X, int Y, int Z );

/// <summary>
/// 16x16x16 block of cells. Per-channel storage is allocated on first touch. A cached value is only
/// trusted while its valid flag is set, so invalidating is a flag clear — the recompute happens
/// lazily on the next read.
/// </summary>
public sealed class FieldChunk
{
	public const int Shift = 4;
	public const int Size = 1 << Shift;   // 16
	public const int Mask = Size - 1;
	public const int CellCount = Size * Size * Size;

	private readonly float[][] _values = new float[(int)FieldChannel.Count][];
	private readonly bool[][] _valid = new bool[(int)FieldChannel.Count][];

	/// <summary>Index of a cell inside its chunk. The mask keeps this correct for negative coordinates.</summary>
	public static int LocalIndex( CellCoord cell ) =>
		(((cell.Z & Mask) * Size) + (cell.Y & Mask)) * Size + (cell.X & Mask);

	public bool TryGet( FieldChannel channel, int index, out float value )
	{
		int c = (int)channel;
		var valid = _valid[c];
		if ( valid != null && valid[index] )
		{
			value = _values[c][index];
			return true;
		}

		value = 0f;
		return false;
	}

	public void Set( FieldChannel channel, int index, float value )
	{
		int c = (int)channel;
		_values[c] ??= new float[CellCount];
		_valid[c] ??= new bool[CellCount];
		_values[c][index] = value;
		_valid[c][index] = true;
	}

	public void Invalidate( FieldChannel channel, int index )
	{
		var valid = _valid[(int)channel];
		if ( valid != null ) valid[index] = false;
	}

	public void InvalidateChannel( FieldChannel channel )
	{
		var valid = _valid[(int)channel];
		if ( valid != null ) System.Array.Clear( valid, 0, valid.Length );
	}
}
