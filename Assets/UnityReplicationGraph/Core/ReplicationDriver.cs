// 1. 首先定义复制系统的基础接口
public abstract class ReplicationDriver
{
    protected NetworkDriver NetDriver { get; private set; }

    public virtual void InitForNetDriver(NetworkDriver driver)
    {
        NetDriver = driver;
    }

    public abstract void ServerReplicateActors(float deltaTime);
} 