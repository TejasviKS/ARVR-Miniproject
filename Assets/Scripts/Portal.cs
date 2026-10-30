using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

// VERSION 10
// Walk into this to go to the next room.
// Put it on a Cube (or any 3D object). A puzzle hides it until it is solved.
// Two ways to use it:
//  - Scene portal (Classroom): fill in "Scene To Load" -> walking in loads that scene.
//  - Teleport portal (Cad_lab): drag an empty object into "Teleport To" -> walking in moves the
//    player there (e.g. through a blocked doorway). It disappears after one use, so it's one-way.
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (it must be in File > Build Profiles > Scene List). Ignored if Teleport To is set.")]
    public string sceneToLoad = "Level3_CADLab";
    [Tooltip("If set: teleport the player to this point instead of loading a scene (the player faces its blue arrow)")]
    public Transform teleportTo;
    [Tooltip("Runs when the player goes through (e.g. start the beeping computers)")]
    public UnityEvent onUsed;
    public float spinSpeed = 90f;

    bool used;

    void Awake()
    {
        // Walk-through trigger. A kinematic Rigidbody makes sure the player is detected.
        GetComponent<Collider>().isTrigger = true;
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        Debug.Log("[Scripts] Portal v10 running on \"" + name + "\" (" + (teleportTo != null ? "teleport to " + teleportTo.name : "scene: " + sceneToLoad) + ")", this);
        if (teleportTo == null && string.IsNullOrEmpty(sceneToLoad))
            Debug.LogError("[Portal] \"" + name + "\" has nothing to do: drag an object into Teleport To, or type a scene name in Scene To Load.", this);
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);   // slow swirl
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (used || pc == null) return;

        if (teleportTo != null)
        {
            used = true;
            Teleport(pc);
            onUsed.Invoke();
            gameObject.SetActive(false);      // one-way: the portal is gone after you use it
            return;
        }

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            PlayerInteractor pi = other.GetComponentInParent<PlayerInteractor>();
            if (pi != null) pi.ShowMessage("Portal is not set up: its Teleport To slot is empty", 3f);
            Debug.LogError("[Portal] \"" + name + "\": Teleport To is empty (and no scene name). Drag the arrival point into Teleport To.", this);
            return;
        }

        if (Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            used = true;
            onUsed.Invoke();
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("[Portal] Can't load scene \"" + sceneToLoad + "\". Check the name and add it to File > Build Profiles > Scene List.", this);
            PlayerInteractor pi = other.GetComponentInParent<PlayerInteractor>();
            if (pi != null) pi.ShowMessage("Portal can't find scene \"" + sceneToLoad + "\"", 3f);
        }
    }

    void Teleport(PlayerController pc)
    {
        // A CharacterController ignores position changes while it's on, so switch it off for a moment
        CharacterController cc = pc.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        pc.transform.SetPositionAndRotation(teleportTo.position, Quaternion.Euler(0f, teleportTo.eulerAngles.y, 0f));
        if (cc != null) cc.enabled = true;

        // Off + on again -> PlayerController.OnEnable re-syncs the camera to the new facing
        pc.enabled = false;
        pc.enabled = true;

        PlaySwoosh(teleportTo.position);
    }

    // Short "whoosh" made in code, played where the player arrives
    void PlaySwoosh(Vector3 at)
    {
        const int rate = 44100;
        const float seconds = 0.7f;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        float smooth = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float k = Mathf.Lerp(0.05f, 0.5f, t / seconds);             // noise gets brighter = rising whoosh
            smooth += (Random.Range(-1f, 1f) - smooth) * k;
            data[i] = smooth * 0.8f * Mathf.Sin(Mathf.PI * t / seconds); // fade in and out
        }
        AudioClip clip = AudioClip.Create("swoosh", n, 1, rate, false);
        clip.SetData(data, 0);
        AudioSource.PlayClipAtPoint(clip, at, 0.9f);
    }
}
