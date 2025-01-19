using UnityEngine;
using System.Collections.Generic;
using System;

public abstract class UReplicationGraph : UReplicationDriver
{
    protected List<UReplicationGraphNode> GlobalGraphNodes = new List<UReplicationGraphNode>();
    protected Dictionary<Type, FClassReplicationInfo> GlobalClassInfoMap = new Dictionary<Type, FClassReplicationInfo>();
    protected FGlobalActorReplicationInfoMap GlobalActorReplicationInfoMap = new FGlobalActorReplicationInfoMap();
	protected HashSet<FActorRepListType> ActiveNetworkActors;

	protected List<UReplicationGraphNode> PrepareForReplicationNodes = new List<UReplicationGraphNode>();
	protected List<UNetReplicationGraphConnection> Connections = new List<UNetReplicationGraphConnection>();
	protected List<UNetReplicationGraphConnection> PendingConnections = new List<UNetReplicationGraphConnection>();
	protected int HeavyComputationConnectionSelector;
    protected uint ReplicationGraphFrame;
    protected bool bWasConnectionSaturated;
    protected bool bWasConnectionFastPathSaturated;

	protected class FFastSharedPathConstants
    {
		public float DistanceRequirementPct = 0.1f;    // 必须达到足够接近的距离，作为剔除距离的平方比例，才能使用快速共享复制路径
		public int MaxBitsPerFrame = 2048;           // 5kBytes/sec @ 20hz
		public int ListSkipPerFrame = 3;
    }
    protected FFastSharedPathConstants FastSharedPathConstants = new FFastSharedPathConstants();

	/** 
	 * 在没有进行复制的情况下，连接上的Actor通道关闭前的帧数。 
	 * 这是一个全局值，会加到每个单独Actor的ActorChannelFrameTimeout上。
	 */
	protected uint GlobalActorChannelFrameNumTimeout = 2;

	// 全局数据
	protected FReplicationGraphGlobalData GraphGlobals;

    public FReplicationGraphGlobalData GetGraphGlobals()
    { 
        return GraphGlobals; 
    }

    public UWorld GetWorld()
    {
        return GraphGlobals?.World;
    }

    // todo
    public void SetRepDriverWorld(UWorld InWorld)
    {
        if (GraphGlobals != null)
        {
            GraphGlobals.World = InWorld;
        }
    }

    public virtual void InitForNetDriver(UNetworkDriver driver)
    {
		this.NetDriver = driver;
		InitGlobalActorClassSettings();
        InitGlobalGraphNodes();
		foreach (var ClientConnection in NetDriver.ClientConnections)
		{
			AddClientConnection(ClientConnection);
		}
	}

    public FNewReplicatedActorInfo CreateReplicatedActorInfo(FActorRepListType actor)
    {
        return new FNewReplicatedActorInfo(actor);
    }

	/// <summary>
	/// 为复制图创建新节点。这个方法和UReplicationNode.CreateChildNode
	/// 应该是创建图节点对象的唯一方式
	/// </summary>
	/// <typeparam name="T">节点类型，必须继承自UReplicationGraphNode</typeparam>
	/// <returns>新创建的节点实例</returns>
	protected T CreateNewNode<T>() where T : UReplicationGraphNode, new()
	{
		// 创建新节点实例
		var newNode = new T();
		// 初始化节点
		InitNode(newNode);
		return newNode;
	}

	protected void AddGlobalGraphNode(UReplicationGraphNode node)
    {
        GlobalGraphNodes.Add(node);
    }

	protected void AddConnectionGraphNode(UReplicationGraphNode GraphNode, UNetReplicationGraphConnection ConnectionManager)
	{
		ConnectionManager.AddConnectionGraphNode(GraphNode);
	}

	public override void AddClientConnection(UNetConnection netConnection)
    {
        // 检查是否已经在待处理列表中创建了该连接的管理器
        for (int i = PendingConnections.Count - 1; i >= 0; --i)
        {
            var connManager = PendingConnections[i];
            if (connManager != null && connManager.NetConnection == netConnection)
            {
                PendingConnections.RemoveAtSwap(i);
                Connections.Add(connManager);
                return;
            }
        }

        // 创建新的连接管理器
        //haha
        Connections.Add(CreateClientConnectionManagerInternal(netConnection));
    }

	private float DestructInfoMaxDistanceSquared = 15000f * 15000f;

