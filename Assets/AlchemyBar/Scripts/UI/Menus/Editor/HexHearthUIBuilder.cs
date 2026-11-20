using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Object = UnityEngine.Object;

namespace AlchemyBar.UI.Menus.EditorTools
{
    public static class HexHearthUIBuilder
    {
        private const string ArtRoot = "Assets/AlchemyBar/Art/UI/HexHearth";
        private const string SpritesDir = ArtRoot + "/Sprites";
        private const string FontsDir = ArtRoot + "/Fonts";
        private const string LayoutPath = ArtRoot + "/hexhearth_layout.json";
        private const string PrefabDir = "Assets/AlchemyBar/Prefabs/UI";
        private const string RigPath = PrefabDir + "/HexHearth_MenuRig.prefab";

        private const float WorldScale = 0.72f / 1080f;

        private static Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        private static readonly Dictionary<string, string> FontFiles = new Dictionary<string, string>
        {
            { "Cormorant Garamond|Regular", "CormorantGaramond-Regular.ttf" },
            { "Cormorant Garamond|Medium", "CormorantGaramond-Medium.ttf" },
            { "Cormorant Garamond|SemiBold", "CormorantGaramond-SemiBold.ttf" },
            { "Inter|Medium", "Inter-Medium.ttf" },
        };

        #region Layout data (matches hexhearth_layout.json)

        [Serializable] public sealed class LayoutFile { public float scale; public ScreenData[] screens; }

        [Serializable]
        public sealed class ScreenData
        {
            public string name, key, backdrop;
            public float width, height;
            public NodeData[] nodes;
        }

        [Serializable] public sealed class StopData { public float t, r, g, b, a; }

        [Serializable]
        public sealed class NodeData
        {
            public int id, parent;
            public string name, kind;
            public float x, y, w, h, opacity;
            public string sprite;
            public bool hasColor;
            public float[] color;
            public string visualSprite;
            public float visualX, visualY, visualW, visualH;
            public bool visualHasColor;
            public float[] visualColor;
            public string glowSprite;
            public float glowX, glowY, glowW, glowH;
            public string chars, family, style, align;
            public float fontSize, spacing, baseline;
            public int lines;
            public bool autoWidth, hasGradient;
            public float[] textColor;
            public float gradFrom, gradTo;
            public StopData[] gradStops;
        }

        private sealed class BuiltScreen
        {
            public ScreenData Data;
            public GameObject Root;
            public readonly Dictionary<int, GameObject> Objects = new Dictionary<int, GameObject>();

            public NodeData Node(string name, int under = -1) =>
                Data.nodes.FirstOrDefault(n => n.name == name && (under < 0 || IsDescendant(n, under)));

            public IEnumerable<NodeData> Children(int id) => Data.nodes.Where(n => n.parent == id);

            public GameObject Go(string name, int under = -1)
            {
                NodeData n = Node(name, under);
                if (n == null) throw new InvalidOperationException($"Node '{name}' not found in {Data.name}");
                return Objects[n.id];
            }

            public bool IsDescendant(NodeData n, int ancestor)
            {
                for (int p = n.parent; p >= 0; p = Data.nodes[p].parent)
                    if (p == ancestor) return true;
                return false;
            }
        }

        #endregion

        [MenuItem("AlchemyBar/UI/Build Hex & Hearth Menus")]
        public static void Build()
        {
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>(LayoutPath);
            if (json == null)
            {
                Debug.LogError($"[HexHearth] Layout not found at {LayoutPath}");
                return;
            }

            LayoutFile layout = JsonUtility.FromJson<LayoutFile>(json.text);
            if (layout.screens.Length > 0) ReferenceResolution = new Vector2(layout.screens[0].width, layout.screens[0].height);
            ConfigureSprites(layout.scale);
            Dictionary<string, TMP_FontAsset> fonts = BuildFonts(layout);

            Directory.CreateDirectory(PrefabDir);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (ScreenData screen in layout.screens)
            {
                BuiltScreen built = BuildScreen(screen, fonts);
                switch (screen.key)
                {
                    case "Mainmenu": WireMainMenu(built); break;
                    case "Settings": WireSettings(built); break;
                    case "Pause": WirePause(built); break;
                }

                string path = $"{PrefabDir}/HexHearth_{screen.key}.prefab";
                prefabs[screen.key] = PrefabUtility.SaveAsPrefabAsset(built.Root, path);
                Object.DestroyImmediate(built.Root);
            }

            BuildRig(prefabs);
            AssetDatabase.SaveAssets();
            HexHearthAutoRebuild.MarkBuilt();
            Debug.Log($"[HexHearth] Built {prefabs.Count} menu prefabs and {RigPath}");
        }

