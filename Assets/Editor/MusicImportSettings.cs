using UnityEditor;
using UnityEngine;

// VERSION 1
// Every audio file in Assets/Resources/Music is imported the same way, so the MusicDirector can measure its loudness and loop it cleanly:
// Decompress On Load (its samples must be readable), kept stereo, not normalised by Unity (the director evens the loudness out itself).
public class MusicImportSettings : AssetPostprocessor
{
    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.8f;
        s.preloadAudioData = true;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = false;
    }
}
