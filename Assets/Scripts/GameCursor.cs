using System.Collections.Generic;
using UnityEngine;

// VERSION 1
// The ONE place that decides whether the mouse is captured (locked in the middle of the game and invisible) or free (a visible arrow).
// You do not put this in a scene: it starts by itself with the game.
//
//   - Normally the mouse is LOCKED and hidden, and it grabs itself again whenever the game window has focus. No clicking to get it back.
//   - Anything that needs the arrow (the Lab Asst.'s answer buttons, the defeat / "mission passed" screens, the pause menu) says so with
//     GameCursor.Request(this) and gives it back with GameCursor.Release(this). While at least one request is open the mouse is free.
//   - In the Unity editor the mouse is only captured while the Game view is focused, so the other editor windows stay usable.
public static class GameCursor
{
    static readonly HashSet<object> requests = new HashSet<object>();

    public static void Request(object owner) { if (owner != null) requests.Add(owner); }
    public static void Release(object owner) { if (owner != null) requests.Remove(owner); }

    static bool AnyRequest()
    {
        requests.RemoveWhere(o => o is Object u && u == null);           // (an owner that was destroyed without releasing)
        return requests.Count > 0;
    }

    // "Enter Play Mode Options" is on (no domain reload): start every Play with no requests
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => requests.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Start()
    {
        GameObject go = new GameObject("GameCursor");
        Object.DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.AddComponent<Runner>();
    }

    static bool GameHasFocus()
    {
#if UNITY_EDITOR
        UnityEditor.EditorWindow w = UnityEditor.EditorWindow.focusedWindow;
        return Application.isFocused && w != null && w.GetType().Name == "GameView";
#else
        return Application.isFocused;
#endif
    }

    static void Apply()
    {
        bool free = PauseMenu.IsPaused || AnyRequest();
        if (free)
        {
            if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
            if (!Cursor.visible) Cursor.visible = true;
        }
        else if (GameHasFocus())
        {
            if (Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            if (Cursor.visible) Cursor.visible = false;
        }
    }

    class Runner : MonoBehaviour
    {
        void Update() => Apply();
        void OnApplicationFocus(bool focused) { if (focused) Apply(); }
    }
}
