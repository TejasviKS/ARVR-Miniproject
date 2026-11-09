using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 2
// What happens when you step through a portal: nothing fancy, you are simply somewhere else.
// Portal.cs starts it; you don't put this in a scene yourself (it makes its own object that survives scene loads).
//
//   - walking is switched off for the moment it takes,
//   - the portal does its job: load the next scene (it was already loading in the background since the portal opened,
//     see Portal.TakePreload, so this only has to switch to it) or teleport + run the "On Used" events,
//   - walking is switched back on in the new place.
public class PortalTransition : MonoBehaviour
{
    static PortalTransition instance;

    // "Enter Play Mode Options" is on (no domain reload): forget the old object from the previous Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    public static bool Busy => instance != null && instance.busy;

    bool busy;

    // atCover runs first (events, teleport). If sceneToLoad is set, that scene is switched to right after it.
    public static void Play(Portal portal, PlayerController player, Action atCover, string sceneToLoad)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("PortalTransition");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PortalTransition>();
        }
        if (instance.busy) return;
        instance.StartCoroutine(instance.Run(portal, player, atCover, sceneToLoad));
    }

    IEnumerator Run(Portal portal, PlayerController player, Action atCover, string sceneToLoad)
    {
        busy = true;
        Hold(player, true);
        MusicDirector.Whoosh();                             // a short whoosh, and the music dips a little (it keeps playing)

        if (!string.IsNullOrEmpty(sceneToLoad))
        {
            atCover?.Invoke();
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> holdNewPlayer = (s, m) => Hold(FindAnyObjectByType<PlayerController>(), true);
            SceneManager.sceneLoaded += holdNewPlayer;                              // freeze the new player before its first frame
            AsyncOperation op = portal != null ? portal.TakePreload() : null;      // already loaded in the background?
            if (op == null) op = SceneManager.LoadSceneAsync(sceneToLoad);
            op.allowSceneActivation = true;                                        // switch to it now
            while (!op.isDone) yield return null;
            SceneManager.sceneLoaded -= holdNewPlayer;
            player = FindAnyObjectByType<PlayerController>();
        }
        else
        {
            atCover?.Invoke();                            // e.g. teleport, which switches walking back on
        }
        yield return null;                                // one frame so the new place settles

        Hold(player, false);
        busy = false;
    }

    // Stops/starts walking and "Press E" (the interactor would otherwise switch walking back on by itself)
    static void Hold(PlayerController pc, bool hold)
    {
        if (pc == null) return;
        PlayerInteractor pi = pc.GetComponent<PlayerInteractor>();
        if (pi != null) pi.enabled = !hold;
        pc.enabled = !hold;
    }
}
