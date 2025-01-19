using System.Collections.Generic;

public class UNetConnection
{
    // 连接状态
    public enum EConnectionState
    {
        Invalid,    // 无效连接
        USOCK_Closed,     // 永久关闭
        USOCK_Pending,    // 等待连接
        USOCK_Open,       // 已打开
        USOCK_Closing     // 正在关闭
    }

    // 基础属性
    public EConnectionState State { get; protected set; }
    public string Address { get; protected set; }
    public UNetworkDriver Driver { get; protected set; }
    public uint ConnectionId { get; private set; }
	// 可见关卡名称集合
	public HashSet<string> ClientVisibleLevelNames = new HashSet<string>();

    // 视角目标
    public FActorRepListType ViewTarget;
	public UNetReplicationGraphConnection ReplicationConnectionDriver { get; private set; }

	public UNetConnection(uint connectionId)
    {
        ConnectionId = connectionId;
        State = EConnectionState.Invalid;
    }

    // 初始化连接
    public virtual void InitBase(UNetworkDriver driver, string address)
    {
        Driver = driver;
        Address = address;
        State = EConnectionState.USOCK_Pending;
    }

    // 关闭连接
    public virtual void Close()
    {
        State = EConnectionState.USOCK_Closed;
        CleanUp();
    }

    // 清理资源
    protected virtual void CleanUp()
    {
        Driver = null;
    }

    public ActorChannel CreateChannel()
    {
        return new ActorChannel(this);
    }

    public void SetReplicationConnectionDriver(UNetReplicationGraphConnection NewReplicationConnectionDriver)
    {
        ReplicationConnectionDriver = NewReplicationConnectionDriver;
    }

	public bool IsClosingOrClosed()
    {
		return State == EConnectionState.USOCK_Closing || State == EConnectionState.USOCK_Closed;
	}

}