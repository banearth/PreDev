// 1. 首先定义复制系统的基础接口
public abstract class UReplicationDriver
{
	protected UNetworkDriver NetDriver;
	public virtual void InitForNetDriver(UNetworkDriver inNetDriver)
	{
		NetDriver = inNetDriver;
	}
	public abstract void AddClientConnection(UNetConnection connection);
	public abstract int ServerReplicateActors(float deltaTime);
} 