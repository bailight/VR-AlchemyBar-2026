using UnityEditor;
using UnityEngine;

namespace AlchemyBar.UI.Menus.EditorTools
{
    internal sealed class HexHearthAudioImport : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/AlchemyBar/Audio/")) return;
            var importer = (AudioImporter)assetImporter;
            bool shortSfx = assetPath.Contains("/Sfx/") && !assetPath.Contains("/StirLoop/");
            importer.loadInBackground = !shortSfx;
            importer.forceToMono = shortSfx;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = shortSfx ? AudioClipLoadType.DecompressOnLoad : AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = shortSfx ? AudioCompressionFormat.ADPCM : AudioCompressionFormat.Vorbis;
            settings.quality = 0.6f;
            importer.defaultSampleSettings = settings;
        }
    }
}
