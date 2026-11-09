using UnityEditor;
using UnityEngine;

// The opening screens crop the logos to circles in code, so the pictures in Assets/Resources/Logos must be readable and uncompressed.
public class LogoImportSettings : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').Contains("/Resources/Logos/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.isReadable = true;
        ti.mipmapEnabled = false;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.maxTextureSize = 1024;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.wrapMode = TextureWrapMode.Clamp;
    }
}
