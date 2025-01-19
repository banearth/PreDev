public class FReplicationGraphGlobalData
{
    // 全局Actor复制信息映射
    public FGlobalActorReplicationInfoMap GlobalActorReplicationInfoMap { get; private set; }

    // 当前世界引用
    public UWorld World { get; set; }

    // 帧计数器
    public uint ReplicationFrameNum { get; set; }

    // 构造函数
    public FReplicationGraphGlobalData(UWorld inWorld)
    {
        World = inWorld;
        GlobalActorReplicationInfoMap = new FGlobalActorReplicationInfoMap();
        ReplicationFrameNum = 0;
    }

    // 重置所有数据
    public void Reset()
    {
        GlobalActorReplicationInfoMap.Reset();
        ReplicationFrameNum = 0;
    }

    // 获取当前帧号
    public uint GetFrameNum()
    {
        return ReplicationFrameNum;
    }

    // 增加帧计数
    public void IncrementFrameNum()
    {
        ReplicationFrameNum++;
    }
}