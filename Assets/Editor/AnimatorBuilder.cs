using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace TheVeil.Editor
{
    /// <summary>
    /// Generates one animator controller per model.
    ///
    /// Every rig in the packs is Generic rather than Humanoid, so clips cannot be
    /// shared between models — a knight's walk will not drive a wolf. That means one
    /// controller each, which is tedious by hand and trivial to generate: the states
    /// are always the same four, only the clips differ.
    ///
    /// Clip names are matched by suffix because the packs disagree on the prefix
    /// (CharacterArmature vs AnimalArmature) and on what an attack is called — Sword,
    /// Sword_Slash, Attack and Attack_Kick all mean the same thing here.
    /// </summary>
    public static class AnimatorBuilder
    {
        const string OutputDir = "Assets/_Project/Animation";

        /// <summary>Clip suffixes tried in order for each state, first match wins.</summary>
        // "Fly" last in both lists, and in both on purpose. A bird in the air has no
        // idle: standing still and travelling are the same kind of thing, so the eagle's
        // flight clips have to answer for both states or the controller has a hole in it
        // where Speed is zero — and a bird frozen mid-air is worse than no bird.
        //
        // Last, so nothing that owns a real idle can lose it to a flight clip.
        //
        // Which flight clip is not settled here. Both lists end in "Fly", so both match
        // the same one, and the eagle has four of them — a soar and a wingbeat among
        // them. See SortOutTheFlying, which measures them instead of reading their names.
        static readonly string[] IdleNames = { "Idle", "Idle_Neutral", "Idle_2", "Eat", "Graze", "Fly" };
        static readonly string[] WalkNames = { "Walk", "Walking", "Trot", "Run", "Running", "Gallop", "Fly" };
        // The last few in each are for clips brought in from outside, which nobody here
        // named. A Mixamo file is called what the animation is called on the website —
        // "Sword And Shield Slash", "Walking", "Dying" — and the matcher falls back to
        // Contains, so a token that appears in the name is enough. Adding an alias costs
        // nothing and the alternative is a soldier who does not swing.
        static readonly string[] AttackNames =
        {
            "Sword", "Sword_Slash", "Attack", "Attack_Kick", "Bite", "Punch", "Punch_Right",
            "Slash", "Swing", "Strike", "Stab", "Melee", "Chop"
        };

        /// <summary>
        /// What an archer does, tried before <see cref="AttackNames"/> for a bow.
        ///
        /// Its own list because the matcher takes the first name that hits and every
        /// entry in AttackNames is a melee move. The archer therefore bound to a sword
        /// slash and stood there swinging his bow like a club while his arrows flew —
        /// the one troop whose whole point is fighting at a distance, animated as if it
        /// fought at arm's length.
        ///
        /// "Bow" first and alone at the top, because a Mixamo file is called what the
        /// animation is called on the website and every bow animation there has the word
        /// in it. The rest are what the same move is called elsewhere.
        /// </summary>
        static readonly string[] BowNames =
        {
            "Bow", "Crossbow", "Shoot", "Shooting", "Draw", "Aim", "Fire",
            "Arrow", "Loose", "Release", "Archer", "Archery"
        };

        static readonly string[] DeathNames = { "Death", "Die", "Dying", "Killed", "Fall" };

        static readonly string[] Models =
        {
            "Assets/Quaternius/Knight/Knight.fbx",
            "Assets/Quaternius/Animals/Horse.fbx",

            // ForestAnimals. The pack ships its own controllers and they cannot be used:
            // this project drives every animator through Speed, Attack and Dead, and a
            // controller with other parameter names is a controller nothing can steer.
            // The wolf that came out of that sat in its default state and slid along
            // beside the caravan — which looks exactly like being dragged, because it is.
            //
            // So the clips are taken from the pack and a controller built here instead.
            "Assets/ForestAnimals/Models/Wolf.fbx",
            "Assets/ForestAnimals/Models/Fox.fbx",
            "Assets/ForestAnimals/Models/DeerFemale.fbx",
            "Assets/ForestAnimals/Models/DeerMale.fbx",
            "Assets/ForestAnimals/Models/Boar.fbx",

            // Four flight clips and nothing else — no idle, no walk, no attack. See the
            // clip-name lists above for how a controller gets built out of that, and
            // SortOutTheFlying for which of the four ends up under each state.
            "Assets/ThirdParty/Eagle/Eagle_B1.Fbx",

            "Assets/Quaternius/ModularMen/Adventurer.fbx",
            "Assets/Quaternius/ModularMen/Farmer.fbx",
            "Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
            "Assets/Quaternius/PiratePack/Characters_Henry.fbx"
        };

        /// <summary>The medieval army pack, and the controller its characters share.</summary>
        public const string ArmyPack = "Assets/Stylized_Medieval_Army_Pack";
        public const string ArmyCharacters = ArmyPack + "/Prefabs - Characters";

        /// <summary>
        /// Where the army pack's characters get their animation from.
        ///
        /// **The pack ships none.** `Build Army Animator` searched every asset under it
        /// and found not one clip: 22 FBXs of meshes, 52 prefabs assembled from them, and
        /// nothing that moves. Its characters were to borrow a Quaternius character's,
        /// which is what Humanoid retargeting is for — a clip described in terms of a
        /// human skeleton rather than of bone names plays on any human skeleton.
        ///
        /// **And there is nothing here to borrow.** The army pack's own rigs map onto a
        /// human skeleton; 17 of them did. Every animated character in this project is a
        /// Quaternius rig, and they all share one skeleton whose arms end at the forearm.
        /// No hands. Unity's humanoid avatar requires fifteen bones with LeftHand and
        /// RightHand among them, so no Quaternius file can be the source — not with a
        /// different importer setting and not by hand, because the Configure screen maps
        /// bones that exist. Unity's own error names the wrong bone, which cost a day:
        /// `Required human bone 'LeftFoot' not found`, and the foot is right there.
        ///
        /// So the army moves when somebody puts a rig with hands in it. Any humanoid FBX
        /// in <see cref="BorrowedClips"/> is tried first and its clips are what the whole
        /// army plays; a Mixamo download without skin is enough. Until then the pack's
        /// characters stand still, and anything that has to move keeps the old models.
        ///
        /// `Rig For Retargeting` is what makes that possible, and has to be run once.
        /// </summary>
        public const string ArmyController = OutputDir + "/" + ArmyName + ".controller";

        /// <summary>The name of the controller the whole army plays.</summary>
        // Its own name rather than the knight's. Which file the clips come out of is a
        // question with more than one answer — the first source that maps wins — and a
        // controller named after whichever one happened to win is a controller nothing
        // else can refer to. TheVeilSetup needs this path at compile time.
        const string ArmyName = "Army";

        /// <summary>
        /// The archers' controller: the same clips, a different choice for Attack.
        ///
        /// A second controller rather than a second skeleton, and that is what makes it
        /// cheap. All fifty-two army characters share one rig, so both controllers are
        /// assembled from the same borrowed clip pool — only the Attack state differs,
        /// picked from <see cref="BowNames"/> instead of the melee list.
        /// </summary>
        public const string ArcherController = OutputDir + "/" + ArcherName + ".controller";

        const string ArcherName = "ArmyArcher";

        /// <summary>Where to drop humanoid clips brought in from outside.</summary>
        // Anything here is tried before the packs, because the packs cannot supply what
        // is wanted. Every animated character in this project is a Quaternius rig, and
        // they all share one skeleton that ends at the forearm: no hands. Unity's
        // humanoid avatar requires fifteen bones with LeftHand and RightHand among them,
        // so not one of these files can be made humanoid — now or by hand, because
        // Configure maps bones that exist and cannot invent one.
        //
        // So the way in is a file from somewhere that rigs hands: a Mixamo download
        // ("Without Skin" is enough — the clips are what is wanted, not the model), or
        // any animation pack whose rig is already Humanoid. Drop it in this folder and
        // run the menu item; nothing else needs changing.
        const string BorrowedClips = OutputDir + "/Humanoid";

        /// <summary>The files the borrowed clips might come out of, best first.</summary>
        // More than one, because the first is not guaranteed to work and the failure is
        // not the kind anyone can see coming: whether Unity can read a skeleton as human
        // is decided by bone names and hierarchy inside a binary file. The knight is
        // first because his clips are the ones this game was built around — a sword, a
        // walk, a death — and the others are asked only if he cannot be made humanoid.
        static readonly string[] ClipSources =
        {
            "Assets/Quaternius/Knight/Knight.fbx",
            "Assets/Quaternius/ModularMen/Adventurer.fbx",
            "Assets/Quaternius/ModularMen/Farmer.fbx",
            "Assets/Quaternius/PiratePack/Characters_Henry.fbx",
            "Assets/Quaternius/PiratePack/Characters_Captain_Barbarossa.fbx",
        };

        /// <summary>Fewest bones a skeleton can have and still be a person.</summary>
        // A longbow in the army pack is skinned to a skeleton as well, and it is not a
        // person. Forcing it humanoid asks Unity to find a pelvis in a bow; it says no,
        // at length, and the warning buries the one rig that mattered. A human avatar
        // needs fifteen bones at the very least; a bow has three.
        const int HumanBones = 15;

        /// <summary>
        /// Builds the one controller every character in the army pack uses.
        ///
        /// Separate from `Build Animator Controllers` because it answers a different
        /// question. That one walks a list of models this project knows the animation
        /// lives inside; this one goes looking, because with the army pack nobody knows
        /// yet whether there is any animation at all — its 22 FBXs are meshes and its
        /// characters are prefabs assembled from them.
        ///
        /// Whatever it says is worth reading. Clips found and matched means the troops
        /// move; clips found and unmatched means the names need adding to the lists at
        /// the top of this file; nothing found means the pack ships no animation, and
        /// the way out is Humanoid retargeting off a pack that does.
        /// </summary>

        /// <summary>
        /// Makes the army pack's characters into Humanoid rigs and finds them something
        /// to play: `The Veil > Rig For Retargeting`.
        ///
        /// The army pack has 52 characters and no animation whatsoever. Every other pack
        /// here ships a model and its clips in one file, and this project has always read
        /// them straight out of it — bone name to bone name, which only ever works within
        /// one file. Humanoid is Unity's way round that: the importer maps a skeleton onto
        /// a standard human one, a clip is stored as *what a human did* rather than as
        /// what these particular bones did, and it then plays on any other rig that has
        /// been mapped the same way.
        ///
        /// Two halves, and both are needed. The clips must be re-imported as humanoid,
        /// which is a Quaternius file; and every rig that is to play them must have an
        /// avatar, which is the army pack's meshes.
        ///
        /// **It can fail, and it fails quietly.** Unity maps bones by guessing from their
        /// names and hierarchy, and a rig it cannot read produces an invalid avatar and
        /// no error. So every avatar is checked afterwards and named — an invalid one has
        /// to be fixed by hand in the importer's Configure screen, and knowing which is
        /// most of that work.
        ///
        /// **And it used to fail destructively**, which is worse, and is the reason this
        /// method is shaped the way it is. A Generic rig plays the clips inside its own
        /// file. Switched to Humanoid it plays clips written for *a human* instead — and
        /// if the mapping then fails it has neither: no avatar to retarget onto, and no
        /// way back to its own animation. That is what happened to the knight, who was
        /// the source of every clip in the game. Twenty rigs were converted, his avatar
        /// came out invalid, his clips went with it, and every soldier in the caravan
        /// stopped moving — reported as one warning among warnings.
        ///
        /// So nothing is left converted unless it maps. A rig that fails goes back to
        /// Generic before the next one is touched, and if no clip source can be made
        /// humanoid at all the run changes nothing and says so. The old models moving
        /// beats the new models frozen.
        /// </summary>
        /// <summary>Whether the editor is out of play mode. See TheVeilSetup.Stopped.</summary>
        // A re-import during play is undone the moment play stops, so this would report
        // twenty rigs converted and leave twenty rigs exactly as they were.
        static bool Stopped(string what)
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return true;

            Debug.LogWarning($"[The Veil] Stop play mode first — {what} changed nothing. "
                             + "It re-imports assets, and a re-import during play is "
                             + "discarded when play stops.");
            return false;
        }

        [MenuItem("The Veil/Rig For Retargeting")]
        public static void RigForRetargeting()
        {
            if (!Stopped("Rig For Retargeting")) return;

            var prefabs = AssetDatabase.FindAssets("t:Prefab", new[] { ArmyCharacters });

            if (prefabs.Length == 0)
            {
                Debug.LogWarning($"[The Veil] No prefabs under {ArmyCharacters}. Either the pack is "
                                 + "not in this project or that folder is named something else.");
                return;
            }

            var rigs = new List<string>();
            var parts = new List<string>();
            int skinned = 0, boneless = 0;

            // The files the characters are actually made of, found through the prefabs
            // rather than listed: a table of prefab-to-FBX would be wrong the first time
            // the pack is updated.
            foreach (var guid in prefabs)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null) continue;

                var skins = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skins.Length == 0) { boneless++; continue; }

                skinned++;

                foreach (var skin in skins)
                {
                    if (skin.sharedMesh == null) continue;

                    string path = AssetDatabase.GetAssetPath(skin.sharedMesh);
                    if (string.IsNullOrEmpty(path)) continue;

                    var into = skin.bones.Length < HumanBones ? parts : rigs;
                    if (!into.Contains(path)) into.Add(path);
                }
            }

            // The finding that decides whether any of this is possible, said before any
            // of the rest of it.
            //
            // A skinned mesh is a mesh bound to a skeleton. A character built without one
            // is a statue: no bones, so no avatar, so nothing to retarget onto, and no
            // amount of importer settings will change that. The pack's own description is
            // careful about this in hindsight — it calls the *bow* fully rigged and never
            // says the same of the characters.
            // A file can hold both — a character and a bow — so anything that is a person
            // somewhere is a person, and only what is nowhere a person is a part.
            foreach (string path in rigs) parts.Remove(path);

            // And a horse is not a person either, whatever Unity is willing to call it.
            //
            // The mapper guesses from bone names and a horse has a hip, a spine and a
            // head like anybody, so MC_Horse_Saddle was converted without a word of
            // complaint, given the human controller every other character shares, and
            // spent the game reared up on its hind legs the length of the caravan. The
            // avatar being valid is not the same as the rig being a person.
            var beasts = new List<string>();

            for (int i = rigs.Count - 1; i >= 0; i--)
                if (NotAPerson(rigs[i])) { beasts.Add(rigs[i]); rigs.RemoveAt(i); }

            foreach (string path in beasts) Generic(path);

            Debug.Log($"[The Veil] {prefabs.Length} character prefab(s): {skinned} with a skeleton, "
                      + $"{boneless} without"
                      + (parts.Count > 0
                         ? $", and {parts.Count} rigged part(s) with too few bones to be a "
                           + $"person: {string.Join(", ", Names(parts))}."
                         : ".")
                      + (beasts.Count > 0
                         ? $" {string.Join(", ", Names(beasts))} left on Generic: not people."
                         : string.Empty));

            if (rigs.Count == 0)
            {
                Debug.LogWarning("[The Veil] Not one of them is skinned to a skeleton, so they are "
                                 + "static meshes. There is nothing to animate and nothing to "
                                 + "retarget onto — no importer setting fixes a model that has "
                                 + "no bones. The choice is between statues in the new "
                                 + "silhouettes and the old models that move.");
                return;
            }

            // The source, before anything else is touched. If no file can supply humanoid
            // clips then converting the army pack achieves nothing and costs those rigs
            // their own animation, so the army is left alone until a source is found.
            // Brought-in clips first, and as a set: one Mixamo download is one clip, so
            // what answers here is the folder rather than any file in it.
            int borrowed = MakeHumanoid(BorrowedClips);

            string source = null;
            var refused = new List<string>();

            // The borrowed folder *supplements* the packs; it does not replace them.
            //
            // It used to: one humanoid file in there and the packs were never asked, on
            // the reasoning that anything brought in on purpose is better than anything
            // found. That is true clip for clip and false for a controller, because a
            // controller needs an idle before it needs anything else. One Mixamo download
            // is one clip. So a single bow animation dropped in this folder skipped the
            // search, converted all 17 army rigs to Humanoid, then built both controllers
            // out of a pool with no idle in it — Assemble returns null — and left the
            // army humanoid with no controller at all. Adding a clip made the army worse.
            //
            // So the question is not "is there anything here" but "can what is here fill
            // the idle", and the packs are asked whenever it cannot.
            var lent = Gather(BorrowedClips);
            bool lentIsEnough = borrowed > 0 && HasIdle(lent);

            if (!lentIsEnough) source = FindClipSource(out refused);

            // Every refused source was converted and put back, and a round trip through
            // Humanoid regenerates the clips inside the file. Its own controller refers
            // to those clips, so it is built again from the ones that exist now — a
            // candidate that was only ever asked a question should not be left worse for
            // having been asked.
            foreach (string path in refused) Build(path);

            // Nothing to stand still on, from either half. Said here, before the rigs are
            // touched, because there is then nothing to undo: they are all still Generic
            // and still playing whatever they came with.
            if (source == null && borrowed > 0 && !lentIsEnough)
            {
                NameFromFile(lent);

                var have = new List<string>();
                foreach (var clip in lent) have.Add(Label(clip));

                Debug.LogWarning($"[The Veil] {BorrowedClips} holds {lent.Count} clip(s) — "
                                 + $"{string.Join(", ", have)} — and not one of them is an "
                                 + "idle, so there is no controller to build: a rig with no "
                                 + "standing-still state has nowhere to be when it is not "
                                 + "doing anything. Not one army rig has been touched: they are "
                                 + "all still Generic and still playing their own animation. Add at "
                                 + "least an idle to that folder"
                                 + (Missing(lent).Count > 0
                                    ? $" (missing: {string.Join(", ", Missing(lent))})"
                                    : string.Empty)
                                 + " and run this again.");
                return;
            }

            if (borrowed == 0 && source == null)
            {
                var why = new List<string>();

                foreach (string path in refused)
                {
                    var missing = Absent(path);
                    why.Add($"{Path.GetFileNameWithoutExtension(path)} has no "
                            + (missing.Count > 0 ? string.Join(" and no ", missing)
                                                 : "skeleton Unity could read"));
                }

                // What is missing, rather than which requirement the mapper tripped over
                // first. The two are not the same and only one of them can be acted on.
                Debug.LogWarning("[The Veil] Nothing here can be read as a human skeleton, so there "
                                 + $"are no clips to lend: {string.Join("; ", why)}. A humanoid "
                                 + "avatar needs fifteen bones and Configure can only map ones "
                                 + "that exist, so this is not a setting and cannot be corrected "
                                 + "by hand. Every rig has been left as it was, Generic and "
                                 + "playing its own animation. The way in is a rig that has the "
                                 + $"missing parts: put any humanoid FBX in {BorrowedClips} — a "
                                 + "Mixamo download without skin will do, the clips are what is "
                                 + "wanted — and run this again.");
                return;
            }

            // The bow is not a person and never was. An earlier run said Human to it
            // anyway, so it is put back — a prop carrying an invalid human avatar is
            // harmless and is also a warning nobody can act on, printed next to the one
            // that matters.
            foreach (string path in parts) Generic(path);

            int changed = 0;
            var broken = new List<string>();

            foreach (string path in rigs)
            {
                if (Humanoid(path)) changed++;
                else broken.Add(Path.GetFileNameWithoutExtension(path));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // The clips are humanoid now, so the controllers built from them have to be
            // built again: the old ones hold the generic versions, which retarget onto
            // nothing.
            //
            // Two controllers out of one pool — the army's and the archers'. Same clips
            // and same skeleton; only the Attack state is chosen differently, so a bowman
            // draws where a swordsman swings. The army's is built under its own name
            // rather than the source file's, because TheVeilSetup asks for that path at
            // compile time and cannot know which file won.
            //
            // Both pools, borrowed first. Match tries an exact name across the whole pool
            // before it tries EndsWith and Contains, so a pack's plain `Idle` still wins
            // the idle state over a long borrowed filename that merely contains the word —
            // and BowNames finds `Aim` only in what was brought in, which is the whole
            // reason for bringing it in.
            var pools = new List<string>();
            if (borrowed > 0) pools.Add(BorrowedClips);
            if (source != null) pools.Add(Path.GetDirectoryName(source));

            BuildFromFolders(ArmyName, pools);
            BuildFromFolders(ArcherName, pools, BowNames);

            // The source's own models still play its own controller, under its own name.
            if (source != null) Build(source);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var from = new List<string>();
            if (borrowed > 0) from.Add($"{borrowed} humanoid file(s) in {BorrowedClips}");
            if (source != null) from.Add($"{Path.GetFileNameWithoutExtension(source)}'s");

            string chose = string.Join(" and ", from);

            Debug.Log($"[The Veil] {rigs.Count} army rig(s), {changed} now Humanoid. They play "
                      + $"{chose} clips through {ArmyController}."
                      + (refused.Count > 0
                         ? $" {string.Join(", ", Names(refused))} could not be read as human, "
                           + "and was put back the way it was."
                         : string.Empty));

            if (broken.Count > 0)
                Debug.LogWarning("[The Veil] Unity could not map these onto a human skeleton: "
                                 + $"{string.Join(", ", broken)}. They are back on Generic and "
                                 + "will stand still — a rig with an invalid avatar plays nothing "
                                 + "at all. To bring one in, select the file, Rig > Configure, "
                                 + "and fix whatever is red.");
        }

        /// <summary>
        /// The first file that both has clips and can be read as a human skeleton.
        /// </summary>
        /// <param name="refused">
        /// The paths that had clips and could not be mapped. Kept, because that is the
        /// list somebody has to open Configure on, because a source silently skipped
        /// reads exactly like a source that was never tried, and because each of them was
        /// converted and put back and so needs its own controller rebuilt.
        /// </param>
        static string FindClipSource(out List<string> refused)
        {
            refused = new List<string>();

            foreach (string path in Candidates())
            {
                if (!(AssetImporter.GetAtPath(path) is ModelImporter)) continue;

                // Anything an earlier run left humanoid-but-unmapped has no clips to
                // count, so put it back first and ask afterwards. This is the repair for
                // a project already in that state, and it runs before anything else.
                if (!Mapped(path)) Generic(path);

                if (LoadClips(path).Count == 0) continue;

                if (Humanoid(path)) return path;

                refused.Add(path);
            }

            return null;
        }

        /// <summary>Whether a rig is something other than a person, by two tests.</summary>
        // Two, because neither is enough on its own. A skeleton with no hand cannot be a
        // human avatar however Unity feels about it — that catches the plain horse. And a
        // rig that carries a rider *does* have hands, so the cavalry passes the first test
        // and is caught by its name instead. Names are a poor rule and this is the place
        // for one: the alternative is a horse walking on two legs through the caravan.
        static readonly string[] Beasts = { "horse", "cavalry", "mount", "steed", "pony" };

        static bool NotAPerson(string path)
        {
            string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            foreach (string beast in Beasts)
                if (name.Contains(beast)) return true;

            return Absent(path).Count > 0;
        }

        /// <summary>
        /// Makes every model in a folder Humanoid, and says how many of them took.
        ///
        /// Whatever is in here came from outside on purpose, so each file is asked on its
        /// own and a file that will not map is put back rather than stopping the rest —
        /// a folder of twenty Mixamo downloads should not be lost to one bad export.
        /// </summary>
        static int MakeHumanoid(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return 0;

            int mapped = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { folder }))
                if (Humanoid(AssetDatabase.GUIDToAssetPath(guid))) mapped++;

            return mapped;
        }

        /// <summary>Everything worth asking, whatever was brought in first.</summary>
        static List<string> Candidates()
        {
            var paths = new List<string>();

            if (AssetDatabase.IsValidFolder(BorrowedClips))
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { BorrowedClips }))
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));

            paths.AddRange(ClipSources);
            return paths;
        }

        /// <summary>
        /// Which parts of a human the rig has no bone for, by name.
        ///
        /// Unity reports the first requirement its guess could not satisfy, which is not
        /// the same as the reason. The knight's arms end at the forearm; the error read
        /// `Required human bone 'LeftFoot' not found`, and the foot is right there in the
        /// file. Reading the whole skeleton at once and saying what is absent turns an
        /// hour in the Configure screen into a decision that takes a second — a bone that
        /// is not in the file cannot be mapped to, and no amount of configuring adds one.
        /// </summary>
        static List<string> Absent(string path)
        {
            var bones = new List<string>();

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is GameObject model)
                    foreach (var joint in model.GetComponentsInChildren<Transform>(true))
                        bones.Add(joint.name.ToLowerInvariant());

            var missing = new List<string>();

            foreach (var part in HumanParts)
            {
                bool found = false;

                foreach (string bone in bones)
                {
                    foreach (string word in part.Value)
                        if (bone.Contains(word)) { found = true; break; }

                    if (found) break;
                }

                if (!found) missing.Add(part.Key);
            }

            return missing;
        }

        /// <summary>The parts a humanoid avatar cannot be built without, and their aliases.</summary>
        // Names rather than Unity's HumanBodyBones, because the question here is what the
        // artist called things. Every rigger spells these differently and all of them are
        // recognisable; what is not recoverable is a part nobody modelled at all.
        static readonly KeyValuePair<string, string[]>[] HumanParts =
        {
            new KeyValuePair<string, string[]>("hips", new[] { "hips", "pelvis" }),
            new KeyValuePair<string, string[]>("a spine", new[] { "spine", "torso", "chest", "abdomen" }),
            new KeyValuePair<string, string[]>("a head", new[] { "head" }),
            new KeyValuePair<string, string[]>("upper arms", new[] { "upperarm", "arm.upper", "upper_arm", "humerus" }),
            new KeyValuePair<string, string[]>("forearms", new[] { "lowerarm", "forearm", "lower_arm", "elbow" }),
            new KeyValuePair<string, string[]>("hands", new[] { "hand", "wrist", "palm" }),
            new KeyValuePair<string, string[]>("thighs", new[] { "upperleg", "thigh", "upper_leg" }),
            new KeyValuePair<string, string[]>("shins", new[] { "lowerleg", "shin", "calf", "lower_leg", "knee" }),
            new KeyValuePair<string, string[]>("feet", new[] { "foot", "ankle" }),
        };

        static List<string> Names(List<string> paths)
        {
            var names = new List<string>();
            foreach (string path in paths) names.Add(Path.GetFileNameWithoutExtension(path));
            return names;
        }

        /// <summary>
        /// Re-imports a model as a Humanoid rig, and puts it back if that does not work.
        /// Returns whether the rig ended up humanoid and mapped.
        /// </summary>
        // The revert is the whole point. Setting animationType and walking away is what
        // took the knight's clips: the importer accepts Human from any rig and reports
        // nothing, and a rig that is humanoid without a valid avatar plays neither the
        // clips it was given nor the ones it came with. Asking afterwards costs one
        // re-import and is the difference between a failed conversion and a broken model.
        static bool Humanoid(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return false;

            if (importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.SaveAndReimport();
            }

            if (Mapped(path)) return true;

            Generic(path);
            return false;
        }

        /// <summary>Puts a model back to the rig it was imported with.</summary>
        static void Generic(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) return;
            if (importer.animationType == ModelImporterAnimationType.Generic) return;

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.SaveAndReimport();
        }

        /// <summary>Whether the importer managed to build a usable human avatar.</summary>
        static bool Mapped(string path)
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Avatar avatar) return avatar.isValid && avatar.isHuman;

            return false;
        }

        [MenuItem("The Veil/Build Army Animator")]
        public static void BuildArmyAnimator()
        {
            if (!AssetDatabase.IsValidFolder(ArmyPack))
            {
                Debug.LogWarning($"[The Veil] {ArmyPack} is not in this project.");
                return;
            }

            var controller = BuildFromFolder("Army", ArmyPack);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (controller == null)
            {
                Debug.Log("[The Veil] Nothing to build, which is the answer: the pack has no "
                          + "animation of its own, so its characters borrow a Quaternius "
                          + $"character's through {ArmyController}. Run The Veil > Rig For "
                          + "Retargeting once, then Refresh Scene Assets.");
                return;
            }

            Debug.LogWarning("[The Veil] The army pack turned out to have clips of its own after "
                             + $"all, and they have just overwritten {ArmyController}. That is "
                             + "the right outcome — borrowing was only ever the answer to it "
                             + "having none — but Rig For Retargeting will overwrite them back "
                             + "if it is run again, so stop running it.");
        }

        [MenuItem("The Veil/Build Animator Controllers")]
        public static void BuildAll()
        {
            if (!AssetDatabase.IsValidFolder(OutputDir))
                AssetDatabase.CreateFolder("Assets/_Project", "Animation");

            int built = 0;
            var missed = new List<string>();

            foreach (var model in Models)
            {
                if (Build(model) != null) built++;
                else missed.Add(Path.GetFileNameWithoutExtension(model));
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // A count on its own cannot be read. "Built eleven" is the right answer for
            // eleven models and a silent failure for twelve, and the difference only
            // showed up because somebody happened to know what the list was that day.
            // The per-model warnings were there and scrolled past; a total that names
            // its denominator does not need anyone to go looking.
            if (missed.Count == 0)
            {
                Debug.Log($"[The Veil] Built {built} of {Models.Length} animator controllers in {OutputDir}.");
                return;
            }

            Debug.LogWarning($"[The Veil] Built {built} of {Models.Length} animator controllers in "
                             + $"{OutputDir}. No controller for: {string.Join(", ", missed)}. "
                             + "The warning above each one says why — a missing file reads as "
                             + "\"No clips\", a file whose clips this project cannot name reads "
                             + "as \"No idle clip\".");
        }

        static string Name(AnimationClip clip) => Label(clip);

        /// <summary>
        /// Builds one controller from every clip in a folder, wherever they live.
        ///
        /// <see cref="Build"/> reads clips out of one FBX, which is right for the packs
        /// that ship a model and its animation in the same file. The medieval army pack
        /// does not: its 22 FBXs are meshes, its characters are prefabs assembled from
        /// them, and whatever animation it has is somewhere else in the folder —
        /// possibly `.anim` assets, possibly sub-assets of a rig file this project never
        /// names. Searching by type finds them either way, and finding nothing is itself
        /// the answer to a question that was otherwise going to cost a round trip.
        ///
        /// One controller for the whole pack rather than one per character. They share a
        /// skeleton — the pack builds 52 characters out of 22 meshes — so a clip that
        /// binds to one binds to all of them, and 52 identical controllers would be 52
        /// assets saying the same thing.
        /// </summary>
        public static AnimatorController BuildFromFolder(string name, string folder,
                                                        string[] prefer = null)
            => BuildFromFolders(name, new List<string> { folder }, prefer);

        /// <summary>
        /// The same, out of several folders at once, earliest folder first.
        ///
        /// Because the borrowed clips and a pack's clips are not alternatives. One Mixamo
        /// download is one clip, so what somebody drops in the borrowed folder is a bow
        /// draw or a walk — never a whole character's worth — and a controller assembled
        /// from it alone has holes in it. Read together, the borrowed clip fills the one
        /// state it was downloaded for and the pack fills the rest.
        /// </summary>
        public static AnimatorController BuildFromFolders(string name, IList<string> folders,
                                                          string[] prefer = null)
        {
            var clips = new List<AnimationClip>();
            var seen = new HashSet<string>();
            var read = new List<string>();

            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    Debug.LogWarning($"[The Veil] {folder} is not a folder in this project.");
                    continue;
                }

                read.Add(folder);
                Gather(folder, clips, seen);
            }

            if (clips.Count == 0)
            {
                Debug.LogWarning("[The Veil] No animation clips anywhere under "
                                 + $"{string.Join(", ", folders)}. The pack ships none, so its "
                                 + "characters will hold their bind pose. The way out is "
                                 + "Humanoid retargeting — set both this pack's rigs and a pack "
                                 + "that does have clips to Humanoid, and Unity will play one "
                                 + "on the other.");
                return null;
            }

            string folder0 = string.Join(" + ", read);

            NameFromFile(clips);

            // Standing still, walking and *striking* have to loop, and a clip from
            // outside arrives set to play once. One file per clip here, so each is
            // switched on in its own importer — and a re-import destroys the clip
            // objects, so they are read again afterwards.
            //
            // The attack was missed the first time round, which is why a soldier swung
            // once and then stood in the last frame of the swing for the rest of the
            // fight. Attack is a bool rather than a trigger precisely because fighting
            // here lasts as long as something is in contact: the state is entered when
            // the group has a target and left when it has none, and what happens in
            // between is the clip repeating.
            if (Looped(Match(clips, IdleNames)) | Looped(Match(clips, WalkNames))
                                                 | Looped(Match(clips, AttackNames)))
                return BuildFromFolders(name, folders, prefer);

            var names = new List<string>();
            foreach (var clip in clips) names.Add(Label(clip));

            Debug.Log($"[The Veil] {clips.Count} clip(s) under {folder0}: {string.Join(", ", names)}");

            return Assemble(name, $"{folder0} (folder)", clips, prefer);
        }

        /// <summary>Every clip under a folder, wherever in a file it lives.</summary>
        static void Gather(string folder, List<AnimationClip> clips, HashSet<string> seen)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AnimationClip clip)) continue;
                    if (clip.name.StartsWith("__preview")) continue;
                    if (!seen.Add($"{path}:{clip.name}")) continue;

                    clips.Add(clip);
                }
            }
        }

        /// <summary>Every clip under a folder, as a list. Reads nothing and changes nothing.</summary>
        static List<AnimationClip> Gather(string folder)
        {
            var clips = new List<AnimationClip>();
            if (!AssetDatabase.IsValidFolder(folder)) return clips;

            Gather(folder, clips, new HashSet<string>());
            return clips;
        }

        /// <summary>Whether a pool has something to stand still on, which is the one state
        /// <see cref="Assemble"/> refuses to build without.</summary>
        static bool HasIdle(List<AnimationClip> clips)
        {
            if (clips.Count == 0) return false;

            NameFromFile(clips);
            return Match(clips, IdleNames) != null;
        }

        /// <summary>Which of the four states a pool cannot fill, by name.</summary>
        // For the warning, so somebody who has to go back to Mixamo knows what to search
        // for rather than being told to try again.
        static List<string> Missing(List<AnimationClip> clips)
        {
            NameFromFile(clips);

            var missing = new List<string>();

            if (Match(clips, IdleNames) == null) missing.Add("an idle (Mixamo: \"Idle\")");
            if (Match(clips, WalkNames) == null) missing.Add("a walk (\"Walking\")");
            if (Match(clips, AttackNames) == null) missing.Add("a melee attack (\"Sword And Shield Slash\")");
            if (Match(clips, BowNames) == null) missing.Add("a bow attack (\"Standing Draw Arrow\")");
            if (Match(clips, DeathNames) == null) missing.Add("a death (\"Dying\")");

            return missing;
        }

        public static AnimatorController Build(string modelPath, string name = null)
        {
            var clips = LoadClips(modelPath);
            if (clips.Count == 0)
            {
                Debug.LogWarning($"[The Veil] No clips in {modelPath}");
                return null;
            }

            var idle = Match(clips, IdleNames);
            var walk = Match(clips, WalkNames);
            var attack = Match(clips, AttackNames);
            var death = Match(clips, DeathNames);

            SortOutTheFlying(clips, ref idle, ref walk);

            // A reimport destroys the clip objects loaded above, so everything is chosen
            // again from the new ones.
            if (EnsureLooping(modelPath, idle, walk, attack))
            {
                clips = LoadClips(modelPath);
                if (clips.Count == 0) return null;

                idle = Match(clips, IdleNames);
                walk = Match(clips, WalkNames);
                attack = Match(clips, AttackNames);
                death = Match(clips, DeathNames);

                SortOutTheFlying(clips, ref idle, ref walk);
            }

            ReportFlight(clips, modelPath, idle, walk);

            return Assemble(name ?? Path.GetFileNameWithoutExtension(modelPath), modelPath, clips);
        }

        /// <summary>
        /// Turns a heap of clips into the four states this project drives, and writes the
        /// controller.
        ///
        /// Split out of <see cref="Build"/> so the same four states can be assembled from
        /// clips found anywhere — one FBX, or a whole pack folder. Which clip fills which
        /// state is the only decision in here, and it is made the same way either way.
        /// </summary>
        /// <param name="prefer">
        /// Names tried for the Attack state before the melee list, or null for melee.
        /// The archers pass <see cref="BowNames"/>; everything else passes nothing and
        /// gets exactly the controller it got before.
        /// </param>
        static AnimatorController Assemble(string name, string source, List<AnimationClip> clips,
                                           string[] prefer = null)
        {
            var idle = Match(clips, IdleNames);
            var walk = Match(clips, WalkNames);
            var death = Match(clips, DeathNames);

            var wanted = prefer == null ? null : Match(clips, prefer);
            var attack = wanted ?? Match(clips, AttackNames);

            // Said out loud, because a bow that turned out to be a sword is invisible
            // from the outside and the reason for it is not a code fault.
            //
            // If this warns, the clips brought in do not contain an archer — nothing in
            // BowNames matched anything in the pool — and no amount of work in this file
            // will produce one. What is needed then is the clip: `The Veil > List Clips`
            // says what is there, and a Mixamo "Standing Draw Arrow" dropped into
            // BorrowedClips is enough.
            if (prefer != null && wanted == null)
                Debug.LogWarning($"[The Veil] {name}: no bow clip among {clips.Count} in {source}, "
                                 + $"so the archers fall back to '{Label(attack)}' and swing "
                                 + "instead of drawing. This is a missing clip, not a bug.");

            SortOutTheFlying(clips, ref idle, ref walk);

            if (idle == null)
            {
                Debug.LogWarning($"[The Veil] No idle clip in {source}. Nothing here is named "
                                 + "anything this project recognises as standing still — see "
                                 + "IdleNames, which is a list to add to rather than a rule.");
                return null;
            }

            string path = $"{OutputDir}/{name}.controller";

            // Said out loud, because a controller built from the wrong clips and one
            // built from the right ones are the same file from the outside, and the
            // difference only shows up as an animal standing still in a running game.
            Debug.Log($"[The Veil] {name}: idle={Name(idle)} walk={Name(walk)} "
                      + $"attack={Name(attack)} death={Name(death)}  ({clips.Count} clips)");

            Loop(idle, walk, attack);

            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // Attack is a bool rather than a trigger: fighting here lasts as long as
            // something is in contact, so the swing should loop rather than fire once.
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

            var machine = controller.layers[0].stateMachine;

            var idleState = machine.AddState("Idle");
            idleState.motion = idle;
            idleState.speed = IsFlight(idle) ? FlightSpeed : 1f;
            machine.defaultState = idleState;

            if (walk != null)
            {
                var walkState = machine.AddState("Walk");
                walkState.motion = walk;
                walkState.speed = IsFlight(walk) ? FlightSpeed : 1f;

                // Threshold sits well below a walking pace so the column does not
                // shuffle between idle and walk while creeping through a marsh.
                Transition(idleState, walkState, AnimatorConditionMode.Greater, 0.15f, "Speed");
                Transition(walkState, idleState, AnimatorConditionMode.Less, 0.15f, "Speed");
            }

            if (attack != null)
            {
                var attackState = machine.AddState("Attack");
                attackState.motion = attack;

                // Entered from anywhere, so a troop caught mid-stride still turns and
                // fights instead of finishing its walk cycle first.
                var toAttack = machine.AddAnyStateTransition(attackState);
                toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
                toAttack.hasExitTime = false;
                toAttack.duration = 0.1f;
                toAttack.canTransitionToSelf = false;

                var fromAttack = attackState.AddTransition(idleState);
                fromAttack.AddCondition(AnimatorConditionMode.IfNot, 0f, "Attack");
                fromAttack.hasExitTime = false;
                fromAttack.duration = 0.15f;
            }

            if (death != null)
            {
                var deathState = machine.AddState("Death");
                deathState.motion = death;

                // From anywhere: dying interrupts whatever was happening.
                var toDeath = machine.AddAnyStateTransition(deathState);
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
                toDeath.hasExitTime = false;
                toDeath.duration = 0.1f;
                toDeath.canTransitionToSelf = false;
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// How fast a flight clip is played back, and only a flight clip.
        ///
        /// 0.45, and this time from the argument rather than from a cautious nudge. A
        /// fifth off was tried first, was not enough, and was not going to be: the beat
        /// is wrong by a factor, not by a margin.
        ///
        /// The factor is the bird's size. She is drawn at a ten-metre wingspan against a
        /// real eagle's two, because at life size she is a speck over a 256 m map (see
        /// <c>VisualLibrary.EagleSpan</c>). Wingbeat frequency falls roughly with the
        /// square root of length for animals of the same shape, so a bird very nearly
        /// five times over should beat about √5 ≈ 2.2 times slower. 1 ÷ 2.2 = 0.45.
        ///
        /// Which is the useful thing about it: the clip is authored for a two-metre bird
        /// and is being watched on a ten-metre one, so it was never going to look right
        /// at any speed chosen by eye — it looks right at the speed its size asks for.
        ///
        /// Nothing that walks is touched. A troop's stride is tied to how fast the
        /// caravan is actually moving, and slowing the clip would put the feet out of
        /// step with the ground.
        /// </summary>
        public const float FlightSpeed = 0.45f;

        static bool IsFlight(AnimationClip clip)
            => clip != null && Contains(Bare(clip.name), "Fly");

        /// <summary>
        /// Switches loop on for clips that are their own assets.
        ///
        /// <see cref="EnsureLooping"/> goes through the model importer, which is right
        /// for a clip that is a sub-asset of an FBX and impossible for one that is not:
        /// a standalone `.anim` has no importer to ask. Its loop flag lives in the clip's
        /// own settings instead.
        ///
        /// Death is left alone here for the same reason it is there — a looping death is
        /// a corpse that keeps getting up to die again.
        /// </summary>
        static void Loop(params AnimationClip[] clips)
        {
            foreach (var clip in clips)
            {
                if (clip == null || clip.isLooping) continue;

                // Sub-assets of a model are the importer's to change, and it has already
                // had its chance by the time this runs.
                string path = AssetDatabase.GetAssetPath(clip);
                if (AssetImporter.GetAtPath(path) is ModelImporter) continue;

                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);

                EditorUtility.SetDirty(clip);
                Debug.Log($"[The Veil] loop switched on for {clip.name}, which was set to play once.");
            }
        }

        /// <summary>
        /// Switches loop on for the clips that are meant to run continuously.
        ///
        /// **Unity imports every clip with Loop Time off.** A cycle that does not loop
        /// plays once and holds its last frame, so a wingbeat lasting a second gives one
        /// beat and then a bird gliding for the rest of the level — which is
        /// indistinguishable from an animator that never ran at all, and cost a round
        /// being mistaken for one.
        ///
        /// Idle, walk and attack only. **Death must never loop**: it is entered from
        /// anywhere on a bool that stays true, and a looping death animation is a corpse
        /// that keeps getting up to die again. Attack loops on purpose — fighting here
        /// lasts as long as something is in contact, so the swing should repeat.
        ///
        /// Written through the importer rather than onto the clip, because a clip is a
        /// sub-asset of the model file: anything set on it directly is regenerated away
        /// the next time the file is imported.
        /// </summary>
        /// <summary>Switches looping on for one clip, in whatever file it came from.</summary>
        // The folder case: BuildFromFolder gathers clips from many files, and each has
        // its own importer to be told. Returns whether anything changed, which is also
        // whether the caller's clip objects have just been thrown away.
        static bool Looped(AnimationClip clip)
        {
            if (clip == null) return false;

            string path = AssetDatabase.GetAssetPath(clip);
            return !string.IsNullOrEmpty(path) && EnsureLooping(path, clip);
        }

        static bool EnsureLooping(string modelPath, params AnimationClip[] wanted)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer)) return false;

            var names = new List<string>();
            foreach (var clip in wanted)
                if (clip != null) { names.Add(clip.name); names.Add(Bare(clip.name)); }

            if (names.Count == 0) return false;

            // defaultClipAnimations is what the file itself declares, and is what an
            // untouched model has: reading clipAnimations alone comes back empty and
            // there is nothing to switch anything on for.
            var settings = importer.clipAnimations;
            if (settings == null || settings.Length == 0) settings = importer.defaultClipAnimations;
            if (settings == null || settings.Length == 0) return false;

            var looped = new List<string>();

            foreach (var setting in settings)
            {
                if (setting.loopTime) continue;
                if (!names.Contains(setting.name) && !names.Contains(Bare(setting.name))) continue;

                setting.loopTime = true;
                looped.Add(setting.name);
            }

            if (looped.Count == 0) return false;

            importer.clipAnimations = settings;
            importer.SaveAndReimport();

            Debug.Log($"[The Veil] {Path.GetFileNameWithoutExtension(modelPath)}: loop switched on "
                      + $"for {string.Join(", ", looped)} — they were set to play once.");
            return true;
        }

        /// <summary>
        /// Chooses between flight clips, which the name lists cannot do.
        ///
        /// The eagle ships four of them — `Fly_01` at 6 s, `Fly_02` at 5 s, `Fly_03` at
        /// 8.33 s, `Fly_04` at 1 s — and they are emphatically not interchangeable: some
        /// of a bird's flying is beating and some of it is soaring. Both lists end in
        /// "Fly", both matched `Fly_01`, and `Fly_01` turns out to be a glide. So the
        /// bird was textured, moving, animated, and had wings that did not beat, which
        /// is three fixes deep into a fault that was never any of them.
        ///
        /// Length looks like the tell and is not one: a one-second clip is probably a
        /// single wingbeat, but a six-second one may be six of them. What settles it is
        /// how much the skeleton actually moves per second, which is a thing that can be
        /// measured rather than guessed at — see <see cref="Cadence"/>. The busiest clip
        /// drives the travelling state and the calmest one the idle, which is also the
        /// right answer for a bird: hanging on a thermal is what it does when it is not
        /// going anywhere.
        /// </summary>
        static void SortOutTheFlying(List<AnimationClip> clips,
                                     ref AnimationClip idle, ref AnimationClip walk)
        {
            var flying = clips.FindAll(clip => Contains(Bare(clip.name), "Fly"));
            if (flying.Count < 2) return;

            flying.Sort((a, b) => Cadence(a).CompareTo(Cadence(b)));

            var calmest = flying[0];
            var busiest = flying[flying.Count - 1];

            if (flying.Contains(idle)) idle = calmest;
            if (flying.Contains(walk)) walk = busiest;
        }

        /// <summary>
        /// The measurements behind the choice above, said out loud once.
        ///
        /// Separate from the choosing because the choosing may run twice — a clip that
        /// had to be switched to looping is reimported, which destroys every clip object
        /// and means picking again from the new ones. The numbers are worth one line,
        /// not two identical ones.
        /// </summary>
        static void ReportFlight(List<AnimationClip> clips, string modelPath,
                                 AnimationClip idle, AnimationClip walk)
        {
            var flying = clips.FindAll(clip => Contains(Bare(clip.name), "Fly"));
            if (flying.Count < 2) return;

            var measured = new List<string>();
            foreach (var clip in flying)
                measured.Add($"{Bare(clip.name)} {Cadence(clip):0.00}/s"
                             + (clip.isLooping ? "" : " (plays once!)"));

            Debug.Log($"[The Veil] {Path.GetFileNameWithoutExtension(modelPath)} flight clips: "
                      + $"{string.Join(", ", measured)}. Travelling on {Name(walk)}, "
                      + $"idling on {Name(idle)}, both at {FlightSpeed:0.00}x.");
        }

        /// <summary>
        /// How much a clip's skeleton turns per second of it.
        ///
        /// Summed off the rotation curves, because that is what a wingbeat is, and with
        /// the root left out: a soaring bird banking across the sky moves its root more
        /// than a flapping one moves it at all, so counting the root would pick exactly
        /// the wrong clip. The absolute value matters not at all — only the ordering
        /// between clips out of the same file, which is why raw quaternion components
        /// are fine to add up.
        /// </summary>
        static float Cadence(AnimationClip clip)
        {
            if (clip == null || clip.length < 0.01f) return 0f;

            float turned = 0f;

            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (string.IsNullOrEmpty(binding.path)) continue;
                if (binding.propertyName.IndexOf("Rotation", System.StringComparison.Ordinal) < 0
                    && binding.propertyName.IndexOf("Euler", System.StringComparison.Ordinal) < 0)
                    continue;

                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                if (curve == null || curve.length < 2) continue;

                for (int i = 1; i < curve.length; i++)
                    turned += Mathf.Abs(curve[i].value - curve[i - 1].value);
            }

            return turned / clip.length;
        }

        static void Transition(AnimatorState from, AnimatorState to,
                               AnimatorConditionMode mode, float threshold, string parameter)
        {
            var transition = from.AddTransition(to);
            transition.AddCondition(mode, threshold, parameter);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
        }

        static List<AnimationClip> LoadClips(string modelPath)
        {
            var clips = new List<AnimationClip>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(modelPath))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    clips.Add(clip);
            return clips;
        }

        /// <summary>
        /// Finds a clip whose name ends with one of the wanted suffixes. Exact matches
        /// win over partial ones, so "Idle" is not beaten by "Idle_HitReact_Left".
        /// </summary>
        /// <summary>
        /// Finds the clip for a state, trying the names in order and, for each, three
        /// increasingly forgiving readings of "matches".
        ///
        /// It used to demand an exact name, which the comment at the top of this file
        /// has always described as matching by suffix — and which was true enough while
        /// every pack here happened to call its clip `Idle`. ForestAnimals calls it
        /// `Wolf_Idle`, the build found no idle, and the animals stood in bind pose
        /// while the simulation walked them about.
        ///
        /// Exact first, so a pack that names a clip plainly still wins it over a longer
        /// name that merely contains the word.
        /// </summary>
        static AnimationClip Match(List<AnimationClip> clips, string[] wanted)
        {
            foreach (var name in wanted)
            {
                var clip = Match(clips, name, Exactly) ?? Match(clips, name, EndsWith)
                           ?? Match(clips, name, Contains);
                if (clip != null) return clip;
            }
            return null;
        }

        static AnimationClip Match(List<AnimationClip> clips, string wanted,
                                   System.Func<string, string, bool> how)
        {
            foreach (var clip in clips)
                if (how(Label(clip), wanted)) return clip;
            return null;
        }

        /// <summary>The part after the last bar: exporters prefix clips with the take's name.</summary>
        static string Bare(string name)
        {
            int bar = name.LastIndexOf('|');
            return bar >= 0 ? name.Substring(bar + 1) : name;
        }

        /// <summary>Clip names that say nothing about what the clip animates.</summary>
        // Every Mixamo download holds one clip called "mixamo.com", whatever it does.
        // Matching on that finds a walk in a death and a death in a walk, so the file
        // name is the name instead: Walking.fbx is the walk. The packs name their takes
        // properly and none of this touches them.
        static readonly string[] Nameless = { "mixamo.com", "take 001", "unnamed", "default" };

        /// <summary>What to call each clip, where its own name will not do.</summary>
        // Beside the clips rather than on them: the name of an imported sub-asset belongs
        // to the importer, and writing to it is refused. This is read by Label and by
        // nothing else, and is rebuilt every time a folder is read.
        static readonly Dictionary<AnimationClip, string> Labels =
            new Dictionary<AnimationClip, string>();

        static void NameFromFile(List<AnimationClip> clips)
        {
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                if (System.Array.IndexOf(Nameless, Bare(clip.name).ToLowerInvariant()) < 0)
                    continue;

                string path = AssetDatabase.GetAssetPath(clip);
                if (string.IsNullOrEmpty(path)) continue;

                Labels[clip] = Path.GetFileNameWithoutExtension(path);
            }
        }

        /// <summary>What a clip is called, for matching and for saying so.</summary>
        static string Label(AnimationClip clip)
            => clip == null ? "—"
             : Labels.TryGetValue(clip, out string name) ? name
             : Bare(clip.name);

        /// <summary>
        /// Says what every controller is actually holding: `The Veil > Report Animation`.
        ///
        /// The build already logs its choices, and a log line is only useful to somebody
        /// who was watching the console at the moment it appeared. Three rounds went on
        /// the question "did the attack clip get in", which is a fact sitting in an asset
        /// on disk and can be read whenever it is asked for.
        ///
        /// It reads the controllers rather than rebuilding them, so it answers about the
        /// files the game will actually play — including a controller built by an older
        /// version of this code, which is exactly the case worth catching.
        /// </summary>
        [MenuItem("The Veil/Report Animation")]
        public static void ReportAnimation()
        {
            if (AssetDatabase.IsValidFolder(BorrowedClips))
            {
                var guids = AssetDatabase.FindAssets("t:Model", new[] { BorrowedClips });
                var found = new List<string>();

                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var clips = LoadClips(path);
                    NameFromFile(clips);

                    string rig = Mapped(path) ? "humanoid" : "NOT humanoid";
                    var names = new List<string>();
                    foreach (var clip in clips) names.Add(Label(clip));

                    found.Add($"{Path.GetFileNameWithoutExtension(path)} ({rig}, "
                              + (names.Count > 0 ? string.Join("/", names) : "no clips") + ")");
                }

                Debug.Log(guids.Length == 0
                    ? $"[The Veil] {BorrowedClips} is there and empty."
                    : $"[The Veil] {guids.Length} borrowed file(s): {string.Join(", ", found)}");
            }
            else
            {
                Debug.Log($"[The Veil] No {BorrowedClips} folder, so nothing has been brought in.");
            }

            foreach (var guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { OutputDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null || controller.layers.Length == 0) continue;

                var wanted = new List<string> { "Idle", "Walk", "Attack", "Death" };
                var report = new List<string>();

                foreach (string state in wanted)
                {
                    var motion = MotionOf(controller, state);

                    report.Add(motion == null
                        ? $"{state}=—"
                        : $"{state}={motion.name}");
                }

                Debug.Log($"[The Veil] {Path.GetFileName(path)}: {string.Join("  ", report)}"
                          + (MotionOf(controller, "Attack") == null
                             ? "   ← no attack state, so nothing swings whatever the game asks for"
                             : string.Empty));
            }
        }

        /// <summary>The clip a named state plays, or null when the state was never built.</summary>
        static Motion MotionOf(AnimatorController controller, string state)
        {
            foreach (var child in controller.layers[0].stateMachine.states)
                if (string.Equals(child.state.name, state, System.StringComparison.OrdinalIgnoreCase))
                    return child.state.motion;

            return null;
        }

        static bool Exactly(string a, string b)
            => string.Equals(a, b, System.StringComparison.OrdinalIgnoreCase);

        static bool EndsWith(string a, string b)
            => a.EndsWith(b, System.StringComparison.OrdinalIgnoreCase);

        static bool Contains(string a, string b)
            => a.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
