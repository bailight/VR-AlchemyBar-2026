using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace AlchemyBar.UI.Menus.EditorTools
{
    [InitializeOnLoad]
    internal sealed class HexHearthAutoRebuild : AssetPostprocessor
    {
        private const string LayoutPath = "Assets/AlchemyBar/Art/UI/HexHearth/hexhearth_layout.json";
        private const string StampPath = "Library/HexHearthUI.stamp";

        private static bool scheduled;

        static HexHearthAutoRebuild()
        {
            EditorApplication.delayCall += CheckStamp;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode) CheckStamp();
            };
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (Array.IndexOf(imported, LayoutPath) >= 0) EditorApplication.delayCall += CheckStamp;
        }

        private static void CheckStamp()
        {
            if (scheduled || !File.Exists(LayoutPath)) return;
            string hash = Hash(LayoutPath);
            string stamp = File.Exists(StampPath) ? File.ReadAllText(StampPath).Trim() : string.Empty;
            if (hash == stamp) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            scheduled = true;
            EditorApplication.delayCall += () =>
            {
                try
                {
                    Debug.Log("[HexHearth] Layout changed — rebuilding menu prefabs.");
                    HexHearthUIBuilder.Build();
                    File.WriteAllText(StampPath, hash);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
                finally
                {
                    scheduled = false;
                }
            };
        }

        internal static void MarkBuilt()
        {
            if (File.Exists(LayoutPath)) File.WriteAllText(StampPath, Hash(LayoutPath));
        }

        private static string Hash(string path)
        {
            using (var md5 = MD5.Create())
                return BitConverter.ToString(md5.ComputeHash(File.ReadAllBytes(path)));
        }
    }
}
