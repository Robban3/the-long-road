using System.Collections.Generic;
using TheVeil.Sim;
using TheVeil.UI;
using TheVeil.View;
using UnityEngine;

namespace TheVeil.App
{
    /// <summary>
    /// Every troop in the game on one field, marching into bandits and fighting them.
    ///
    /// A showcase, not a level: no caravan, no wagons, no route and no simulation. The
    /// escort's line holds six posts and there are thirteen kinds of troop, so no real
    /// level can show them all at once — and what is wanted here is to see each of them
    /// move and strike, not to balance a fight. So each troop gets a lane of its own
    /// with a handful of bandits coming down it, and the fight is staged: both sides
    /// close, the melee meets, the shooters loose as the bandits come on, the bandits
    /// fall, and after a breath it all starts again.
    ///
    /// Drawn by <see cref="RunVisuals"/> through the same spawn path the level uses —
    /// the same models, heights, liveries, weapons, animators and horse gait — so what
    /// is seen here is what the game shows.
    ///
    /// The scout and the priest march with the rest and do not strike: she fights
    /// nothing and he heals, which on a field with no wounds is standing ready. Their
    /// lanes are empty rather than theirs to lose.
    /// </summary>
    public sealed class TroopShowcase : MonoBehaviour
    {
        /// <summary>Filled by TheVeil &gt; Demo &gt; Troop Showcase from the setup's own library.</summary>
        public VisualLibrary Models = new VisualLibrary();

        const float LaneWidth = 6f;

        /// <summary>Where each side starts, either side of the line they meet on.</summary>
        const float StartGap = 24f;

        /// <summary>Where melee stands when it meets: shoulder to shoulder across the line.</summary>
        const float Contact = 1.3f;

        const float MarchSeconds = 7f;
        const float FightSeconds = 9f;
        const float RestSeconds = 3f;
        const float Cycle = MarchSeconds + FightSeconds + RestSeconds;

        /// <summary>How often a shooter looses, in seconds.</summary>
        const float ShotInterval = 1.2f;

        const int BanditsPerLane = 3;

        sealed class Lane
        {
            public TroopKind Kind;
            public float X;
            public bool Fights;
            public bool Shoots;

            /// <summary>Where the troop halts: at the line for blades, back from it for bows.</summary>
            public float Halt;

            public readonly List<Transform> Troops = new List<Transform>();
            public readonly List<Transform> Bandits = new List<Transform>();

            /// <summary>When in the cycle each bandit falls.</summary>
            public readonly List<float> Falls = new List<float>();

            public float NextShot;
        }

        RunVisuals _visuals;
        readonly List<Lane> _lanes = new List<Lane>();
        float _clock;
        System.Random _random = new System.Random(5701);

        void Start()
        {
            var cast = new GameObject("Cast").transform;
            _visuals = new RunVisuals(cast) { Library = Models };

            Ground();

            var kinds = TroopTable.All;
            float left = -(kinds.Length - 1) * LaneWidth * 0.5f;

            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                var lane = new Lane
                {
                    Kind = kind,
                    X = left + i * LaneWidth,
                    Fights = TroopTable.Dps(kind) > 0f,
                    Shoots = TroopTable.Range(kind) > 4f
                };

                // Back from the line by most of their reach, so they are seen to shoot
                // from a distance rather than from the bandits' elbows.
                lane.Halt = lane.Shoots ? -Mathf.Clamp(TroopTable.Range(kind) * 0.6f, 7f, 12f) : -Contact;

                for (int f = 0; f < TroopTable.Models(kind); f++)
                    lane.Troops.Add(_visuals.ShowTroop(kind, $"{kind}_{f}", Vector3.zero));

                if (lane.Fights)
                    for (int b = 0; b < BanditsPerLane; b++)
                        lane.Bandits.Add(_visuals.ShowEnemy(EnemyKind.Bandit, i, b, $"Bandit_{i}_{b}", Vector3.zero));

                Label(Names.Troop(kind), lane.X);
                _lanes.Add(lane);
            }

            Reset();
            Frame(kinds.Length * LaneWidth);
        }

