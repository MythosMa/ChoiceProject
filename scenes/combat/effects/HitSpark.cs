using Godot;
using System;

public partial class HitSpark : Node2D
{
    private CpuParticles2D _p;
    public override void _Ready()
    {
        _p = GetNodeOrNull<CpuParticles2D>("CPUParticles2D");
        if (_p == null)
        {
            QueueFree();
        }
    }

    /// <summary>按招式配置火花：颜色 + 粒子数（轻招小而密，重招大而散）。</summary>
    public void Configure(Color color, int amount)
    {
        if (_p == null)
        {
            return;
        }
        _p.Color = color;
        _p.Amount = Mathf.Max(4, amount);
    }

    public void Play()
    {
        if (_p != null)
        {
            _p.Emitting = true;
            _p.Restart();
            GetTree().CreateTimer(_p.Lifetime + 0.2).Timeout += () => QueueFree();
        }
    }
}
