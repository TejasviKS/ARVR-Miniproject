using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// VERSION 1
// Tools > Music > Measure Loudness
// Works out the average loudness (RMS) of every file in Assets/Resources/Music and writes it to Assets/Resources/Music/loudness.txt
// (one "name=rms" line per file). The MusicDirector uses these numbers to make every track equally loud without having to decode
// the (streamed) files while the game runs. Run it again whenever a music file is added or replaced.
public static class MusicLoudnessTool
{
    const string Folder = "Assets/Resources/Music";

    [MenuItem("Tools/Music/Measure Loudness")]
    public static void Measure()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Folder });
        var paths = new List<string>();
        foreach (string g in guids) paths.Add(AssetDatabase.GUIDToAssetPath(g));
        var sb = new StringBuilder();
        try
        {
            MusicImportSettings.Measuring = true;                                         // import them readable
            foreach (string p in paths) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
            foreach (string p in paths)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p);
                float rms = RmsOf(clip);
                string name = Path.GetFileNameWithoutExtension(p);
                if (rms > 0f) sb.AppendLine(name + "=" + rms.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture));
                Debug.Log("[MusicLoudness] " + name + ": rms " + rms.ToString("0.00000"));
            }
        }
        finally
        {
            MusicImportSettings.Measuring = false;                                        // and back to streaming
            foreach (string p in paths) AssetDatabase.ImportAsset(p, ImportAssetOptions.ForceUpdate);
        }
        File.WriteAllText(Folder + "/loudness.txt", sb.ToString());
        AssetDatabase.ImportAsset(Folder + "/loudness.txt", ImportAssetOptions.ForceUpdate);
        AssetDatabase.Refresh();
        Debug.Log("[MusicLoudness] wrote " + Folder + "/loudness.txt");
    }

    // The same measure the director used to take at run time: every third sample of the whole clip
    static float RmsOf(AudioClip clip)
    {
        if (clip == null) return 0f;
        if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();
        int ch = clip.channels, window = 32768;
        if (clip.samples < window) return 0f;
        var buf = new float[window * ch];
        double sum = 0; long count = 0;
        for (int off = 0; off + window <= clip.samples; off += window)
        {
            if (!clip.GetData(buf, off)) return 0f;
            for (int i = 0; i < buf.Length; i += 3) { sum += buf[i] * buf[i]; count++; }
        }
        return count == 0 ? 0f : Mathf.Sqrt((float)(sum / count));
    }
}
