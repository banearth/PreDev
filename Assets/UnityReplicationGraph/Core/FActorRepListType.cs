using UnityEngine;

public enum ENetDormancy
{
	/** 此Actor永远不会进入网络休眠状态 */
	DORM_Never,
	/** 此Actor可以休眠,但当前未休眠。游戏代码会告诉它何时休眠 */
	DORM_Awake,
	/** 此Actor想要对所有连接完全休眠 */
	DORM_DormantAll,
	/** 此Actor可能想要对某些连接休眠,将调用GetNetDormancy()来确定具体哪些连接 */
	DORM_DormantPartial,
	/** 如果此Actor被放置在地图中,则初始时对所有连接都处于休眠状态 */
	DORM_Initial,
}

/// <summary>
/// 对应UE中的FActorRepListType(AActor*)
/// </summary>
public class FActorRepListType : UObject
{
	public Vector3 Position { get; set; }
	// ue里面的owner其实是一个actor，我们这里简单一些，直接给connection
	public UNetConnection Owner { get; set; }

	// 复制相关的基础属性
	public float NetUpdateFrequency { get; set; } = 30f;  // 默认30Hz
	public bool bAlwaysRelevant { get; set; } = false;
	public bool bOnlyRelevantToOwner { get; set; } = false;
	public float NetCullDistanceSquared { get; set; } = 10000f;  // 默认100米
	public uint NetId { get; set; }
	public bool IsReplicated { get; set; } = true;
	public ENetDormancy NetDormancy { get; set; } = ENetDormancy.DORM_Awake;

	/// <summary>
    /// 表示这个Actor是在关卡中放置的（而不是运行时动态生成的）
    /// 这类Actor在DORM_Initial状态下开始时不会被复制
    /// </summary>
    public bool IsNetStartupActor { get; set; } = false;

	private bool bActorIsBeingDestroyed = false;
	private bool bTearOff = false;

	// 复制类型标识符
    public string ReplicationType { get; set; } = "Default";

	public FActorRepListType()
	{
		Position = Vector3.zero;
	}

	public virtual bool IsNetRelevantFor(UNetConnection connection)
	{
		if (bAlwaysRelevant) return true;
		if (bOnlyRelevantToOwner) return connection == Owner;
		return true;
	}

	public bool IsActorBeingDestroyed() { return bActorIsBeingDestroyed; }
	public bool GetTearOff() { return bTearOff; }
	public UNetConnection GetNetConnection() { return Owner; }

	/// <summary>
	/// 这里面不重要模拟
	/// </summary>
	public void CallPreReplication(UNetworkDriver netDriver)
	{
		if (netDriver == null)
		{
			return;
		}
	}

	/// <summary>
    /// 获取Actor所在的关卡
    /// </summary>
    public ULevel GetLevel()
    {
        // 递归向上查找ULevel类型的Outer
        return GetTypedOuter<ULevel>();
    }

	public virtual void GetPlayerViewPoint(ref Vector3 position, ref Vector3 viewDir)
	{	
	}

}