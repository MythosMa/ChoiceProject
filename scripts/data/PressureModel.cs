using System;

/// <summary>
/// 压力读：不看招名，只统计"被压了多少下"。
/// 独立成纯 C# 类，方便单测，也避免 EnemyStateMachine 继续膨胀。
/// </summary>
public sealed class PressureModel
{
    public int Value { get; private set; }
    public int Max { get; }
    public bool IsFull => Value >= Max;

    private float _decayAccum;

    public PressureModel(int max = 3)
    {
        Max = Math.Max(1, max);
    }

    /// <summary>被命中一次 +1（到顶不溢出）。</summary>
    public void Add()
    {
        Value = Math.Min(Value + 1, Max);
    }

    /// <summary>每 secondsPerPoint 秒掉 1 格；按 delta 累积，掉格时补齐余数。</summary>
    public void Decay(float delta, float secondsPerPoint)
    {
        if (Value <= 0 || secondsPerPoint <= 0f)
        {
            if (Value <= 0) _decayAccum = 0f;
            return;
        }

        _decayAccum += delta;
        while (_decayAccum >= secondsPerPoint && Value > 0)
        {
            _decayAccum -= secondsPerPoint;
            Value--;
        }

        if (Value <= 0) _decayAccum = 0f;
    }

    public void Reset()
    {
        Value = 0;
        _decayAccum = 0f;
    }
}
