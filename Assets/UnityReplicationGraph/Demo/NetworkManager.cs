using ReplicationGraphVisualizer;
using UnityEngine;
using UnityEngine.UIElements;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }
    
    public UNetworkDriver Driver { get; private set; }
    public UReplicationGraph ReplicationGraph { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeNetwork();
		}
        else
        {
            Destroy(gameObject);
        }
    }

	private void InitializeNetwork()
    {
        // 创建网络驱动
        Driver = new UNetworkDriver();
        
        // 创建复制图
        ReplicationGraph = new UBasicReplicationGraph();
        
        // 初始化复制驱动
        Driver.InitReplicationDriver(ReplicationGraph);
    }

    public void DoUpdate()
    {
        // 更新网络状态
        Driver.TickFlush(Time.deltaTime);
    }

	public void SpawnNetworkActor(FActorRepListType actor, string id, string type)
	{
        // 添加actor到复制图中
        ReplicationGraph.AddNetworkActor(actor);
        // 可视化
        var position = actor.Position;
		Visualizer.AddGlobalObservee(
			id,
	        position.x,
	        position.y,
	        position.z,
	        type
        );

	}
}