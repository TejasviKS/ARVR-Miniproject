// VERSION 1
// How hard the boss fight is. EVERY number that changes with difficulty lives here, so adding a harder mode later is a
// matter of adding one more line to each property (and, when there is a main menu, setting BossDifficulty.Current from it).
//
//   HpMultiplier   : scales Prof. Claude's health (Easy = 600 HP, about a 3 minute fight)
//   Tempo          : attack speed. Telegraphs, recoveries and cooldowns are DIVIDED by it, so 1.3 is a 30% faster fight
//   DamageFactor   : scales the damage of his attacks
//
// BossFight.cs reads these every time it applies a phase, so nothing else needs to know about difficulty.
public static class BossDifficulty
{
    public enum Level { Easy, Hard }

    // For now the game is always Easy (a quick fight for presenting it). Hard is ready for the day there is a menu to pick it.
    public static Level Current = Level.Easy;

    public static float HpMultiplier => Current == Level.Hard ? 1.8f : 1f;
    public static float Tempo => Current == Level.Hard ? 1.3f : 1f;
    public static float DamageFactor => Current == Level.Hard ? 1.15f : 1f;
    public static int PenMarks => Current == Level.Hard ? 4 : 3;                  // how many red pen marks Opus drops at once
}