	public override int ServerReplicateActors(float deltaSeconds)
    {
        // 增加复制帧计数
        NetDriver.ReplicationFrame++;  
        uint frameNum = ReplicationGraphFrame;
		ReplicationGraphFrame++;

		bWasConnectionSaturated = false;
        bWasConnectionFastPathSaturated = false;

        var connectionsToClose = new List<UNetConnection>();

        // 准备阶段(全局)
        foreach (var node in PrepareForReplicationNodes)
        {
            node.PrepareForReplication();
        }

        // 处理每个连接
        int numChildrenConnectionsProcessed = 0;
        HeavyComputationConnectionSelector = (HeavyComputationConnectionSelector + 1) % Connections.Count;

        foreach (var ConnectionManager in Connections)
        {
            // 准备复制,同时处理子连接
            if (!ConnectionManager.PrepareForReplication())
            {
                continue;
            }
			List<FNetViewer> ConnectionViewers = new List<FNetViewer>();
            var NetConnection = ConnectionManager.NetConnection;
            var connectionActorInfoMap = ConnectionManager.ActorInfoMap;

            // 只添加一个
			ConnectionViewers.Add(new FNetViewer(NetConnection));
            numChildrenConnectionsProcessed++;

			FReplicationGraphDestructionSettings DestructionSettings = new FReplicationGraphDestructionSettings(
                DestructInfoMaxDistanceSquared,
				ReplicationGraphDebugger.CVar_RepGraph_OutOfRangeDistanceCheckRatio * DestructInfoMaxDistanceSquared);

			FGatheredReplicationActorLists GatheredReplicationListsForConnection = new FGatheredReplicationActorLists();

			// 构建复制参数
			var Parameters = new FConnectionGatherActorListParameters(
				ConnectionViewers,
				ConnectionManager,
				ConnectionManager.GetCachedClientVisibleLevelNames(),
				frameNum,
				GatheredReplicationListsForConnection,
				false
				);

			List<FRepGraphDestructionViewerInfo> DestructionViewersInfo = new List<FRepGraphDestructionViewerInfo>();

			// 收集需要复制的Actor列表
			foreach (var Node in GlobalGraphNodes)
            {
                Node.GatherActorListsForConnection(Parameters);
            }

			foreach (var Node in ConnectionManager.ConnectionGraphNodes)
			{
				Node.GatherActorListsForConnection(Parameters);
			}

			ConnectionManager.UpdateGatherLocationsForConnection(ConnectionViewers, DestructionSettings);

            if (GatheredReplicationListsForConnection.NumLists() == 0)
            {
                ReplicationGraphDebugger.LogWarning("No Replication Lists were returned for connection");
				continue;
            }

			foreach (var NetViewer in ConnectionViewers)
			{
				FLastLocationGatherInfo LastInfoForViewer = ConnectionManager.LastGatherLocations.Find(temp=>temp.Connection == NetViewer.Connection);
				DestructionViewersInfo.Add(new FRepGraphDestructionViewerInfo(NetViewer.ViewLocation, LastInfoForViewer.LastOutOfRangeLocationCheck));
			}

			// --------------------------------------------------------------------------------------------------------------
			// PROCESS gathered replication lists
			// --------------------------------------------------------------------------------------------------------------

			ReplicateActorListsForConnections_Default(ConnectionManager, GatheredReplicationListsForConnection, ConnectionViewers);

			if (Parameters.OutGatheredReplicationLists.NumLists() > 0)
            {
                ReplicateActorsForConnection(
                    NetConnection, 
                    connectionActorInfoMap,
                    ConnectionManager, 
                    frameNum);
            }

			// 检查连接状态
			if (NetConnection.State == UNetConnection.EConnectionState.USOCK_Closed)
			{
                connectionsToClose.Add(NetConnection);
            }
        }
        // 清理已关闭的连接
        foreach (var conn in connectionsToClose)
        {
			conn.Close();
        }
        return numChildrenConnectionsProcessed;
    }

    public virtual void RouteAddNetworkActorToNodes(FNewReplicatedActorInfo actorInfo, FGlobalActorReplicationInfo globalInfo)
    {
        // 通知所有全局节点
        foreach (var node in GlobalGraphNodes)
        {
            node.NotifyAddNetworkActor(actorInfo);
        }
    }

    public virtual void RouteRemoveNetworkActorToNodes(FNewReplicatedActorInfo actorInfo)
    {
        foreach (var node in GlobalGraphNodes)
        {
            node.NotifyRemoveNetworkActor(actorInfo);
        }
    }

