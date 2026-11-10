using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 1
// Leaving a level for the main menu (from the pause menu or the "Mission Passed" screen): instead of cutting the music and blanking the screen,
// the screen fades to black and the current music fades out together, over FadeSeconds, and only then does the menu load (it fades in from
// black by itself). It makes its own object, which survives the scene change and removes itself once the menu is up.
public class MenuExit : MonoBehaviour
{
    public const float FadeSeconds = 1.25f;

    public static bool Active { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { Active = false; }

    // beforeLoad (optional) runs just before the menu loads, e.g. to un-pause the game
    public static void Go(Action beforeLoad = null)
    {
        if (Active) return;
        Active = true;
        var go = new GameObject("MenuExit");
        DontDestroyOnLoad(go);
        go.AddComponent<MenuExit>().beforeLoad = beforeLoad;
    }

    Action beforeLoad;
    float startedAt;
    bool loaded;
    int framesAfterLoad;

    void Start()
    {
        startedAt = Time.unscaledTime;
        MusicDirector.Silence(FadeSeconds);                 // the music in the level fades out as the screen goes dark
    }

    void OnDestroy() { Active = false; }

    void Update()
    {
        if (!loaded)
        {
            if (Time.unscaledTime - startedAt >= FadeSeconds)
            {
                loaded = true;
                beforeLoad?.Invoke();
                SceneManager.LoadScene("MainMenu");
            }
        }
        else if (++framesAfterLoad > 3) Destroy(gameObject);   // (the menu draws black on its first frames itself, then fades in)
    }

    void OnGUI()
    {
        GUI.depth = -10000;                                    // over every other screen
        float k = loaded ? 1f : Mathf.Clamp01((Time.unscaledTime - startedAt) / FadeSeconds);
        GUI.color = new Color(0f, 0f, 0f, k * k * (3f - 2f * k));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
