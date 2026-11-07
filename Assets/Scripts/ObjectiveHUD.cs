using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 2
// The objective of a level, shown in the objective strip in the top left ("▶  Find a way out of this classroom"):
// the same strip the cabin tutorials use for "Find the light switch" / "Step through the portal".
// The cabins (tutorial and combat) have their own step-by-step instructions, so they are not listed here.
// Levels after that get an abstract objective: it says what you are here for, not how to do it.
//
// You do NOT put this in a scene: it creates itself when a scene loads and looks up that scene's objective below.
// A level can have more than one stage (Cad_lab): NextStage() moves on to the next text.
public class ObjectiveHUD : MonoBehaviour
{
    // scene name -> the objective for each stage of that level. Edit the texts here.
    static readonly Dictionary<string, string[]> Objectives = new Dictionary<string, string[]>
    {
        { "Classroom",      new[] { "Find a way out of this classroom" } },
        { "Cad_lab",        new[] { "Earn your passage", "Silence the lab" } },
        { "Classroom_boss", new[] { "Survive your final lecture" } },
    };

    static ObjectiveHUD instance;
    string[] stages;
    int stage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    // make one for every scene that has an objective written above
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (instance != null) { Destroy(instance.gameObject); instance = null; }
        if (!Objectives.TryGetValue(scene.name, out string[] texts)) return;
        var go = new GameObject("ObjectiveHUD");
        instance = go.AddComponent<ObjectiveHUD>();
        instance.stages = texts;
    }

    // Move the objective on (e.g. when you pass through the Lab portal)
    public static void NextStage()
    {
        if (instance != null && instance.stage < instance.stages.Length - 1) instance.stage++;
    }

    void OnDestroy() { if (instance == this) instance = null; }

    void OnGUI()
    {
        if (stages == null || stages.Length == 0) return;
        TutorialUI.ObjectiveStrip(stages[stage]);
    }
}