	public bool IsConnectionReady(UNetConnection Connection)
	{
		return true;
	}

	public void InitNode(UReplicationGraphNode Node)
	{
		Node.Initialize(GraphGlobals);
		if (Node.GetRequiresPrepareForReplication())
		{
			PrepareForReplicationNodes.Add(Node);
		}
	}

	public virtual void InitGlobalActorClassSettings(){ }
    protected abstract void InitGlobalGraphNodes();

    protected virtual void InitConnectionGraphNodes(UNetReplicationGraphConnection ConnectionManager)
    {
		// 这个方法处理断开(TearOff)的Actor
		// 子类应该调用 base.InitConnectionGraphNodes()
		// 创建一个专门处理断开Actor的节点
		ConnectionManager.TearOffNode = CreateNewNode<UReplicationGraphNode_TearOff_ForConnection>();
		// 将断开节点添加到连接的图节点列表中
		ConnectionManager.AddConnectionGraphNode(ConnectionManager.TearOffNode);
	}

    protected int GetReplicationPeriodFrameForFrequency(float frequency)
    {
        const float ServerMaxTickRate = 60.0f;
        return Mathf.Max(1, Mathf.RoundToInt(ServerMaxTickRate / frequency));
    }

	protected virtual UNetReplicationGraphConnection CreateClientConnectionManagerInternal(UNetConnection connection)
	{
		// 创建连接管理器对象
		var newConnectionManager = new UNetReplicationGraphConnection();

		// 分配ID
		int newConnectionNum = Connections.Count + PendingConnections.Count;
		newConnectionManager.ConnectionOrderNum = newConnectionNum;

		// 初始化图关联
		newConnectionManager.InitForGraph(this);

		// 关联网络连接
		newConnectionManager.InitForConnection(connection);

		// 为这个特定连接创建图节点
		InitConnectionGraphNodes(newConnectionManager);

		return newConnectionManager;
	}

    FPrioritizedRepList PrioritizedReplicationList = new FPrioritizedRepList();

