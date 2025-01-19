/// <summary>
/// Actor网络同步通道
/// </summary>
public class ActorChannel
{
    /// <summary>
    /// 对应的Actor
    /// </summary>
    public FActorRepListType Actor { get; private set; }
    
    /// <summary>
    /// 对应的网络连接
    /// </summary>
    public UNetConnection Connection { get; private set; }

    /// <summary>
    /// 通道状态
    /// </summary>
    public EChannelState State { get; private set; }

    /// <summary>
    /// 是否正在休眠
    /// </summary>
    public bool IsDormant { get; private set; }

    public ActorChannel(UNetConnection connection)
    {
        Connection = connection;
        State = EChannelState.Open;
        IsDormant = false;
    }

    public void SetChannelActor(FActorRepListType Actor)
    {
		this.Actor = Actor;
	}

	/// <summary>
	/// 复制Actor状态
	/// </summary>
	/// <returns>发送的比特数</returns>
	public virtual long ReplicateActor()
    {
        if (State != EChannelState.Open)
        {
            return 0;
        }

        if (IsDormant)
        {
            return 0;
        }

        // TODO: 实际的属性同步逻辑
        return 0;
    }

    /// <summary>
    /// 关闭通道
    /// </summary>
    public virtual long Close(EChannelCloseReason reason)
    {
        if (State == EChannelState.Closed)
        {
            return 0;
        }

        State = EChannelState.Closed;
        
        // TODO: 发送关闭消息给客户端
        return 0;
    }

    /// <summary>
    /// 开始进入休眠状态
    /// </summary>
    public void StartBecomingDormant()
    {
        if (!IsDormant)
        {
            IsDormant = true;
            // TODO: 通知客户端Actor进入休眠
        }
    }

    /// <summary>
    /// 唤醒
    /// </summary>
    public void WakeUp()
    {
        if (IsDormant)
        {
            IsDormant = false;
            // TODO: 通知客户端Actor唤醒
        }
    }

    public void SendBunch(int bunch)
    {
    }
}

/// <summary>
/// 通道状态
/// </summary>
public enum EChannelState
{
    /// <summary>
    /// 已打开
    /// </summary>
    Open,
    
    /// <summary>
    /// 已关闭
    /// </summary>
    Closed
}

/// <summary>
/// 通道关闭原因
/// </summary>
public enum EChannelCloseReason
{
    /// <summary>
    /// 正常关闭
    /// </summary>
    Normal,
    
    /// <summary>
    /// 断开连接
    /// </summary>
    Disconnect,
    
    /// <summary>
    /// Actor销毁
    /// </summary>
    Destroyed,
    
    /// <summary>
    /// 强制断开
    /// </summary>
    TearOff
}