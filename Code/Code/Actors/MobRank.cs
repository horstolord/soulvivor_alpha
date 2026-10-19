namespace Sandbox.Code.Actors;
 
/// <summary>
/// How special an actor instance is. Set per instance (SpawnDirector for elites, inspector for bosses),
/// not per preset, so any mob type can roll elite. Drives boon drop chance and rarity bias.
/// </summary>
public enum MobRank
{
	Normal,
	Elite,
	Boss
}
