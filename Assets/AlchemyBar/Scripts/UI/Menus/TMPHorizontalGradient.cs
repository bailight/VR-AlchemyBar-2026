using TMPro;
using UnityEngine;

namespace AlchemyBar.UI.Menus
{
    [ExecuteAlways]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TMPHorizontalGradient : MonoBehaviour
    {
        [SerializeField] private Gradient gradient = new Gradient();
        [Tooltip("Gradient start/end in normalized text-box width (0 = left edge, 1 = right edge).")]
        [SerializeField] private float from;
        [SerializeField] private float to = 1f;

        private TMP_Text text;

        public void Configure(Gradient value, float start, float end)
        {
            gradient = value;
            from = start;
            to = end;
            Refresh();
        }

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            Refresh();
        }

        private void OnDisable()
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            if (text != null) text.ForceMeshUpdate();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled) Refresh();
        }

        private void Refresh()
        {
            if (text == null) text = GetComponent<TMP_Text>();
            if (text == null) return;
            text.ForceMeshUpdate();
            Apply();
        }

        private void OnTextChanged(Object changed)
        {
            if (changed == text) Apply();
        }

        private void Apply()
        {
            TMP_TextInfo info = text.textInfo;
            if (info == null || info.characterCount == 0) return;

            Rect rect = text.rectTransform.rect;
            float width = Mathf.Max(0.001f, rect.width);
            float alpha = text.color.a;

            for (int i = 0; i < info.characterCount; i++)
            {
                TMP_CharacterInfo c = info.characterInfo[i];
                if (!c.isVisible) continue;

                TMP_MeshInfo mesh = info.meshInfo[c.materialReferenceIndex];
                for (int v = 0; v < 4; v++)
                {
                    int index = c.vertexIndex + v;
                    float u = (mesh.vertices[index].x - rect.xMin) / width;
                    Color color = gradient.Evaluate(Mathf.InverseLerp(from, to, u));
                    color.a *= alpha;
                    mesh.colors32[index] = color;
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
    }
}
