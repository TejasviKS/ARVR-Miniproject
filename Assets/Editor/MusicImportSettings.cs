using UnityEditor;
using UnityEngine;

// VERSION 2
// Every audio file in Assets/Resources/Music is imported the same way:
//   - STREAMING: the file stays compressed and is read from disk while it plays. Some of the tracks are very long (the Claude fight track is
//     nearly 14 minutes, about 160 MB once decoded), and decoding one when a level starts froze the game for over a second. Streaming has no load time.
//   - kept stereo, not normalised by Unity (the MusicDirector evens the loudness out itself, using the numbers in Music/loudness.txt).
// A streamed clip cannot be read sample by sample, so the loudness is measured once in the editor: Tools > Music > Measure Loudness
// (it briefly imports the files the other way, measures them and writes Music/loudness.txt). Run it again if you replace a music file.
public class MusicImportSettings : AssetPostprocessor
{
    public static bool Measuring;                       // true only while the measuring tool runs: import as Decompress On Load so the samples are readable

    void OnPreprocessAudio()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Music/")) return;
        var importer = (AudioImporter)assetImporter;
        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = Measuring ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.Streaming;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.8f;
        s.preloadAudioData = Measuring;
        importer.defaultSampleSettings = s;
        importer.forceToMono = false;
        importer.loadInBackground = false;
    }
}