	/** Default Replication Path */
	void ReplicateActorListsForConnections_Default(
        UNetReplicationGraphConnection ConnectionManager, 
        FGatheredReplicationActorLists GatheredReplicationListsForConnection, 
        List<FNetViewer> Viewers)
	{
        bool bEnableFullActorPrioritizationDetails = false;  // 是否启用详细的Actor优先级信息
        bool bDoDistanceCull = true;                         // 是否启用距离裁剪
        bool bDoCulledOnConnectionCount = false;             // 是否统计连接裁剪数量

        // 连接统计
        int NumGatheredActorsOnConnection = 0;    // 此连接收集到的Actor总数
        int NumPrioritizedActorsOnConnection = 0; // 此连接优先级排序后的Actor数

        // 获取基本信息
        var NetConnection = ConnectionManager.NetConnection;
        var ConnectionActorInfoMap = ConnectionManager.ActorInfoMap;
        uint FrameNum = ReplicationGraphFrame;

		PrioritizedReplicationList.Reset();
        var SortingArray = PrioritizedReplicationList.Items;

		var MaxDistanceScaling = PrioritizationConstants.MaxDistanceScaling;
		var MaxFramesSinceLastRep = PrioritizationConstants.MaxFramesSinceLastRep;

		// Add actors from gathered list
		var Actors = GatheredReplicationListsForConnection.ViewActors(EActorRepListTypeFlags.Default);
		NumGatheredActorsOnConnection += Actors.Count;

        foreach(var Actor in Actors)
        {
			if (!ReplicationGraphDebugger.EnsureMsg(ReplicationGraphUtils.IsActorValidForReplication(Actor), "Actor not valid for replication"))
			{
                continue;
            }
			// -----------------------------------------------------------------------------------------------------------------
			// 为连接计算Actor的优先级：这是计算Actor最终优先级分数的主要代码块
			// - 这部分代码还比较粗糙。如果能让每个项目自定义这部分逻辑而不需要使用虚函数调用就更好了。
			// -----------------------------------------------------------------------------------------------------------------
			FConnectionReplicationActorInfo ConnectionData = ConnectionActorInfoMap.FindOrAdd(Actor);

			// 跳过在此连接上处于休眠状态的Actor。我们希望这始终是第一个/最快的检查。
			if (ConnectionData.bDormantOnConnection)
			{
				continue;
			}

			FGlobalActorReplicationInfo GlobalData = GlobalActorReplicationInfoMap.Get(Actor);

			// 跳过尚未到达复制时机的Actor。这里需要检查ForceNetUpdateFrame。
			// 理论上可以在调用ForceNetUpdate时清除所有连接的NextReplicationFrameNum，
			// 但这可能会增加每帧的总体工作量。这是一个需要权衡的点。
			if(!ReadyForNextReplication(ConnectionData, GlobalData, FrameNum))
			{
				continue;
			}

			float AccumulatedPriority = GlobalData.Settings.AccumulatedNetPriorityBias;

			// -------------------
			// 距离缩放
			// -------------------
			if (GlobalData.Settings.DistancePriorityScale > 0f)
            {
				// 即使是AlwaysRelevant的Actor也要计算距离，因为优先级缩放需要它
				float SmallestDistanceSq = float.MaxValue;
				int ViewersThatSkipActor = 0;
				foreach (var curViewer in Viewers)
				{
					float distSq = (GlobalData.WorldLocation - curViewer.ViewLocation).sqrMagnitude;
					SmallestDistanceSq = Mathf.Min(distSq, SmallestDistanceSq);
					// 判断是否应该跳过这个Actor
					if (bDoDistanceCull &&
						ConnectionData.GetCullDistanceSquared() > 0f &&
						distSq > ConnectionData.GetCullDistanceSquared())
					{
						++ViewersThatSkipActor;
						continue;
					}
				}
				// 如果没有观察者在这个Actor附近，跳过它
				if (ViewersThatSkipActor >= Viewers.Count)
				{
					continue;
				}
				// 计算距离因子并应用到优先级
				float DistanceFactor = Mathf.Clamp(SmallestDistanceSq / MaxDistanceScaling, 0f, 1f)
									  * GlobalData.Settings.DistancePriorityScale;
				AccumulatedPriority += DistanceFactor;
			}

			// 在这里更新超时帧号。
			// (因为这个Actor是由Graph返回的，无论我们最终是否复制它，都要增加超时帧号。
			// 必须在这里做是因为距离缩放可能会裁剪掉这个Actor)
			UpdateActorChannelCloseFrameNum(Actor, ConnectionData, GlobalData, FrameNum, NetConnection);

			// -------------------
			// 饥饿缩放
			// 这段代码的作用是防止Actor被"饿死"（长时间不被复制）：
			// 基本原理：
			// 越长时间没有被复制的Actor
			// 它的优先级就会越来越高
			// 确保最终一定会被复制
			// -------------------
			if (GlobalData.Settings.StarvationPriorityScale > 0f)
			{
				// StarvationPriorityScale = 缩放"自上次复制以来的帧数"
				// 例如，2.0意味着将每个错过的帧视为2帧，以此类推
				float FramesSinceLastRep = (FrameNum - ConnectionData.LastRepFrameNum) *
										  GlobalData.Settings.StarvationPriorityScale;
				float StarvationFactor = 1f - Mathf.Clamp(
					FramesSinceLastRep / MaxFramesSinceLastRep,
					0f,
					1f);
				AccumulatedPriority += StarvationFactor;
			}

			// ------------------------
			// 待休眠状态的优先级调整
			// ------------------------
			// 确保已经至少复制过一次且待休眠的Actor获得更高优先级，
			// 这样我们可以快速将它们标记为休眠，跳过后续工作，并关闭它们的通道。
			// 否则，新生成或从未复制过的Actor可能会"饿死"那些正在尝试进入休眠状态的现有Actor。
			if (GlobalData.bWantsToBeDormant && ConnectionData.LastRepFrameNum > 0)
			{
				AccumulatedPriority -= 1.5f;
			}
			// -------------------
			// 游戏代码优先级
			// -------------------
			if (GlobalData.ForceNetUpdateFrame > ConnectionData.LastRepFrameNum)
			{
				// 注意：在旧版本的ForceNetUpdate中实际上并不会提升优先级。
				// 这里给出一个硬编码的优先级提升，如果我们在上次ForceNetUpdate帧之后还没有复制过。
				AccumulatedPriority -= 1f;  // 降低优先级值意味着更高的复制优先级
			}

			// -------------------
			// 始终优先处理连接的所有者和视角目标，因为这些是对客户端最重要的Actor。
			// -------------------
			foreach (var curViewer in Viewers)
			{
				// 我们需要找出这是否是任何人的观察者或视角目标，而不仅仅是父连接。
				if (Actor == curViewer.ViewTarget || Actor == curViewer.InViewer)
				{
					if (ReplicationGraphDebugger.CVar_ForceConnectionViewerPriority > 0)
					{
						AccumulatedPriority = float.MinValue;  // 最高优先级
					}
					else
					{
						AccumulatedPriority -= 10.0f;  // 显著提高优先级
					}
					break;
				}
			}
			SortingArray.Add(new FPrioritizedRepList.FItem(AccumulatedPriority, Actor, GlobalData, ConnectionData));
			// 对合并后的优先级列表进行排序
			// 我们可以考虑把这个移到下面的复制循环中
			// 这可能可以让我们避免对超出预算的数组进行排序
			NumPrioritizedActorsOnConnection += SortingArray.Count;
			SortingArray.Sort();  // 按优先级排序
		}
		//haha
		ReplicateActorsForConnection(NetConnection, ConnectionActorInfoMap, ConnectionManager, FrameNum);

	}

