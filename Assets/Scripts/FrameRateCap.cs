using UnityEngine;

// VERSION 2
// Keeps the game at about 60 frames per second and in step with the monitor, so the picture never tears. You do not put this in a scene:
// it runs by itself once, as the game starts.
//
// V-Sync makes every frame wait for the monitor's refresh, which is what stops screen tearing (version 1 had V-Sync off, and tore
// badly whenever a frame took longer than usual, e.g. when a portal opens or is walked through). With V-Sync on Unity follows the monitor and
// ignores Application.targetFrameRate, so the 60 FPS cap is done by choosing how many refreshes to wait for each frame:
//   60 Hz monitor -> every refresh (60 FPS)      120 / 144 Hz -> every 2nd refresh (60 / 72 FPS)      240 Hz -> every 4th (60 FPS)
public static class FrameRateCap
{
    public const int TargetFps = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Apply()
    {
        double refresh = Screen.currentResolution.refreshRateRatio.value;
        if (refresh < 1.0) refresh = TargetFps;                                   // (unknown: assume 60 Hz)
        QualitySettings.vSyncCount = Mathf.Clamp(Mathf.RoundToInt((float)(refresh / TargetFps)), 1, 4);
        Application.targetFrameRate = -1;                                         // (ignored with V-Sync on anyway)
    }
}
