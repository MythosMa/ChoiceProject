using Godot;

/// <summary>
/// 统一的高频调试日志开关。
/// 每次命中（霸体检查）、每次切态这类日志在调参时会淹没控制台，
/// 默认静音；排查问题时把 Verbose 置 true 即可恢复。
/// </summary>
public static class DebugLog
{
    public static bool Verbose = false;

    public static void V(string message)
    {
        if (Verbose)
        {
            GD.Print(message);
        }
    }
}
