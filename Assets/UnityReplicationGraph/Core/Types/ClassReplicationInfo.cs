public class ClassReplicationInfo
{
    public int ReplicationPeriodFrame;  // 多少帧更新一次
    public float CullDistanceSquared;   // 裁剪距离
    public bool AlwaysRelevant;         // 是否总是相关
    public bool OnlyRelevantToOwner;    // 是否只对所有者相关
} 