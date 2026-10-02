using System.Collections.Generic;
using Sandbox.Code.World;

namespace Sandbox.Code.Data;

public class CatalystDef
{
	public string Id;
	public string DisplayName;
	public int SlotCapacity = 6;
	public float BaseRechargeTime = 1.0f;
	public float BaseCastDelay = 0.1f;
	public float Spread = 0f;
	public bool IsShuffle = false;
	public List<RuneDef> EquippedRunes = new();
}
