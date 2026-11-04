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
