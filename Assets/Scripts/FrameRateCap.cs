using UnityEngine;

// VERSION 1
// Caps the whole game at 60 frames per second. You do not put this in a scene: it runs by itself once, as the game starts.
// (V-Sync has to be off for the cap to count: with V-Sync on, Unity follows the monitor's refresh rate and ignores the target.)
// To change the cap, change TargetFps below.
public static class FrameRateCap
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = TargetFps;
    }
}