        [MenuItem("AlchemyBar/UI/Add Hex & Hearth Menu Rig To Scene")]
        public static void AddRigToScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            if (prefab == null)
            {
                Build();
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
            }

            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.position = new Vector3(0f, 1.45f, 1.1f);
            Undo.RegisterCreatedObjectUndo(rig, "Add Hex & Hearth Menu Rig");
            Selection.activeGameObject = rig;

            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                Debug.LogWarning("[HexHearth] No EventSystem in the scene. Add one with an XR UI Input Module so rays can click the menus.");
        }

        #region Assets

        private static void ConfigureSprites(float scale)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { SpritesDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                if (ti.textureType == TextureImporterType.Sprite && ti.userData == "hexhearth") continue;

                ti.textureType = TextureImporterType.Sprite;
                ti.spriteImportMode = SpriteImportMode.Single;
                ti.spritePixelsPerUnit = 100f * scale;
                ti.mipmapEnabled = true;
                ti.alphaIsTransparency = true;
                ti.sRGBTexture = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = 2048;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;

                var settings = new TextureImporterSettings();
                ti.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spriteAlignment = (int)SpriteAlignment.Center;
                ti.SetTextureSettings(settings);

                ti.userData = "hexhearth";
                ti.SaveAndReimport();
            }
        }

        private static Sprite LoadSprite(string file)
        {
            if (string.IsNullOrEmpty(file)) return null;
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesDir}/{file}");
            if (sprite == null) Debug.LogWarning($"[HexHearth] Missing sprite {file}");
            return sprite;
        }

        private static Dictionary<string, TMP_FontAsset> BuildFonts(LayoutFile layout)
        {
            var chars = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) chars.Append(c);
            for (char c = 'А'; c <= 'я'; c++) chars.Append(c);
            chars.Append("Ёё·…«»—–’“”é");
            foreach (ScreenData s in layout.screens)
            foreach (NodeData n in s.nodes)
                if (n.kind == "text") chars.Append(n.chars);
            foreach (string lang in GameSettings.Languages) chars.Append(lang);
            string glyphs = new string(chars.ToString().Distinct().ToArray());

            var result = new Dictionary<string, TMP_FontAsset>();
            foreach (KeyValuePair<string, string> pair in FontFiles)
            {
                string fontPath = $"{FontsDir}/{pair.Value}";
                string assetPath = $"{FontsDir}/{Path.GetFileNameWithoutExtension(pair.Value)} SDF.asset";

                var asset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
                if (asset == null)
                {
                    var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
                    if (font == null)
                    {
                        Debug.LogError($"[HexHearth] Font file missing: {fontPath}");
                        continue;
                    }

                    asset = TMP_FontAsset.CreateFontAsset(font, 72, 7, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                    asset.name = Path.GetFileNameWithoutExtension(assetPath);
                    asset.TryAddCharacters(glyphs, out string missing);
                    if (!string.IsNullOrEmpty(missing)) Debug.Log($"[HexHearth] {asset.name}: no glyphs for '{missing}'");

                    AssetDatabase.CreateAsset(asset, assetPath);
                    for (int i = 0; i < asset.atlasTextures.Length; i++)
                    {
                        Texture2D tex = asset.atlasTextures[i];
                        if (tex == null || AssetDatabase.Contains(tex)) continue;
                        tex.name = $"{asset.name} Atlas {i}";
                        AssetDatabase.AddObjectToAsset(tex, asset);
                    }
                    asset.material.name = asset.name + " Material";
                    AssetDatabase.AddObjectToAsset(asset.material, asset);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssets();
                }

                result[pair.Key] = asset;
            }
            return result;
        }

        #endregion

        #region Screen construction

        private static BuiltScreen BuildScreen(ScreenData screen, Dictionary<string, TMP_FontAsset> fonts)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            var built = new BuiltScreen { Data = screen };

            var root = new GameObject($"HexHearth_{screen.key}", typeof(RectTransform));
            var rootRt = (RectTransform)root.transform;
            rootRt.sizeDelta = new Vector2(screen.width, screen.height);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.localScale = Vector3.one * WorldScale;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 |
                                               AdditionalCanvasShaderChannels.Normal |
                                               AdditionalCanvasShaderChannels.Tangent;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.referencePixelsPerUnit = 100f;
            scaler.dynamicPixelsPerUnit = 2f;
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<RectMask2D>();
            built.Root = root;

            Image backdrop = AddImage(CreateRect("Backdrop", rootRt, 0, 0, screen.width, screen.height), LoadSprite(screen.backdrop), Color.white);
            backdrop.raycastTarget = true;

            foreach (NodeData n in screen.nodes)
            {
                Transform parent = n.parent < 0 ? rootRt : built.Objects[n.parent].transform;
                GameObject go;
                switch (n.kind)
                {
                    case "image":
                        go = CreateRect(n.name, parent, n.x, n.y, n.w, n.h);
                        AddImage(go, LoadSprite(n.sprite), n.hasColor ? ToColor(n.color) : Color.white);
                        break;

                    case "text":
                        if (!string.IsNullOrEmpty(n.glowSprite))
                            AddImage(CreateRect(n.name + " Glow", parent, n.x + n.glowX, n.y + n.glowY, n.glowW, n.glowH), LoadSprite(n.glowSprite), Color.white);
                        go = CreateRect(n.name, parent, n.x, n.y, n.w, n.h);
                        SetupText(go, n, fonts);
                        break;

                    default:
                        go = CreateRect(n.name, parent, n.x, n.y, n.w, n.h);
                        if (n.opacity < 0.999f) go.AddComponent<CanvasGroup>().alpha = n.opacity;
                        if (!string.IsNullOrEmpty(n.visualSprite))
                        {
                            GameObject visual = CreateRect("Visual", go.transform, n.visualX, n.visualY, n.visualW, n.visualH);
                            AddImage(visual, LoadSprite(n.visualSprite), n.visualHasColor ? ToColor(n.visualColor) : Color.white);
                        }
                        break;
                }

                SetLayerRecursive(go, uiLayer);
                built.Objects[n.id] = go;
            }

            SetLayerRecursive(root, uiLayer);
            return built;
        }

        private static GameObject CreateRect(string name, Transform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return go;
        }

        private static Image AddImage(GameObject go, Sprite sprite, Color color)
        {
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
            return image;
        }

        private static void SetupText(GameObject go, NodeData n, Dictionary<string, TMP_FontAsset> fonts)
        {
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (fonts.TryGetValue($"{n.family}|{n.style}", out TMP_FontAsset font)) tmp.font = font;
            else Debug.LogWarning($"[HexHearth] No font for {n.family} {n.style}");

            tmp.text = n.chars;
            tmp.fontSize = n.fontSize;
            tmp.characterSpacing = n.spacing;
            tmp.raycastTarget = false;
            tmp.richText = false;
            tmp.margin = Vector4.zero;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.textWrappingMode = n.lines > 1 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;

            string align = n.autoWidth ? "CENTER" : n.align;
            switch (align)
            {
                case "CENTER": tmp.alignment = TextAlignmentOptions.Top; break;
                case "RIGHT": tmp.alignment = TextAlignmentOptions.TopRight; break;
                case "JUSTIFIED": tmp.alignment = TextAlignmentOptions.TopJustified; break;
                default: tmp.alignment = TextAlignmentOptions.TopLeft; break;
            }

            if (tmp.font != null)
            {
                FaceInfo face = tmp.font.faceInfo;
                float ascent = face.ascentLine * (n.fontSize / face.pointSize) * face.scale;
                var rt = (RectTransform)go.transform;
                rt.anchoredPosition = new Vector2(n.x, -(n.y + n.baseline - ascent));
            }

            if (n.hasGradient && n.gradStops != null && n.gradStops.Length > 0)
            {
                tmp.color = Color.white;
                var gradient = new Gradient
                {
                    mode = GradientMode.Blend
                };
                StopData[] stops = n.gradStops.Take(8).ToArray();
                gradient.SetKeys(
                    stops.Select(s => new GradientColorKey(new Color(s.r, s.g, s.b), s.t)).ToArray(),
                    stops.Select(s => new GradientAlphaKey(s.a, s.t)).ToArray());
                go.AddComponent<TMPHorizontalGradient>().Configure(gradient, n.gradFrom, n.gradTo);
            }
            else
            {
                tmp.color = ToColor(n.textColor);
            }
        }

        #endregion

        #region Wiring

        private static void WireMainMenu(BuiltScreen s)
        {
            var view = s.Root.AddComponent<MainMenuView>();
            SetRefs(view,
                ("startButton", MakeButton(s.Go("Menu button · Start"))),
                ("settingsButton", MakeButton(s.Go("Menu button · Settings"))),
                ("exitButton", MakeButton(s.Go("Menu button · Exit"))));
        }

        private static void WireSettings(BuiltScreen s)
        {
            var view = s.Root.AddComponent<SettingsView>();

            NodeData musicRow = s.Node("Setting · Music");
            NodeData sfxRow = s.Node("Setting · SFX");
            NodeData[] musicSegs = SegmentNodes(s, musicRow.id);
            NodeData[] sfxSegs = SegmentNodes(s, sfxRow.id);
            Color unlit = ToColor(musicSegs[musicSegs.Length - 1].color);
            var lit = musicSegs.Select(n => ToColor(n.color)).Where(c => c != unlit).ToList();
            while (lit.Count < musicSegs.Length) lit.Add(Color.Lerp(lit[lit.Count - 1], Color.white, 0.35f));

            SegmentedVolumeBar music = MakeVolumeBar(s, musicRow.id, musicSegs, lit.ToArray(), unlit);
            SegmentedVolumeBar sfx = MakeVolumeBar(s, sfxRow.id, sfxSegs, lit.ToArray(), unlit);

            NodeData ambientRow = s.Node("Setting · Ambient");
            NodeData shakeRow = s.Node("Setting · Shake");
            NodeData onTrack = s.Children(ambientRow.id).First(n => n.name == "Frame" && !string.IsNullOrEmpty(n.visualSprite));
            NodeData offTrack = s.Children(shakeRow.id).First(n => n.name == "Frame" && !string.IsNullOrEmpty(n.visualSprite));
            NodeData onKnob = s.Children(onTrack.id).First();
            NodeData offKnob = s.Children(offTrack.id).First();

            MagicToggleSwitch ambient = MakeToggle(s, ambientRow, onTrack, onKnob, onTrack, onKnob, offTrack, offKnob, true);
            MagicToggleSwitch shake = MakeToggle(s, shakeRow, offTrack, offKnob, onTrack, onKnob, offTrack, offKnob, false);

            GameObject languageRow = s.Go("Setting · Language");
            Button languageButton = MakeButton(languageRow);
            SetValues(languageRow.GetComponent<MagicButtonFeedback>(), ("hoverScale", 1.01f), ("pressScale", 0.99f));

            var languageValue = s.Go("Lang value").GetComponent<TMP_Text>();
            languageValue.alignment = TextAlignmentOptions.TopRight;

            SetRefs(view,
                ("musicVolume", music),
                ("spellEffectsVolume", sfx),
                ("ambientWhispers", ambient),
                ("brewShake", shake),
                ("languageButton", languageButton),
                ("languageValue", languageValue),
                ("returnButton", MakeButton(s.Go("Back button"))),
                ("saveButton", MakeButton(s.Go("Save button"))));
        }

        private static void WirePause(BuiltScreen s)
        {
            var view = s.Root.AddComponent<PauseMenuView>();
            TMP_Text[] stats = s.Data.nodes.Where(n => n.name == "Stat value").Select(n => s.Objects[n.id].GetComponent<TMP_Text>()).ToArray();
            SetRefs(view,
                ("resumeButton", MakeButton(s.Go("Pause btn · Resume"))),
                ("settingsButton", MakeButton(s.Go("Pause btn · Settings"))),
                ("saveAndReturnButton", MakeButton(s.Go("Pause btn · Save Exit"))),
                ("abandonButton", MakeButton(s.Go("Pause btn · Abandon"))),
                ("potionsBrewed", stats.ElementAtOrDefault(0)),
                ("goldEarned", stats.ElementAtOrDefault(1)),
                ("sessionTime", stats.ElementAtOrDefault(2)));
        }

        private static void BuildRig(Dictionary<string, GameObject> prefabs)
        {
            var rig = new GameObject("HexHearth_MenuRig");
            var router = rig.AddComponent<HexHearthMenuRouter>();
            GameObject Spawn(string key)
            {
                if (!prefabs.TryGetValue(key, out GameObject prefab)) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rig.transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                return go;
            }
            SetRefs(router, ("mainMenu", Spawn("Mainmenu")), ("settings", Spawn("Settings")), ("pause", Spawn("Pause")));
            SetValues(router, ("referenceResolution", ReferenceResolution), ("worldUnitsPerPixel", WorldScale));
            PrefabUtility.SaveAsPrefabAsset(rig, RigPath);
            Object.DestroyImmediate(rig);
        }

        private static Button MakeButton(GameObject go)
        {
            if (!go.TryGetComponent(out Image hit)) hit = go.AddComponent<Image>();
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = hit;
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;

            var feedback = go.AddComponent<MagicButtonFeedback>();
            Transform visual = go.transform.Find("Visual");
            if (visual != null) feedback.SetVisual(visual.GetComponent<Graphic>());
            return button;
        }

        private static NodeData[] SegmentNodes(BuiltScreen s, int rowId) =>
            s.Data.nodes.Where(n => n.kind == "image" && n.name == "Rectangle" && n.hasColor && s.IsDescendant(n, rowId)).ToArray();

        private static SegmentedVolumeBar MakeVolumeBar(BuiltScreen s, int rowId, NodeData[] segs, Color[] lit, Color unlit)
        {
            NodeData holder = s.Children(rowId).First(n => s.IsDescendant(segs[0], n.id));
            GameObject go = s.Objects[holder.id];

            if (!go.TryGetComponent(out Image hit)) hit = go.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            hit.raycastPadding = new Vector4(-12f, -24f, -12f, -24f);

            Color unlitColor = unlit;
            int startLevel = segs.Count(n => ToColor(n.color) != unlitColor);
            var bar = go.AddComponent<SegmentedVolumeBar>();
            bar.Configure(segs.Select(n => s.Objects[n.id].GetComponent<Image>()).ToArray(), lit, unlit, startLevel);
            return bar;
        }

        private static MagicToggleSwitch MakeToggle(BuiltScreen s, NodeData row, NodeData track, NodeData knob,
                                                     NodeData onTrack, NodeData onKnob, NodeData offTrack, NodeData offKnob, bool isOn)
        {
            GameObject rowGo = s.Objects[row.id];
            if (!rowGo.TryGetComponent(out Image hit)) hit = rowGo.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;

            var toggle = rowGo.AddComponent<MagicToggleSwitch>();
            SetRefs(toggle,
                ("track", s.Objects[track.id].transform.Find("Visual").GetComponent<Image>()),
                ("knob", s.Objects[knob.id].transform),
                ("knobImage", s.Objects[knob.id].GetComponent<Image>()),
                ("trackOn", LoadSprite(onTrack.visualSprite)),
                ("trackOff", LoadSprite(offTrack.visualSprite)));
            SetValues(toggle,
                ("isOn", isOn),
                ("knobOn", ToColor(onKnob.color)), ("knobXOn", onKnob.x),
                ("knobOff", ToColor(offKnob.color)), ("knobXOff", offKnob.x));
            return toggle;
        }

        #endregion

        #region Helpers

        private static void SetRefs(Object target, params (string name, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach ((string name, Object value) in refs)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null) Debug.LogError($"[HexHearth] {target.GetType().Name}.{name} not found");
                else p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetValues(Object target, params (string name, object value)[] values)
        {
            var so = new SerializedObject(target);
            foreach ((string name, object value) in values)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null) { Debug.LogError($"[HexHearth] {target.GetType().Name}.{name} not found"); continue; }
                switch (value)
                {
                    case bool b: p.boolValue = b; break;
                    case float f: p.floatValue = f; break;
                    case int i: p.intValue = i; break;
                    case Color c: p.colorValue = c; break;
                    case Vector2 v: p.vector2Value = v; break;
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Color ToColor(float[] c) =>
            c == null || c.Length < 4 ? Color.white : new Color(c[0], c[1], c[2], c[3]);

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0) return;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        #endregion
    }
}