	public void ReplicateActorListsForConnections_FastShared(
		UNetReplicationGraphConnection connectionManager,
		FGatheredReplicationActorLists gatheredLists,
		List<FNetViewer> viewers)
	{
		// 检查是否启用快速共享路径
		if (!ReplicationGraphDebugger.CVar_RepGraph_EnableFastSharedPath)
			return;

        // 检查是否包含快速共享类型的列表
        if (!gatheredLists.ContainsLists(EActorRepListTypeFlags.FastShared))
            return;

        var connectionActorInfoMap = connectionManager.ActorInfoMap;
        var netConnection = connectionManager.NetConnection;
        uint frameNum = ReplicationGraphFrame;
        float fastSharedDistanceRequirementPct = FastSharedPathConstants.DistanceRequirementPct;
        int maxBits = FastSharedPathConstants.MaxBitsPerFrame;
        int startIdx = (int)(frameNum * FastSharedPathConstants.ListSkipPerFrame);

        int totalBitsWritten = 0;

        // 处理Actor列表
        var actors = gatheredLists.ViewActors(EActorRepListTypeFlags.FastShared);
        for (int i = 0; i < actors.Count; i++)
        {
            // 轮询列表中的Actor
            var actor = actors[(startIdx + i) % actors.Count];
            int bitsWritten = 0;
            var connectionData = connectionActorInfoMap.FindOrAdd(actor);

            // 跳过已经在这一帧复制过的Actor
            if (connectionData.LastRepFrameNum == frameNum)
                continue;

            // 跳过标记为TearOff的Actor
            if (connectionData.bTearOff)
                continue;

            // 检查Actor通道
            var actorChannel = connectionData.Channel;
            if (actorChannel == null)
                continue;

            var globalActorInfo = GlobalActorReplicationInfoMap.Get(actor);
            if (globalActorInfo.Settings.FastSharedReplicationFunc == null)
                continue;

            // 检查视图相关性
            bool bNoViewRelevancy = true;
            foreach (var viewer in viewers)
            {
                var connectionViewLocation = viewer.ViewLocation;
                var connectionViewDir = viewer.ViewDir;

                // 点积检查：只复制在连接前方的Actor
                var dirToActor = globalActorInfo.WorldLocation - connectionViewLocation;
                if (Vector3.Dot(dirToActor, connectionViewDir) >= 0f)
                {
                    bNoViewRelevancy = false;
                    break;
                }

                // 距离检查
                float distSq = dirToActor.sqrMagnitude;
                if (distSq <= (connectionData.GetCullDistanceSquared() * fastSharedDistanceRequirementPct))
                {
                    bNoViewRelevancy = false;
                    break;
                }
            }

            if (bNoViewRelevancy)
                continue;

            // 执行快速共享复制
            bitsWritten = ReplicateSingleActor_FastShared(
                actor,
                connectionData,
                globalActorInfo,
                connectionManager,
                frameNum);

            totalBitsWritten += bitsWritten;

            // 检查是否超过最大比特数
            if (totalBitsWritten > maxBits)
            {
                // NotifyConnectionFastPathSaturated();
                break;
            }
        }
        //netConnection.QueuedBits -= totalBitsWritten;
    }

