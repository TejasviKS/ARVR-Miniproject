using System.Collections;
using UnityEngine;
using UnityEngine.Events;

// VERSION 1
// The one working rocker on the cabin's switch panel. Put it on the rocker object (with a collider).
// Press E: the rocker flips, the room lights flicker out (RoomLights), then "On Switched Off" fires
// (the tutorial uses it to open the portal). It works only once.
public class LightSwitch : Interactable
{
    public RoomLights lights;
    [Tooltip("Which way the panel faces (out of the wall, into the room). Needed to flip the rocker the right way.")]
    public Vector3 frontDirection = Vector3.forward;
    [Tooltip("How far the rocker tilts when pressed (degrees)")]
    public float flipAngle = 20f;
    public UnityEvent onSwitchedOff;

    bool used;
    AudioSource sfx;
    AudioClip click;

    void Awake()
    {
        promptText = "switch off the lights";
        sfx = gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;
        sfx.spatialBlend = 1f;                    // 3D: the click comes from the panel
        sfx.rolloffMode = AudioRolloffMode.Linear;
        sfx.maxDistance = 12f;
        click = MakeClick();
    }

    public override bool CanInteract => !used;
    public override string GetPrompt() => Prompt("switch off the lights");

    public override void Interact(PlayerInteractor player)
    {
        if (used) return;
        used = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        sfx.PlayOneShot(click, 1f);

        // flip the rocker around the horizontal line along the panel
        Vector3 axis = Vector3.Cross(Vector3.up, frontDirection.normalized);
        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.AngleAxis(-flipAngle, axis) * from;
        const float flipTime = 0.12f;
        for (float t = 0f; t < flipTime; t += Time.deltaTime)
        {
            transform.rotation = Quaternion.Slerp(from, to, t / flipTime);
            yield return null;
        }
        transform.rotation = to;

        yield return new WaitForSeconds(0.25f);
        if (lights != null) yield return StartCoroutine(lights.SwitchOff());
        yield return new WaitForSeconds(0.6f);       // a beat of darkness before something happens
        onSwitchedOff.Invoke();
    }

    static AudioClip MakeClick()
    {
        const int rate = 44100;
        const float seconds = 0.08f;
        int n = Mathf.CeilToInt(rate * seconds);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            data[i] = (Mathf.Sin(2f * Mathf.PI * 2400f * t) * 0.5f + (Random.value * 2f - 1f) * 0.3f) * Mathf.Exp(-t * 90f);
        }
        AudioClip clip = AudioClip.Create("click", n, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
