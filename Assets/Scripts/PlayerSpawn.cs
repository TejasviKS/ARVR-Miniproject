using UnityEngine;
using UnityEngine.Events;

// VERSION 1
// Remembers where the player started in this scene. Put it on an empty object ("PlayerSpawn").
// While a scene has one, pressing R puts the player back at that spot (and facing the same way): the way out of
// being stuck in a corner. Nothing else changes: puzzles, notes, lights and so on keep their state.
// Scenes without one (Classroom, Cad_lab) keep the old R: just stand up / straighten the view.
public class PlayerSpawn : MonoBehaviour
{
    public static PlayerSpawn Current { get; private set; }

    public UnityEvent onReset;

    Vector3 position;
    Quaternion rotation;
    PlayerController player;

    // "Enter Play Mode Options" is on (no domain reload): forget the old one from the previous Play
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ClearStatics() => Current = null;

    void Awake() { Current = this; }
    void OnDestroy() { if (Current == this) Current = null; }

    void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        if (player != null)
        {
            position = player.transform.position;
            rotation = player.transform.rotation;
        }
        else Debug.LogWarning("[PlayerSpawn] No player found in the scene.", this);
    }

    public void ResetPlayer()
    {
        if (player == null) return;
        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;                    // a CharacterController ignores position changes while it is on
        player.transform.SetPositionAndRotation(position, rotation);
        if (cc != null) cc.enabled = true;
        player.enabled = false;                                // off + on: PlayerController.OnEnable re-syncs the camera and facing
        player.enabled = true;
        onReset.Invoke();
    }
}
