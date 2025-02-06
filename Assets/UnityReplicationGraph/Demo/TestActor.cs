using UnityEngine;

public class TestActor : FActorRepListType
{
    public bool IsMoving { get; set; }
    public Vector3 InitialPosition { get; set; }
    public float MoveRadius { get; set; }
    public float MoveSpeed { get; set; }

	public TestActor(string name, Vector3 position, float moveRadius = 10f, float moveSpeed = 5f)
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

    public override void GetPlayerViewPoint(ref Vector3 viewPosition, ref Vector3 viewDir)
    {
        // 当前角度
        float angle = Time.time * MoveSpeed;

		// 计算当前角度的切线方向
		viewDir = new Vector3(
            -Mathf.Sin(angle),
            0,
            Mathf.Cos(angle)
        ).normalized;

		// 当前的位置
		var selfPosition = InitialPosition + new Vector3(
		   Mathf.Cos(angle) * MoveRadius,
		   0,
		   Mathf.Sin(angle) * MoveRadius
	   );

       float time = 1;
       viewPosition = viewDir * MoveSpeed * time + selfPosition;
	}
}