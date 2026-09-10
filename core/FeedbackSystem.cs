using Godot;
using System;

public partial class FeedbackSystem : Node
{
    public static FeedbackSystem Instance { get; private set; }
    private int _hitstopFrame = 0;

    private int _shakeFrames;
    private int _shakeFramesTotal;
    private float _shakeMagnitude;

    public override void _EnterTree()
    {
        if (Instance != null && Instance != this)
        {
            GD.Print("FeedbackSystem: 检测到重复实例，请确保只配置了一个 Autoload。");
        }
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void RequestHitstop(int frames)
    {
        if (frames > _hitstopFrame)
        {
            _hitstopFrame = frames;
        }
    }

    public bool ConsumeHitstop()
    {
        if (_hitstopFrame > 0)
        {
            GD.Print("Consuming hitstop frame.");
            _hitstopFrame--;
            return true;
        }
        return false;
    }

    public void RequestShake(int frames, float magnitude)
    {
        if (frames > _shakeFrames)
        {
            _shakeFrames = frames;
            _shakeFramesTotal = frames;
            _shakeMagnitude = magnitude;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_shakeFrames <= 0)
        {
            return;
        }
        Camera2D cam = GetViewport().GetCamera2D();
        _shakeFrames--;

        if (_shakeFrames <= 0 || cam == null)
        {
            if (cam != null)
            {
                cam.Offset = Vector2.Zero;
            }
            return;
        }
        float t = (float)_shakeFrames / _shakeFramesTotal;
        float mag = _shakeFrames * t;
        cam.Offset = new Vector2((float)GD.RandRange(-mag, mag), (float)GD.RandRange(-mag, mag));
    }
}
