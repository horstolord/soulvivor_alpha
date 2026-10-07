namespace Sandbox.Code.Data;

/// <summary>
/// Shared rarity colors for world loot visuals. Keep the matching inventory stylesheet colors in sync.
/// </summary>
public static class RarityStyle
{
	public static Color ColorOf( ItemRarity rarity ) => rarity switch
	{
		ItemRarity.Common => FromRgb( 0xD9D4C7 ),
		ItemRarity.Magic => FromRgb( 0x4A9EFF ),
		ItemRarity.Rare => FromRgb( 0xF4EC3A ),
		ItemRarity.Epic => FromRgb( 0xA855F7 ),
		ItemRarity.Legendary => FromRgb( 0xFF7A1A ),
		ItemRarity.Mythical => FromRgb( 0xE02828 ),
		ItemRarity.Divine => FromRgb( 0xF5C242 ),
		_ => Color.White
	};

	/// <summary>Lowercase enum name, matching the rarity classes in the inventory stylesheet.</summary>
	public static string CssClass( ItemRarity rarity ) => rarity.ToString().ToLower();

	private static Color FromRgb( int rgb ) => new Color(
		((rgb >> 16) & 0xFF) / 255f,
		((rgb >> 8) & 0xFF) / 255f,
		(rgb & 0xFF) / 255f );
}
