public class NetworkConnection
{
    public uint ConnectionId { get; private set; }
    public bool IsValid => ConnectionId != 0;

    public NetworkConnection(uint connectionId)
    {
        ConnectionId = connectionId;
    }

    public override bool Equals(object obj)
    {
        if (obj is NetworkConnection other)
        {
            return ConnectionId == other.ConnectionId;
        }
        return false;
    }

    public override int GetHashCode()
    {
        return ConnectionId.GetHashCode();
    }
} 