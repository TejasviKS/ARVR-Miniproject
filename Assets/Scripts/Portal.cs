using UnityEngine;
using UnityEngine.SceneManagement;

// VERSION 8
// Walk into this to go to the next room.
// Put it on a Cylinder (or any 3D object). CodePanel hides it until the code is solved.
[RequireComponent(typeof(Collider))]
public class Portal : MonoBehaviour
{
    [Tooltip("Exact name of the scene to load (it must be in File > Build Profiles > Scene List)")]
    public string sceneToLoad = "Level3_CADLab";
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
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.Self);   // slow swirl
    }

    void OnTriggerEnter(Collider other)
    {
        if (used || other.GetComponentInParent<PlayerController>() == null) return;

        if (Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            used = true;
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            Debug.LogError("[Portal] Can't load scene \"" + sceneToLoad + "\". Check the name and add it to File > Build Profiles > Scene List.", this);
            PlayerInteractor pi = other.GetComponentInParent<PlayerInteractor>();
            if (pi != null) pi.ShowMessage("Portal can't find scene \"" + sceneToLoad + "\"", 3f);
        }
    }
}
