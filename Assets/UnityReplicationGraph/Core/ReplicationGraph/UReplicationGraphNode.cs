using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 复制图节点的基础抽象类
/// </summary>
public abstract class UReplicationGraphNode
{
    protected string nodeName;
    protected List<UReplicationGraphNode> AllChildNodes = new List<UReplicationGraphNode>();
    protected bool bRequiresPrepareForReplicationCall = false;
    protected FReplicationGraphGlobalData GraphGlobals;

    public UReplicationGraphNode()
    {
    }

    public virtual void Initialize(FReplicationGraphGlobalData inGraphGlobals)
    {
        GraphGlobals = inGraphGlobals;
    }

    public bool GetRequiresPrepareForReplication() => bRequiresPrepareForReplicationCall;

    public virtual void NotifyAddNetworkActor(FNewReplicatedActorInfo actorInfo)
    {
        foreach (var childNode in AllChildNodes)
        {
            childNode.NotifyAddNetworkActor(actorInfo);
        }
    }

    public virtual bool NotifyRemoveNetworkActor(FNewReplicatedActorInfo actorInfo, bool bWarnIfNotFound = true)
    {
        foreach (var childNode in AllChildNodes)
        {
            childNode.NotifyRemoveNetworkActor(actorInfo, bWarnIfNotFound);
        }
        return false;
    }

    public virtual void NotifyResetAllNetworkActors()
    {
        foreach (var childNode in AllChildNodes)
        {
            childNode.NotifyResetAllNetworkActors();
        }
    }

	public virtual void TearDown()
    {
		foreach (var Node in AllChildNodes)
		{
			Node.TearDown();
		}
		AllChildNodes.Clear();
	}

	public virtual void PrepareForReplication()
    {
        if (bRequiresPrepareForReplicationCall)
        {
            foreach (var childNode in AllChildNodes)
            {
                childNode.PrepareForReplication();
            }
        }
    }

    /// <summary>
    /// 为指定连接收集Actor列表的纯虚函数
    /// 每个子类必须实现此方法来定义自己的收集逻辑
    /// </summary>
    public abstract void GatherActorListsForConnection(FConnectionGatherActorListParameters Params);

    public virtual void GetAllActorsInNode_Debugging(List<FActorRepListType> outArray) { }

    public void VerifyActorReferences()
    {
        VerifyActorReferencesInternal();
        foreach(var Node in AllChildNodes)
        {
            Node.VerifyActorReferencesInternal();
        }
    }

    public UWorld GetWorld() => GraphGlobals?.World;

	public virtual void LogNode(FReplicationGraphDebugInfo DebugInfo, string NodeName)
	{
		DebugInfo.PushIndent();
		foreach (var ChildNode in AllChildNodes)
		{
			if (DebugInfo.bShowEmptyNodes == false)
			{
                var TempArray = new List<FActorRepListType>();
				ChildNode.GetAllActorsInNode_Debugging(TempArray);
				if (TempArray.Count == 0)
				{
					continue;
				}
			}
			ChildNode.LogNode(DebugInfo, ChildNode.GetDebugString());
		}
		DebugInfo.PopIndent();
	}

	public virtual string GetDebugString() => GetType().Name;

    protected T CreateChildNode<T>() where T : UReplicationGraphNode, new()
    {
        var newNode = new T();
        newNode.Initialize(GraphGlobals);
        AllChildNodes.Add(newNode);
        return newNode;
    }

    protected virtual void OnCollectActorRepListStats(FActorRepListStatCollector StatsCollector) { }

    protected virtual void VerifyActorReferencesInternal() { }

    protected bool VerifyActorReference(FActorRepListType Actor)
    {
        bool bIsValid = Actor != null;
		ReplicationGraphDebugger.EnsureMsg(bIsValid, string.Format("VerifyActorReference Invalid Actor in RepGraphNode: {0}", this.nodeName));
		return bIsValid;
    }

    
}