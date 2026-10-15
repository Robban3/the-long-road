using TheVeil.App;
using TheVeil.Gen;
using TheVeil.Sim;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// The harbour at the end of each coast level, photographed: `The Veil &gt; Harbour Photos`.
    ///
    /// <b>The last landing is the one level the whole road walks towards</b>, and until the
    /// pirate pack arrived what was moored at it was a five-metre rowing boat - the largest
    /// thing afloat in any pack the project owned, and a thing the code's own comment said
    /// a caravan of three wagons does not leave on. It is a warship now, thirty metres
    /// against a nine-metre dock, and the only way to know whether that reads is to look at
    /// it.
    ///
    /// Every coast level, not only the last: the nine others moor a boat, and a boat at a
    /// dock is what says this country lives off the water. A harbour that works on one
    /// level and sits in a cliff on another is exactly the fault a sheet of ten pictures
    /// finds and a single picture does not.
    ///
    /// Headless: unity run . -- -executeMethod TheVeil.Editor.HarbourPhotos.Run
    /// </summary>
    public static class HarbourPhotos
    {
        [UnityEditor.MenuItem("The Veil/Harbour Photos")]
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/PlayLevel.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<LevelRunner>();
            if (runner == null) { Debug.LogError("[Harbour] PlayLevel has no LevelRunner."); return; }

            string shots = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TheVeilHarbour");
            System.IO.Directory.CreateDirectory(shots);

            int chapter = Biomes.LastChapter;

            for (int level = 1; level <= Campaign.LevelsPerChapter; level++)
            {
                var root = SmokeTest.Build(runner, chapter, level, out _);
                var hull = Find(root.transform);

                if (hull == null)
                {
                    Debug.Log($"[Harbour] {chapter}-{level}: nothing moored");
                    Object.DestroyImmediate(root);
                    continue;
                }

                var box = TheVeil.View.ModelScaling.Measure(hull.gameObject);

                Debug.Log($"[Harbour] {chapter}-{level}: {hull.name} "
                          + $"{box.size.x:0.0} x {box.size.y:0.0} x {box.size.z:0.0} m"
                          + (Biomes.TheLastLanding(chapter, level) ? "  - the last landing" : ""));

                // <b>From the side, so the jetty and what is tied to it are both in
                // it.</b>
                //
                // This stood off the hull on a fixed bearing and photographed the hull.
                // The planking lies alongside, which means that on half the levels it is
                // directly behind the ship from wherever the camera happened to be: the
                // last landing came back looking as though it had no jetty at all, and the
                // jetty was five sections long and three metres from the hull. The same
                // mistake the fall report made for an hour, standing on the bank it was
                // trying to see past.
                //
                // So the two are found, the frame holds both, and the camera goes out
                // along the perpendicular of the line between them - the one bearing from
                // which neither can hide the other.
                // <b>The jetty is the subject and the ship is what it holds.</b>
                //
                // Two sets of pictures framed the hull and backed the camera off by the
                // hull's own size, and in both of them the planking was a dark smudge at
                // the edge or behind the ship entirely. It cannot be otherwise: the hull
                // is thirty metres and the planking is two across, so a frame that fits
                // the ship renders the jetty at a few pixels whichever way it faces.
                //
                // So the frame is taken from the jetty's head - the section the ship is
                // tied beside - and the ship is allowed to run out of shot. The camera
                // stands off along the line from the hull to that head, which is the one
                // bearing with the planking in front and the hull behind it rather than
                // the other way round.
                var pier = FindPier(root.transform, box.center);

                var subject = box;
                var from = new Vector3(1f, 0f, -1f).normalized;

                if (pier != null)
                {
                    subject = TheVeil.View.ModelScaling.Measure(pier.gameObject);

                    var line = subject.center - box.center;
                    line.y = 0f;

                    if (line.sqrMagnitude > 1f) from = line.normalized;
                }

                float away = Mathf.Max(subject.size.x, subject.size.z) * 1.1f + 22f;

                Shoot(subject.center + from * away + Vector3.up * (away * 0.55f),
                      subject.center, System.IO.Path.Combine(shots, $"harbour-{level}.png"));

                // <b>And the town, which the harbour shot cannot hold.</b>
                //
                // The frame above was widened by sixty metres each way to take in the
                // waterfront, and the picture that came back was a bay with a ship in it
                // and one hut among the trees - because a box centred on the jetty and
                // grown by a fixed amount is not where the street is. The street follows
                // the shoreline, which bends; it ran out of the right-hand edge of every
                // shot.
                //
                // The report counted thirteen buildings along that water at fifteen
                // metres apart while the picture showed one of them. So the street is
                // found the way the report finds it - the buildings themselves - and the
                // camera is put where a person standing off the harbour in a boat would
                // be: out on the water, looking back at the front.
                var town = Street(root.transform);

                if (town.size.x + town.size.z > 1f)
                {
                    var seaward = town.center - box.center;
                    seaward.y = 0f;

                    // Towards the hull, which is the one thing on the level that is
                    // certainly afloat, and so the one bearing that is certainly the sea.
                    var stand = seaward.sqrMagnitude > 1f ? -seaward.normalized : from;

                    float back = Mathf.Max(town.size.x, town.size.z) * 0.8f + 40f;

                    Shoot(town.center + stand * back + Vector3.up * (back * 0.5f),
                          town.center, System.IO.Path.Combine(shots, $"front-{level}.png"));

                    // And straight down, which is the one view that answers how the town
                    // is laid out rather than how it looks. From the water a street is a
                    // row of roofs with trees in front of it and the question of whether
                    // the ship lies in the middle of it cannot be read off at all.
                    var whole = new Bounds(town.center, town.size);
                    whole.Encapsulate(box.center);

                    float up = Mathf.Max(whole.size.x, whole.size.z) * 1.3f + 40f;

                    Shoot(whole.center + Vector3.up * up, whole.center,
                          System.IO.Path.Combine(shots, $"above-{level}.png"));
                }

                Wreck(root.transform, shots, level);

                Object.DestroyImmediate(root);
            }

            Debug.Log($"[Harbour] pictures in {shots}");
        }

        /// <summary>
        /// The box the waterfront buildings stand in.
        ///
        /// Taken off the node the pass builds under, not off the models' names: the
        /// country's village is built out of the same ten houses, and a frame that took
        /// those in as well was a frame round half the level. Empty where no front was
        /// built, which the caller checks.
        /// </summary>
        static Bounds Street(Transform at)
        {
            var quarter = Node(at, "Waterfront");
            if (quarter == null) return new Bounds();

            var box = new Bounds();
            bool any = false;

            foreach (Transform child in quarter)
            {
                if (!any) { box = new Bounds(child.position, Vector3.zero); any = true; }
                else box.Encapsulate(child.position);
            }

            return any ? box : new Bounds();
        }

        /// <summary>The child by that name, anywhere under here.</summary>
        static Transform Node(Transform at, string called)
        {
            foreach (Transform child in at)
            {
                if (child.name == called) return child;

                var found = Node(child, called);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// The hull, which is the largest thing on the level called a boat.
        ///
        /// <b>Largest rather than first-over-a-threshold, and the threshold was the
        /// bug.</b> This asked for the first boat-shaped mesh over a metre tall, on the
        /// reasoning that a rig is small pieces and a hull is not. A rowing boat is under a
        /// metre: eight of the ten harbours came back "nothing moored" and the ninth
        /// reported a boat drawn up on the beach, and none of it was true - the vessels
        /// were there and the finder could not see them.
        ///
        /// A level has one moored vessel and it is the biggest boat on it, so that is what
        /// is asked for. The rigging is still skipped by name, because a warship's sails
        /// are wide enough to win on size.
        /// </summary>
        static Transform Find(Transform at)
        {
            Transform best = null;
            float biggest = 0f;

            void Walk(Transform where)
            {
                foreach (Transform child in where)
                {
                    if (child.name.IndexOf("Boat", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && !SmokeTest.IsRigging(child.name))
                    {
                        var size = TheVeil.View.ModelScaling.Measure(child.gameObject).size;
                        float bulk = size.x * size.y * size.z;

                        if (bulk > biggest) { biggest = bulk; best = child; }
                    }

                    Walk(child);
                }
            }

            Walk(at);
            return best;
        }

        /// <summary>
        /// The head of the jetty: the section nearest what is tied up to it.
        ///
        /// <b>Nearest, and the first version took whichever came first in the
        /// hierarchy.</b> The planking is a run of five and the one the hierarchy reaches
        /// first is the shoreward end, so the line from ship to jetty came out pointing
        /// along the shore instead of across it - and a camera placed on the perpendicular
        /// of that line looks straight down the jetty with the hull broadside in front of
        /// it. Two sets of pictures were composed that way and the jetty is in neither.
        /// </summary>
        static Transform FindPier(Transform at, Vector3 near)
        {
            Transform best = null;
            float closest = float.MaxValue;

            void Walk(Transform where)
            {
                foreach (Transform child in where)
                {
                    if (child.name.IndexOf("Dock", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        float away = (child.position - near).sqrMagnitude;
                        if (away < closest) { closest = away; best = child; }
                    }

                    Walk(child);
                }
            }

            Walk(at);
            return best;
        }

        /// <summary>
        /// The hulk on the beach, photographed low and from the water.
        ///
        /// A wreck is meant to be lying across the waterline with the sea around the near
        /// side of it, and the only way that reads is from a camera at about the height
        /// of its own deck looking back at the shore. From above it is a brown shape on
        /// sand, which is what every shot of it would be if this used the same framing as
        /// the harbour.
        /// </summary>
        static void Wreck(Transform at, string shots, int level)
        {
            Transform found = null;

            void Walk(Transform where)
            {
                foreach (Transform child in where)
                {
                    if (found == null
                        && child.name.IndexOf("Shipwreck", System.StringComparison.OrdinalIgnoreCase) >= 0
                        && child.name.IndexOf("(Clone)", System.StringComparison.Ordinal) >= 0)
                        found = child;

                    Walk(child);
                }
            }

            Walk(at);
            if (found == null) { Debug.Log($"[Harbour] {level}: no wreck"); return; }

            var box = TheVeil.View.ModelScaling.Measure(found.gameObject);

            // Broadside, which is across the hull - the wreck is laid along the shore, so
            // its own sideways is out to sea.
            var side = found.right;
            side.y = 0f;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;

            float away = Mathf.Max(box.size.x, box.size.z) * 1.3f + 14f;

            Shoot(box.center + side.normalized * away + Vector3.up * (away * 0.35f),
                  box.center, System.IO.Path.Combine(shots, $"wreck-{level}.png"));
        }

        static void Shoot(Vector3 from, Vector3 at, string path)
        {
            var go = new GameObject("Harbour camera");
            var camera = go.AddComponent<Camera>();

            camera.transform.position = from;
            camera.transform.LookAt(at);
            camera.fieldOfView = 45f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.62f, 0.74f, 0.82f);
            camera.farClipPlane = 2000f;

            var texture = new RenderTexture(1100, 1100, 24);
            camera.targetTexture = texture;

            // Twice, the first thrown away: the first render of a freshly built level comes
            // back with half its surfaces blown out. See GroundPhotos.
            camera.Render();
            camera.Render();

            RenderTexture.active = texture;
            var shot = new Texture2D(1100, 1100, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, 1100, 1100), 0, 0);
            shot.Apply();
            RenderTexture.active = null;

            camera.targetTexture = null;
            Object.DestroyImmediate(go);

            System.IO.File.WriteAllBytes(path, shot.EncodeToPNG());
            Debug.Log($"[Harbour] {System.IO.Path.GetFileName(path)}");
        }
    }
}
