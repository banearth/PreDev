using UnityEngine;

public class TestActor : FActorRepListType
{
    public bool IsMoving { get; set; }
    public Vector3 InitialPosition { get; set; }
    public float MoveRadius { get; set; }
    public float MoveSpeed { get; set; }
    
    public TestActor(Vector3 position, float moveRadius = 10f, float moveSpeed = 5f)
    {
        Position = position;
        InitialPosition = position;
        MoveRadius = moveRadius;
        MoveSpeed = moveSpeed;
        
        // 设置默认的网络属性
        NetUpdateFrequency = 30f;  // 30Hz更新
        NetCullDistanceSquared = 100 * 100;  // 100米裁剪距离
        bAlwaysRelevant = false;
        bOnlyRelevantToOwner = false;
    }

    public void UpdateMovement(float deltaTime)
    {
        if (!IsMoving) return;

        // 简单的圆周运动
        float angle = Time.time * MoveSpeed;
        Position = InitialPosition + new Vector3(
            Mathf.Cos(angle) * MoveRadius,
            0,
            Mathf.Sin(angle) * MoveRadius
        );
    }
} 