public class FConnectionAlwaysRelevantNodePair
{
    // 网络连接
    public UNetConnection NetConnection { get; private set; }

    // 对应的始终相关节点
    public UReplicationGraphNode_AlwaysRelevant_ForConnection Node { get; private set; }

    public FConnectionAlwaysRelevantNodePair()
    {
    }

    public FConnectionAlwaysRelevantNodePair(UNetConnection inConnection, UReplicationGraphNode_AlwaysRelevant_ForConnection inNode)
    {
        NetConnection = inConnection;
        Node = inNode;
    }

    // 实现与UNetConnection的比较
    public static bool operator ==(FConnectionAlwaysRelevantNodePair pair, UNetConnection connection)
    {
        return pair != null && pair.NetConnection == connection;
    }

    public static bool operator !=(FConnectionAlwaysRelevantNodePair pair, UNetConnection connection)
    {
        return !(pair == connection);
    }

    public override bool Equals(object obj)
    {
        if (obj is UNetConnection connection)
        {
            return this == connection;
        }
        return base.Equals(obj);
    }

    public override int GetHashCode()
    {
        return NetConnection?.GetHashCode() ?? 0;
    }
}