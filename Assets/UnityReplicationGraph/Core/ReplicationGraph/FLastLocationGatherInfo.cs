using UnityEngine;

public class FLastLocationGatherInfo
{
    /// <summary>
    /// 网络连接
    /// </summary>
    public UNetConnection Connection { get; private set; }

    /// <summary>
    /// 最后的位置
    /// </summary>
    public Vector3 LastLocation { get; set; }

    /// <summary>
    /// 最后一次超出范围的位置检查
    /// </summary>
    public Vector3 LastOutOfRangeLocationCheck { get; set; }

    /// <summary>
    /// 默认构造函数
    /// </summary>
    public FLastLocationGatherInfo()
    {
        Connection = null;
        LastLocation = Vector3.zero;
        LastOutOfRangeLocationCheck = Vector3.zero;
    }

    /// <summary>
    /// 带参数的构造函数
    /// </summary>
    public FLastLocationGatherInfo(UNetConnection connection, Vector3 lastLocation)
    {
        Connection = connection;
        LastLocation = lastLocation;
        LastOutOfRangeLocationCheck = lastLocation;
    }

    /// <summary>
    /// 比较连接是否相等
    /// </summary>
    public bool Equals(UNetConnection other)
    {
        return Connection == other;
    }
}