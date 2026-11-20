using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AlchemyBar.UI.Menus
{
    public sealed class MenuLocalizer : MonoBehaviour
    {
        private sealed class Entry
        {
            public TMP_Text Text;
            public string Key;
            public Vector2 BasePos;
            public Vector2 BaseSize;
            public float BaseWidth;
            public bool CentredGroup;
            public bool IsGroup;
            public RectTransform Glow;
            public Vector2 GlowPos, GlowSize;
            public readonly List<(RectTransform rt, Vector2 basePos, int side)> Siblings = new List<(RectTransform, Vector2, int)>();
        }

        private readonly List<Entry> entries = new List<Entry>();
        private bool initialised;
        private int applied = -1;

        private void Awake() => Init();

        private void OnEnable()
        {
            GameSettings.EnsureLoaded();
            GameSettings.LanguageChanged += Apply;
            Apply(GameSettings.DisplayLanguageIndex);
        }

        private void OnDisable() => GameSettings.LanguageChanged -= Apply;

        private void Init()
        {
            if (initialised) return;
            initialised = true;

            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                string key = text.text;
                if (!MenuLocalization.Has(key)) continue;

                var rt = text.rectTransform;
                var entry = new Entry
                {
                    Text = text,
                    Key = key,
                    BasePos = rt.anchoredPosition,
                    BaseSize = rt.sizeDelta,
                    BaseWidth = text.GetPreferredValues(key).x,
                };

                Transform glow = rt.parent != null ? rt.parent.Find(text.name + " Glow") : null;
                if (glow != null)
                {
                    entry.Glow = (RectTransform)glow;
                    entry.GlowPos = entry.Glow.anchoredPosition;
                    entry.GlowSize = entry.Glow.sizeDelta;
                }

                int others = rt.parent != null ? rt.parent.childCount - 1 - (glow != null ? 1 : 0) : 0;
                if (rt.parent is RectTransform group && others >= 1 && others <= 2)
                {
                    bool allIcons = true;
                    foreach (Transform child in group)
                        if (child != rt && child != glow && child.GetComponent<Image>() == null) allIcons = false;

                    if (allIcons)
                    {
                        entry.IsGroup = true;
                        float textCentre = rt.anchoredPosition.x + rt.sizeDelta.x * 0.5f;
                        foreach (Transform child in group)
                        {
                            if (child == rt || child == glow) continue;
                            var c = (RectTransform)child;
                            float centre = c.anchoredPosition.x + c.sizeDelta.x * 0.5f;
                            entry.Siblings.Add((c, c.anchoredPosition, centre < textCentre ? -1 : 1));
                        }

                        if (group.parent is RectTransform outer)
                        {
                            float left = group.anchoredPosition.x;
                            float right = outer.rect.width - left - group.rect.width;
                            entry.CentredGroup = Mathf.Abs(left - right) <= 4f;
                        }
                    }
                }

                entries.Add(entry);
            }
        }

        private void Apply(int language)
        {
            Init();
            if (applied == language) return;
            applied = language;

            foreach (Entry e in entries)
            {
                string value = MenuLocalization.Get(e.Key, language);
                e.Text.text = value;

                float delta = e.Text.GetPreferredValues(value).x - e.BaseWidth;
                if (language == MenuLocalization.English) delta = 0f;

                var rt = e.Text.rectTransform;
                rt.sizeDelta = new Vector2(e.BaseSize.x + delta, e.BaseSize.y);

                bool keepLeftEdge = e.IsGroup && !e.CentredGroup;
                Vector2 textShift = keepLeftEdge ? Vector2.zero : new Vector2(-delta * 0.5f, 0f);
                rt.anchoredPosition = e.BasePos + textShift;
                if (e.Glow != null)
                {
                    e.Glow.anchoredPosition = e.GlowPos + textShift;
                    e.Glow.sizeDelta = new Vector2(e.GlowSize.x + delta, e.GlowSize.y);
                }

                foreach ((RectTransform sib, Vector2 basePos, int side) in e.Siblings)
                {
                    float shift = keepLeftEdge ? (side > 0 ? delta : 0f) : side * delta * 0.5f;
                    sib.anchoredPosition = basePos + new Vector2(shift, 0f);
                }
            }
        }
    }
}
