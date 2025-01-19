public class FFastSharedReplicationInfo
{
    // 最后一次调用FastSharedReplicationFunc的帧号
    public uint LastAttemptBuildFrameNum { get; set; } = 0;
    
    // 最后一次实际创建新bunch的帧号
    public uint LastBunchBuildFrameNum { get; set; } = 0;

    // 用于网络复制的数据包
    public int Bunch { get; set; }
}