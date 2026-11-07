using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// VERSION 1
// A cupboard you open with E. Put it on the cupboard BODY (with a box collider); the two door panels must be
// children of the body. The doors swing open outwards on their outer edges, and the note hidden inside appears.
public class OpenableCupboard : Interactable
{
    [Tooltip("The two door panels (children of this object). They swing around their outer vertical edge.")]
    public Transform[] doors;
    [Tooltip("Which way the cupboard faces (out of the cupboard, into the room)")]
    public Vector3 frontDirection = Vector3.forward;
    [Tooltip("Hidden until the cupboard is open (e.g. a note)")]
    public GameObject revealOnOpen;
    public float openAngle = 105f;
    public float openTime = 0.9f;
    public UnityEvent onOpened;
    [Tooltip("Runs when you press E on the cupboard AFTER it is open (the tutorial shows the note again)")]
    public UnityEvent onReadAgain;

    [Tooltip("While true, pressing E does nothing (the tutorial unlocks it when it asks you to open it)")]
    public bool locked;

    bool opened, opening;
    AudioSource sfx;
    AudioClip creak;

    void Awake()
    {
        promptText = "open the cupboard";
        if (revealOnOpen != null) revealOnOpen.SetActive(false);
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 1f;
        sfx.rolloffMode = AudioRolloffMode.Linear;
        sfx.maxDistance = 12f;
        creak = MakeCreak();
    }

    public override bool CanInteract => !opening && !locked;
    public override string GetPrompt() => opened ? "Press E to read the note" : "Press E to open the cupboard";

    public override void Interact(PlayerInteractor player)
    {
        if (opening || locked) return;
        if (opened) { onReadAgain.Invoke(); return; }       // already open: look at the note again
        StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        opening = true;
        sfx.PlayOneShot(creak, 0.9f);

        Renderer bodyRenderer = GetComponent<Renderer>();
        Vector3 bodyCentre = bodyRenderer != null ? bodyRenderer.bounds.center : transform.position;
        Vector3 front = frontDirection.normalized;
        Vector3 widthDir = Vector3.Cross(Vector3.up, front).normalized;

        // for every door: hinge on its outer edge, and which way round the free edge swings outwards
        var hinge = new Vector3[doors.Length];
        var dir = new float[doors.Length];
        for (int i = 0; i < doors.Length; i++)
        {
            Bounds b = doors[i].GetComponent<Renderer>().bounds;
            float side = Mathf.Sign(Vector3.Dot(b.center - bodyCentre, widthDir));
            float halfW = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(widthDir.x), Mathf.Abs(widthDir.y), Mathf.Abs(widthDir.z)));
            hinge[i] = b.center + widthDir * side * halfW;
            Vector3 freeEdge = b.center - widthDir * side * halfW;
            Vector3 swung = hinge[i] + Quaternion.AngleAxis(90f, Vector3.up) * (freeEdge - hinge[i]);
            dir[i] = Vector3.Dot(swung - freeEdge, front) > 0f ? 1f : -1f;
        }

        float done = 0f;
        for (float t = 0f; t < openTime; t += Time.deltaTime)
        {
            float target = openAngle * Mathf.SmoothStep(0f, 1f, t / openTime);
            float step = target - done;
            for (int i = 0; i < doors.Length; i++) doors[i].RotateAround(hinge[i], Vector3.up, dir[i] * step);
            done = target;
            yield return null;
        }
        for (int i = 0; i < doors.Length; i++) doors[i].RotateAround(hinge[i], Vector3.up, dir[i] * (openAngle - done));

        if (revealOnOpen != null) revealOnOpen.SetActive(true);
        opened = true;
        opening = false;
        onOpened.Invoke();
    }

    static AudioClip MakeCreak()
    {
        const int rate = 44100;
        const float seconds = 0.8f;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = 180f + 120f * Mathf.Sin(t * 9f) + 60f * t;       // a wavering squeak
            phase += 2f * Mathf.PI * f / rate;
            data[i] = (Mathf.Sin(phase) * 0.25f + (Random.value * 2f - 1f) * 0.05f) * Mathf.Sin(Mathf.PI * t / seconds);
        }
        AudioClip clip = AudioClip.Create("creak", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
