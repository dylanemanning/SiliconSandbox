using UnityEngine;

public class LogicClockSource : LogicSignalSource
{
    [Min(.01f)] public float frequencyHz = 1f;
    protected override void Update()
    {
        base.Update();
        SetState(Mathf.FloorToInt(Time.time * Mathf.Max(.01f,frequencyHz) * 2f) % 2);
    }
}
