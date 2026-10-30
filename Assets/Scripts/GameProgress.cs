using UnityEngine;

// VERSION 1
// Remembers the player's progress while moving from scene to scene (Classroom -> Cad_lab).
// It is "static": there is only one copy, it lives in memory (not on any object), so you don't
// put it in the scene. Other scripts just read/write GameProgress.riddlesCorrect etc.
public static class GameProgress
{
    public static bool riddlesDone;          // has the player finished the NPC's riddles?
    public static int riddlesCorrect;        // how many were right
    public static int riddlesTotal;          // how many were asked
    public static int computersToSwitchOff;  // the penalty chosen by the NPC from the score
    public static int computersSwitchedOff;  // filled in when the beeping puzzle is solved
    public static float startTime;           // Time.time when the run started (for the end screen)

    // Back to the start (Play Again button, and every time Play is pressed in the editor)
    public static void Reset()
    {
        riddlesDone = false;
        riddlesCorrect = 0;
        riddlesTotal = 0;
        computersToSwitchOff = 0;
        computersSwitchedOff = 0;
        startTime = Time.time;
    }

    // This project has "Enter Play Mode Options" on (no domain reload), which means static values
    // would survive from one Play press to the next. This makes Unity call Reset() at every Play.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        Reset();
        Debug.Log("[Scripts] GameProgress v1 reset");
    }
}
