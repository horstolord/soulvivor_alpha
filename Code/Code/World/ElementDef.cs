using System.Collections.Generic;
using Sandbox;
using Sandbox.Code.Systems;

namespace Sandbox.Code.World;

/// <summary>What a spell spawns when it fires. Chosen from the Method rune's DeliveryType.</summary>
public enum SpellShape { Projectile, Beam, Nova, Cone, Pulse, Imbue }

/// <summary>
/// Per-element presentation + small tuning knobs. The Force rune decides the element
/// (SpellContext.PrimaryElement); Method runes stay element-agnostic and look the visuals up here.
/// </summary>
public sealed class ElementDef
{
	public string Name;

	/// <summary>Element material used on its elemental shape prefabs.</summary>
	public string MaterialPath;

	/// <summary>Element-specific prefab paths. Shapes not listed use the shared fallback.</summary>
	public Dictionary<SpellShape, string> PrefabOverrides = new();

	public float ProjectileSpeedMultiplier = 1f;

	private Material _material;
	private bool _materialTried;

	public Material GetMaterial()
	{
		if ( _materialTried ) return _material;
		_materialTried = true;
		if ( !string.IsNullOrWhiteSpace( MaterialPath ) )
			_material = Material.Load( MaterialPath );
		return _material;
	}
}

public static class ElementLibrary
{
	public static string SharedPrefab( SpellShape shape ) => shape switch
	{
		SpellShape.Projectile => "fireballin'.prefab", // neutral fallback until a generic orb exists
		SpellShape.Beam => "beamblue.prefab",
		SpellShape.Nova => "spell_nova.prefab",
		SpellShape.Cone => "spell_cone.prefab",
		SpellShape.Pulse => "spell_pulse.prefab",
		SpellShape.Imbue => "spell_imbue.prefab",
		_ => null
	};

	public static readonly ElementDef Neutral = new() { Name = "Neutral" };

	public static readonly ElementDef Fire = new()
	{
		Name = "Fire",
		MaterialPath = "karmanfire.vmat",
		PrefabOverrides = ElementPrefabs( "fire", "fireballin'.prefab" )
	};

	public static readonly ElementDef Frost = new()
	{
		Name = "Frost",
		MaterialPath = "ice.vmat",
		PrefabOverrides = ElementPrefabs( "frost", "frostballin'.prefab" )
	};

	public static readonly ElementDef Air = new()
	{
		Name = "Air",
		MaterialPath = "airforce.vmat",
		PrefabOverrides = ElementPrefabs( "air", "airballin'.prefab" )
	};

	public static readonly ElementDef Earth = new()
	{
		Name = "Earth",
		MaterialPath = "earth_geode.vmat",
		ProjectileSpeedMultiplier = 0.8333333f, // preserves the former 1000 vs 1200 projectile speeds
		PrefabOverrides = ElementPrefabs( "earth", "earthballin'.prefab" )
	};

	private static Dictionary<SpellShape, string> ElementPrefabs( string element, string projectile ) => new()
	{
		[SpellShape.Projectile] = projectile,
		[SpellShape.Beam] = element + "beam.prefab",
		[SpellShape.Nova] = element + "nova.prefab",
		[SpellShape.Cone] = element + "cone.prefab",
		[SpellShape.Pulse] = element + "pulse.prefab",
		[SpellShape.Imbue] = element + "imbue.prefab"
	};

	public static ElementDef Get( RuneElementTag? tag ) => tag switch
	{
		RuneElementTag.Fire => Fire,
		RuneElementTag.Frost => Frost,
		RuneElementTag.Air => Air,
		RuneElementTag.Earth => Earth,
		_ => Neutral
	};
}

public static class SpellVisualResolver
{
	public static SpellShape ShapeOf( RuneDeliveryType type ) => type switch
	{
		RuneDeliveryType.Projectile => SpellShape.Projectile,
		RuneDeliveryType.Blast => SpellShape.Projectile,
		RuneDeliveryType.Beam => SpellShape.Beam,
		RuneDeliveryType.Nova => SpellShape.Nova,
		RuneDeliveryType.AoE => SpellShape.Nova,
		RuneDeliveryType.Cone => SpellShape.Cone,
		RuneDeliveryType.Imbue => SpellShape.Imbue,
		_ => SpellShape.Pulse
	};

	/// <summary>Rune-level explicit path wins, then the element override, then the shared shape prefab.</summary>
	public static string ResolvePrefab( RuneElementTag? element, SpellShape shape, RuneDef rune = null )
	{
		if ( rune != null )
		{
			if ( !string.IsNullOrWhiteSpace( rune.PrefabPath ) ) return rune.PrefabPath;
			if ( !string.IsNullOrWhiteSpace( rune.ProjectilePrefabPath ) ) return rune.ProjectilePrefabPath;
			if ( !string.IsNullOrWhiteSpace( rune.BeamPrefabPath ) ) return rune.BeamPrefabPath;
		}

		var def = ElementLibrary.Get( element );
		if ( def.PrefabOverrides.TryGetValue( shape, out var path ) && !string.IsNullOrWhiteSpace( path ) )
			return path;
		return ElementLibrary.SharedPrefab( shape );
	}

	/// <summary>A Force rune's VisualMaterial wins, then the element's material (may be null).</summary>
	public static Material ResolveMaterial( SpellContext ctx )
		=> ctx?.VisualMaterial ?? ElementLibrary.Get( ctx?.PrimaryElement ).GetMaterial();
}