	protected bool ReadyForNextReplication(
			FConnectionReplicationActorInfo ConnectionData,
			FGlobalActorReplicationInfo GlobalData,
			uint FrameNum)
	{
		// 如果设置了强制更新，或者已经到达了下一次复制帧，则可以复制
		return (ConnectionData.NextReplicationFrameNum <= FrameNum ||
			GlobalData.ForceNetUpdateFrame > ConnectionData.LastRepFrameNum);
	}

	protected virtual void UpdateActorChannelCloseFrameNum(
		FActorRepListType Actor,
		FConnectionReplicationActorInfo ConnectionData,
		FGlobalActorReplicationInfo GlobalData,
		uint FrameNum,
		UNetConnection NetConnection)
	{
		// 只有当Actor设置了超时时间时才更新
		if (GlobalData.Settings.ActorChannelFrameTimeout > 0)
		{
			// 计算新的关闭帧号 = 当前帧 + 复制周期 + Actor超时设置 + 全局超时设置
			uint NewCloseFrameNum = FrameNum +
								   ConnectionData.ReplicationPeriodFrame +
								   GlobalData.Settings.ActorChannelFrameTimeout +
								   GlobalActorChannelFrameNumTimeout;

			// 永远不要后退，其他地方可能已经intentionally把它设置得更远
			ConnectionData.ActorChannelCloseFrameNum = Math.Max(
				ConnectionData.ActorChannelCloseFrameNum,
				NewCloseFrameNum);
		}
	}

	protected virtual void ReplicateActorsForConnection(
		UNetConnection NetConnection,
		FPerConnectionActorInfoMap ConnectionActorInfoMap,
		UNetReplicationGraphConnection ConnectionManager,
		uint FrameNum)
	{
		for (int ActorIdx = 0; ActorIdx < PrioritizedReplicationList.Items.Count; ++ActorIdx)
		{
			var RepItem = PrioritizedReplicationList.Items[ActorIdx];
			var Actor = RepItem.Actor;
			var ActorInfo = RepItem.ConnectionData;

			// 如果这一帧已经复制过就跳过。
			// 当一个Actor出现在多个复制列表中时会发生这种情况
			if (ActorInfo.LastRepFrameNum == FrameNum)
			{
				continue;
			}

			var GlobalActorInfo = RepItem.GlobalData;

			// 复制单个Actor并获取写入的比特数
			long BitsWritten = ReplicateSingleActor(
				Actor,
				ActorInfo,
				GlobalActorInfo,
				ConnectionActorInfoMap,
				ConnectionManager,
				FrameNum);

			// --------------------------------------------------
			// 更新数据包预算跟踪
			// --------------------------------------------------
			if (!IsConnectionReady(NetConnection))
			{
				break;
			}
		}
	}

	public int ReplicateSingleActor_FastShared(
	FActorRepListType actor,
	FConnectionReplicationActorInfo connectionData,
	FGlobalActorReplicationInfo globalActorInfo,
	UNetReplicationGraphConnection connectionManager,
	uint frameNum)
	{
		var netConnection = connectionManager.NetConnection;
		// 更新帧号
		connectionData.FastPath_LastRepFrameNum = frameNum;
		connectionData.FastPath_NextReplicationFrameNum = frameNum + connectionData.FastPath_ReplicationPeriodFrame;
		var actorChannel = connectionData.Channel;
		if (actorChannel == null)
		{
			return 0;
		}
		// 确保共享复制信息有效
		if (globalActorInfo.FastSharedReplicationInfo == null)
		{
			globalActorInfo.FastSharedReplicationInfo = new FFastSharedReplicationInfo();
		}
		var outBunch = globalActorInfo.FastSharedReplicationInfo.Bunch;

		// 如果这一帧还没有尝试构建共享数据
		if (globalActorInfo.FastSharedReplicationInfo.LastAttemptBuildFrameNum < frameNum)
		{
			globalActorInfo.FastSharedReplicationInfo.LastAttemptBuildFrameNum = frameNum;

			if (globalActorInfo.Settings.FastSharedReplicationFunc == null)
			{
				return 0;
			}

			if (!globalActorInfo.Settings.FastSharedReplicationFunc(actor))
			{
				return 0;
			}

			globalActorInfo.FastSharedReplicationInfo.LastBunchBuildFrameNum = frameNum;
		}

		if (connectionData.FastPath_LastRepFrameNum >= globalActorInfo.FastSharedReplicationInfo.LastBunchBuildFrameNum)
		{
			return 0;
		}

		actorChannel.SendBunch(outBunch);
		return outBunch;
	}

