using UnityEngine;

// VERSION 1
// Something small you can pick up with E (a letter, a key card ...). Put it on an object with a collider.
// When picked up it disappears, a short message appears and the item id is saved in GameProgress.AddItem(),
// so later scenes can ask GameProgress.HasItem("letter_of_recommendation").
// Items are forgotten when a new run starts (Play Again, or pressing Play in the editor).
public class PickupItem : Interactable
{
    [Tooltip("Short name used in the on-screen message")]
    public string itemName = "Letter of Recommendation";
    [Tooltip("Saved in GameProgress. Other scripts ask for this id.")]
    public string itemId = "letter_of_recommendation";

    void Awake()
    {
        if (promptText == "interact") promptText = "pick up " + itemName;
        if (GameProgress.HasItem(itemId)) gameObject.SetActive(false);      // already taken (e.g. the scene was reloaded)
    }

    public override void Interact(PlayerInteractor player)
    {
        GameProgress.AddItem(itemId);
        player.ShowMessage("Picked up: " + itemName, 3f);
        Chime(transform.position);
        gameObject.SetActive(false);
    }

    // a small rising "ding", made in code
    static void Chime(Vector3 at)
    {
        const int rate = 44100; const float seconds = 0.35f;
        int n = Mathf.CeilToInt(rate * seconds); float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / rate;
            float f = t < 0.12f ? 880f : 1320f;
            data[i] = Mathf.Sin(2f * Mathf.PI * f * t) * 0.3f * Mathf.Exp(-t * 7f);
        }
        AudioClip clip = AudioClip.Create("pickup", n, 1, rate, false);
        clip.SetData(data, 0);
        AudioSource.PlayClipAtPoint(clip, at, 1f);
    }
}
