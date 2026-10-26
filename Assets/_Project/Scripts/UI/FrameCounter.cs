using UnityEngine;

namespace TheVeil.UI
{
    /// <summary>
    /// The frame rate, on screen, in the built game.
    ///
    /// <b>Because the one budget line no instrument here can reach is the one the whole
    /// budget exists for.</b> docs/technical-design.md §1 asks for sixty frames a second
    /// on a mid-range phone and thirty on a cheap one, and nothing in this project has
    /// ever measured either - there was no build to measure. An APK with no readout in it
    /// does not answer the question it was built to answer, so this goes in with the
    /// build.
    ///
    /// <b>Two numbers, not one.</b> An average frame rate hides the thing that is actually
    /// felt: sixty a second with one frame of a hundred and twenty milliseconds in it
    /// reads as 60 and plays as a stutter. So the longest frame of the last second is
    /// shown beside the count, which is what a hitch looks like when it is written down.
    /// The worst frame since the level began is kept too, because the one that matters is
    /// usually the one that happened while you were looking at the road and not at the
    /// corner of the screen.
    ///
    /// It stands itself up rather than being placed in a scene: three scenes would
    /// otherwise each need a copy, and the two that were forgotten would be the ones being
    /// profiled. Drawn with IMGUI, which costs a little - one label a frame - and is worth
    /// saying out loud in a readout whose whole job is to be honest about cost.
    ///
    /// Switched off by default in the editor, where the figure means nothing: the editor
    /// renders into a window beside a compiler and an asset database.
    /// </summary>
    public sealed class FrameCounter : MonoBehaviour
    {
        /// <summary>Whether the readout is drawn. Left on in a player build.</summary>
        public static bool Showing = !Application.isEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Stand()
        {
            var host = new GameObject("Frame Counter");
            host.AddComponent<FrameCounter>();
            DontDestroyOnLoad(host);
        }

        int _frames;
        float _since;
        float _longest;

        int _rate;
        float _worstSecond;
        float _worstEver;

        void Update()
        {
            float step = Time.unscaledDeltaTime;

            _frames++;
            _since += step;

            if (step > _longest) _longest = step;
            if (step > _worstEver) _worstEver = step;

            if (_since < 1f) return;

            // Per second of real time rather than a running average over frames: a mean
            // over the last N frames moves faster when the frames are faster, which is
            // exactly backwards for reading a drop.
            _rate = Mathf.RoundToInt(_frames / _since);
            _worstSecond = _longest;

            _frames = 0;
            _since = 0f;
            _longest = 0f;
        }

        void OnGUI()
        {
            if (!Showing) return;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.Max(14, Screen.height / 50),
                alignment = TextAnchor.UpperLeft,
            };

            // Black behind white, because a readout over a sunlit meadow is unreadable in
            // white alone and over a night forest unreadable in black.
            string said = $"{_rate} fps   worst {_worstSecond * 1000f:0} ms   "
                          + $"ever {_worstEver * 1000f:0} ms";

            var at = new Rect(12f, 12f, Screen.width - 24f, style.fontSize * 1.6f);

            style.normal.textColor = Color.black;
            GUI.Label(new Rect(at.x + 1f, at.y + 1f, at.width, at.height), said, style);

            style.normal.textColor = Color.white;
            GUI.Label(at, said, style);
        }
    }
}