        /// <summary>Back to the start of a cycle: both sides at their marks, every bandit on his feet.</summary>
        void Reset()
        {
            _clock = 0f;

            foreach (var lane in _lanes)
            {
                lane.Falls.Clear();
                lane.NextShot = MarchSeconds * 0.45f;

                // Shot down on the way in for the bows, cut down in the press for the blades.
                float from = lane.Shoots ? MarchSeconds * 0.55f : MarchSeconds + 1.5f;
                float to = MarchSeconds + FightSeconds - 1f;

                for (int b = 0; b < lane.Bandits.Count; b++)
                    lane.Falls.Add(Mathf.Lerp(from, to, (b + (float)_random.NextDouble() * 0.6f) / lane.Bandits.Count));
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            if (_clock >= Cycle) Reset();

            float march = Mathf.Clamp01(_clock / MarchSeconds);
            bool marching = _clock < MarchSeconds;
            bool fighting = _clock >= MarchSeconds && _clock < MarchSeconds + FightSeconds;

            foreach (var lane in _lanes) Stage(lane, march, marching, fighting);

            _visuals.AdvanceShots(dt);
        }

        void Stage(Lane lane, float march, bool marching, bool fighting)
        {
            // Directions rather than rotations: RunVisuals turns each model by its own
            // yaw on top, as the level does, so a figure saved facing sideways still
            // walks forwards.
            var forward = Vector3.forward;
            var back = Vector3.back;

            // The troop: marching up to its halt, then holding it.
            float troopZ = Mathf.Lerp(-StartGap, lane.Halt, march);
            float troopPace = marching ? (StartGap + lane.Halt) / MarchSeconds : 0f;

            bool anyStanding = false;
            for (int b = 0; b < lane.Bandits.Count; b++)
                if (_clock < lane.Falls[b]) anyStanding = true;

            bool shooting = lane.Shoots && anyStanding && _clock >= MarchSeconds * 0.45f && _clock < MarchSeconds + FightSeconds;
            bool striking = lane.Fights && !lane.Shoots && fighting && anyStanding;

            for (int f = 0; f < lane.Troops.Count; f++)
            {
                var spot = Spot(lane.X, troopZ, f, lane.Troops.Count, -1f);
                _visuals.Stage(lane.Troops[f], spot, forward, shooting || striking ? 0f : troopPace, shooting || striking, false);
            }

            // The bandits: down the lane towards the troop, stopping at the line or at
            // the shooters, and dropping where they are when their time comes.
            // The bandits come the whole way in every lane: a shooter who halts ten metres
            // back and is then walked up to has nothing to shoot across.
            float banditStop = Contact;
            float banditZ = Mathf.Lerp(StartGap, banditStop, Mathf.Clamp01(_clock / MarchSeconds));
            float banditPace = banditZ > banditStop + 0.01f ? (StartGap - banditStop) / MarchSeconds : 0f;

            for (int b = 0; b < lane.Bandits.Count; b++)
            {
                bool dead = _clock >= lane.Falls[b];
                var spot = Spot(lane.X, banditZ, b, lane.Bandits.Count, 1f);

                // A fallen man stays where he fell; the ones still up keep coming.
                if (dead) _visuals.Stage(lane.Bandits[b], spot, back, 0f, false, true);
                else _visuals.Stage(lane.Bandits[b], spot, back, banditPace, banditPace <= 0f, false);
            }

            if (!shooting || _clock < lane.NextShot) return;
            lane.NextShot = _clock + ShotInterval / Mathf.Max(1, lane.Troops.Count) * 1.6f;

            // At somebody still standing, from one of the shooters in turn.
            int target = -1;
            for (int b = 0; b < lane.Bandits.Count; b++)
                if (_clock < lane.Falls[b]) { target = b; break; }
            if (target < 0) return;

            var shooter = lane.Troops[_random.Next(lane.Troops.Count)];
            _visuals.Loose(lane.Kind, shooter.position + Vector3.up * 1.5f,
                           lane.Bandits[target].position + Vector3.up * 1.1f);
        }

        /// <summary>A figure's place in its group: side by side across the lane, a little staggered.</summary>
        static Vector3 Spot(float laneX, float z, int index, int count, float side)
        {
            float across = count <= 1 ? 0f : (index - (count - 1) * 0.5f) * 1.3f;
            float stagger = (index % 2) * 0.8f * side;
            return new Vector3(laneX + across, 0f, z + stagger);
        }

        void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Field";
            ground.transform.localScale = new Vector3(12f, 1f, 8f);

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null)
            {
                var grass = new Material(shader) { name = "Field" };
                grass.SetColor("_BaseColor", new Color(0.33f, 0.42f, 0.24f));
                grass.SetFloat("_Smoothness", 0f);
                ground.GetComponent<Renderer>().sharedMaterial = grass;
            }

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientLight = new Color(0.55f, 0.58f, 0.62f);
        }

        void Label(string text, float x)
        {
            var label = new GameObject("Label_" + text);
            label.transform.position = new Vector3(x, 0.05f, -StartGap - 3.2f);
            // Lying on the ground and read from the camera, which looks up the field from behind.
            label.transform.rotation = Quaternion.Euler(70f, 0f, 0f);

            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = 0.16f;
            mesh.fontSize = 48;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.color = Color.white;
        }

        /// <summary>A camera behind the escort's side, high enough to take in every lane.</summary>
        static void Frame(float width)
        {
            var camera = Camera.main;
            if (camera == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                camera = go.AddComponent<Camera>();
            }

            camera.fieldOfView = 50f;
            camera.farClipPlane = 400f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.58f, 0.66f, 0.74f);

            float distance = width * 0.62f;
            camera.transform.position = new Vector3(0f, distance * 0.62f, -StartGap - distance * 0.55f);
            camera.transform.LookAt(new Vector3(0f, 0f, -2f));
        }
    }
}
