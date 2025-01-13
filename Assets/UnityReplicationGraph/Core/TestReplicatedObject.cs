using UnityEngine;

public class TestReplicatedObject : ReplicatedActor
{
    public string TestName { get; set; } = "TestObject";
    public float TestValue { get; set; } = 0f;
    public bool IsActive { get; set; } = true;
    public Vector3 Velocity { get; set; } = Vector3.zero;
    public float RotationSpeed { get; set; } = 10f;

    public TestReplicatedObject()
    {
        // 设置一些基类的属性默认值
        NetCullDistanceSquared = 1000f;
        bAlwaysRelevant = false;
        bOnlyRelevantToOwner = false;
    }

    public void Update()
    {
        // 简单的更新逻辑，用于测试
        Position += Velocity * Time.deltaTime;
        TestValue += RotationSpeed * Time.deltaTime;
    }
} 