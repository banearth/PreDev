using UnityEngine;

public class TestActor : FActorRepListType
{
    public string Id { get; private set; }
    public Vector3 Position { get; set; }
    public string Type { get; private set; }
    public bool IsDynamic { get; private set; }
    public string OwnedClientId { get; set; }
    public bool IsOwnedByClient => !string.IsNullOrEmpty(OwnedClientId);

    public Vector3 InitialPosition { get; private set; }  // 保存初始位置作为圆心
    public float MoveSpeed { get; set; }                 // 移动速度
    public float MoveRange { get; set; }                 // 运动半径
    private float _phaseOffset;                          // 每个Actor的相位偏移

	public TestActor(string id, Vector3 position, string type, bool isDynamic, float moveRange, float moveSpeed)
	{
        Id = id;
        Position = position;
        InitialPosition = position;
        Type = type;
        IsDynamic = isDynamic;
        MoveRange = moveRange;
        MoveSpeed = moveSpeed;
        _phaseOffset = Random.Range(0f, Mathf.PI * 2f); // 随机初始相位
        
        // 设置默认的网络属性
        NetUpdateFrequency = 30f;  // 30Hz更新
        NetCullDistanceSquared = 100 * 100;  // 100米裁剪距离
        bAlwaysRelevant = false;
        bOnlyRelevantToOwner = false;
    }

    public void UpdateMovement(float deltaTime)
    {
        if (!IsDynamic) return;

        _phaseOffset += deltaTime * MoveSpeed;
        var angle = _phaseOffset;
        Vector3 offset = new Vector3(
            Mathf.Sin(angle) * MoveRange,
            0,
            Mathf.Cos(angle) * MoveRange
        );

        Position = InitialPosition + offset;
    }

    public override void GetPlayerViewPoint(ref Vector3 viewPosition, ref Vector3 viewDir)
    {
        if (!IsDynamic)
        {
            viewPosition = Position;
            viewDir = Vector3.forward;
            return;
        }

        // 计算当前角度的切线方向
        viewDir = new Vector3(
            Mathf.Cos(_phaseOffset),
            0,
            -Mathf.Sin(_phaseOffset)
        ).normalized;

        // 当前位置就是视点位置
        viewPosition = Position;
    }
}