	protected virtual long ReplicateSingleActor(
		FActorRepListType Actor,
		FConnectionReplicationActorInfo ActorInfo,
		FGlobalActorReplicationInfo GlobalActorInfo,
		FPerConnectionActorInfoMap ConnectionActorInfoMap,
		UNetReplicationGraphConnection ConnectionManager,
		uint FrameNum)
	{
		if (!ReplicationGraphDebugger.EnsureMsg(Actor != null, "Null Actor! ReplicateSingleActor"))
		{
			return 0;
		}
		var NetConnection = ConnectionManager.NetConnection;
		if (!ReplicationGraphDebugger.EnsureMsg(ReplicationGraphUtils.IsActorValidForReplication(Actor), "Actor not valid for replication"))
		{
			return 0;
		}
		// 更新复制统计
		ActorInfo.LastRepFrameNum = FrameNum;
		ActorInfo.NextReplicationFrameNum = FrameNum + ActorInfo.ReplicationPeriodFrame;

		// 调用PreReplication
		if (GlobalActorInfo.LastPreReplicationFrame != FrameNum)
		{
			GlobalActorInfo.LastPreReplicationFrame = FrameNum;
			Actor.CallPreReplication(NetDriver);
		}

		bool bWantsToGoDormant = GlobalActorInfo.bWantsToBeDormant;
		bool bOpenActorChannel = (ActorInfo.Channel == null);

		// 创建新通道
		if (bOpenActorChannel)
		{
			ActorInfo.Channel = NetConnection.CreateChannel();
			ActorInfo.Channel.SetChannelActor(Actor);
		}

		// 处理休眠状态
		if (bWantsToGoDormant)
		{
			ActorInfo.Channel.StartBecomingDormant();
		}

		// 复制Actor状态
		long BitsWritten = 0;
		double startTime = Time.realtimeSinceStartup;

		if (ActorInfo.bTearOff)
		{
			// 复制并立即关闭通道
			BitsWritten = ActorInfo.Channel.ReplicateActor();
			BitsWritten += ActorInfo.Channel.Close(EChannelCloseReason.TearOff);
		}
		else
		{
			// 正常复制
			BitsWritten = ActorInfo.Channel.ReplicateActor();
		}

		double deltaTime = Time.realtimeSinceStartup - startTime;
		bool bWasDataSent = BitsWritten > 0;

		// 处理依赖Actor
		var listGatherer = new FGatheredReplicationActorLists();
		GlobalActorInfo.GatherDependentActorLists(ConnectionManager, listGatherer);

		if (listGatherer.NumLists() > 0)
		{
			uint closeFrameNum = ActorInfo.ActorChannelCloseFrameNum;
			var dependentActors = listGatherer.ViewActors(EActorRepListTypeFlags.Default);

			foreach (var dependentActor in dependentActors)
			{
				var dependentActorInfo = ConnectionActorInfoMap.FindOrAdd(dependentActor);
				var dependentGlobalInfo = GlobalActorReplicationInfoMap.Get(dependentActor);

				UpdateActorChannelCloseFrameNum(
					dependentActor,
					dependentActorInfo,
					dependentGlobalInfo,
					FrameNum,
					NetConnection);

				// 依赖Actor的通道保持开启，直到拥有者的通道关闭
				dependentActorInfo.ActorChannelCloseFrameNum =
					Math.Max(closeFrameNum, dependentActorInfo.ActorChannelCloseFrameNum);

				if (!ReadyForNextReplication(dependentActorInfo, dependentGlobalInfo, FrameNum))
				{
					continue;
				}
				BitsWritten += ReplicateSingleActor(
					dependentActor,
					dependentActorInfo,
					dependentGlobalInfo,
					ConnectionActorInfoMap,
					ConnectionManager,
					FrameNum);
			}
		}
		return BitsWritten;
	}

    public virtual void AddNetworkActor(FActorRepListType actor)
    {
        if (actor == null)
            return;

        // 创建或获取Actor的全局复制信息
        var globalInfo = GlobalActorReplicationInfoMap.Get(actor);
        
        // 创建新的复制Actor信息
        var actorInfo = new FNewReplicatedActorInfo(actor);
        
        // 将Actor添加到活动网络Actor列表
        ActiveNetworkActors.Add(actor);
        
        // 通知所有节点有新的网络Actor加入
        RouteAddNetworkActorToNodes(actorInfo, globalInfo);
    }

}