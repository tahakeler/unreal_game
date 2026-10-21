using UnityEngine;
using UnityEngine.Rendering;

namespace HarvestPanic.Voice
{
    /// <summary>
    /// "Talking" marker above a remote farmer's name tag: a small speaker glyph whose waves follow loudness
    /// (<c>&lt;)</c>, <c>&lt;))</c>, <c>&lt;)))</c>). Parented under the Customization NameTag object when present (so it
    /// inherits the tag's billboard and distance hide without editing Customization), else above the head with its
    /// own billboard. Uses the built-in font material like the NameTag text, so it works in every build.
    /// </summary>
    public sealed class VoiceSpeakingIndicator : MonoBehaviour
    {
        public const string NameTagName = "NameTag";
        static readonly string[] Glyphs = { "<)", "<))", "<)))" };

        TextMesh _text, _shadow;
        bool _ownBillboard;
        bool _shown;
        int _level = -1;

        public bool Shown => _shown;

        /// <summary>Creates the marker for the player whose head is <paramref name="head"/>.</summary>
        public static VoiceSpeakingIndicator Attach(Transform head, VoiceConfig cfg)
        {
            if (head == null) return null;
            Transform root = head.root;
            Transform tag = PlayerHeadLocator.FindDeep(root, NameTagName);
            var go = new GameObject("VoiceSpeaking");
            var ind = go.AddComponent<VoiceSpeakingIndicator>();
            if (tag != null)
            {
                go.transform.SetParent(tag, false);
                go.transform.localPosition = new Vector3(0f, cfg.IndicatorAboveNameTag, 0f);
            }
            else
            {
                go.transform.SetParent(head, false);
                go.transform.localPosition = new Vector3(0f, cfg.IndicatorHeight, 0f);
                ind._ownBillboard = true;
            }
            go.transform.localRotation = Quaternion.identity;
            ind.Build();
            ind.Show(false, 0f);
            return ind;
        }

        void Build()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _shadow = Label("Shadow", new Vector3(-0.008f, -0.008f, 0.01f), new Color(0.1f, 0.06f, 0.03f), font);
            _text = Label("Glyph", Vector3.zero, new Color(1f, 0.85f, 0.25f), font);
        }

        TextMesh Label(string n, Vector3 pos, Color c, Font font)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.fontSize = 72;
            tm.characterSize = 0.014f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            tm.color = c;
            tm.text = Glyphs[2];
            var mr = go.GetComponent<MeshRenderer>();
            if (font != null) mr.sharedMaterial = font.material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return tm;
        }

        /// <summary>Shows/hides the marker and sets its wave count from loudness 0..1.</summary>
        public void Show(bool on, float loudness)
        {
            if (on != _shown)
            {
                _shown = on;
                if (_text != null) _text.gameObject.SetActive(on);
                if (_shadow != null) _shadow.gameObject.SetActive(on);
            }
            if (!on) return;
            int level = loudness < 0.3f ? 0 : loudness < 0.6f ? 1 : 2;
            if (level == _level) return;
            _level = level;
            if (_text != null) _text.text = Glyphs[level];
            if (_shadow != null) _shadow.text = Glyphs[level];
        }

        void LateUpdate()
        {
            if (!_ownBillboard || !_shown) return;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 toCam = transform.position - cam.transform.position;
            if (toCam.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(toCam, cam.transform.up);
        }
    }
}
