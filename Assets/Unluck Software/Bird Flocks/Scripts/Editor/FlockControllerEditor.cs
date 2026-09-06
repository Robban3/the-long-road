/*
Copyright Unluck Software
www.chemicalbliss.com
*/

using UnityEngine;
using UnityEditor;

namespace UnluckSoftware {

[CustomEditor(typeof(FlockController))]
[CanEditMultipleObjects]
[System.Serializable]

public class FlockControllerEditor :Editor
{
	public SerializedProperty avoidanceMask;
	FlockController target_cs;

	public void OnEnable()
	{
		target_cs = (FlockController)target;
		avoidanceMask = serializedObject.FindProperty("_avoidanceMask");
	}

	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		
		bool propFold = EditorPrefs.GetBool("UnluckSoftware.BF.Properties", true);
		bool behFold = EditorPrefs.GetBool("UnluckSoftware.BF.Behaviour", true);
		bool animFold = EditorPrefs.GetBool("UnluckSoftware.BF.Animations", false);
		bool avoidFold = EditorPrefs.GetBool("UnluckSoftware.BF.Avoidance", false);
		bool groupFold = EditorPrefs.GetBool("UnluckSoftware.BF.Grouping", false);

		GUIBeginBox();
		if (GUILayout.Button(GUIButtonText("Flock Properties", propFold), EditorStyles.boldLabel))
			EditorPrefs.SetBool("UnluckSoftware.BF.Properties", !propFold);
		if (propFold)
		{
			GUIBeginBox("", true, 2);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_childPrefab"), new GUIContent("Bird Prefab"));
			EditorGUILayout.LabelField("Drag & Drop bird prefab from project folder", EditorStyles.miniLabel);
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Roaming Area", EditorStyles.boldLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionSphere"), new GUIContent("Roaming Area Width"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionSphereDepth"), new GUIContent("Roaming Area Depth"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_positionSphereHeight"), new GUIContent("Roaming Area Height"));
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Flock Size", EditorStyles.boldLabel);
			EditorGUILayout.IntSlider(serializedObject.FindProperty("_childAmount"), 0, 2000, new GUIContent("Bird Amount"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnSphere"), new GUIContent("Flock Width"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnSphereDepth"), new GUIContent("Flock Depth"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_spawnSphereHeight"), new GUIContent("Flock Height"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_startPosOffset"), new GUIContent("Start Position Offset"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_slowSpawn"), new GUIContent("Slowly Spawn Birds"));
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_skipFrame"), new GUIContent("Skip Frame"));
			EditorGUILayout.LabelField("Run script every other frame to increase performance.", EditorStyles.miniLabel);
			GUIEndBox();
		}
		GUIEndBox();

		GUIBeginBox();
		if (GUILayout.Button(GUIButtonText("Behaviors and Appearance", behFold), EditorStyles.boldLabel))
			EditorPrefs.SetBool("UnluckSoftware.BF.Behaviour", !behFold);
		if (behFold)
		{
			GUIBeginBox("", true, 2);	
			EditorGUILayout.LabelField("Change how the birds move and behave", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_minSpeed"), new GUIContent("Birds Min Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxSpeed"), new GUIContent("Birds Max Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_minAcceleration"), new GUIContent("Min Acceleration"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxAcceleration"), new GUIContent("Max Acceleration"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_diveValue"), new GUIContent("Birds Dive Depth"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_diveFrequency"), new GUIContent("Birds Dive Chance"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_soarFrequency"), new GUIContent("Birds Soar Chance"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_soarMaxTime"), new GUIContent("Soar Time (0 = Always)"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_minDamping"), new GUIContent("Min Damping Turns"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxDamping"), new GUIContent("Max Damping Turns"));
			EditorGUILayout.LabelField("Bigger number = faster turns", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_minScale"), new GUIContent("Birds Min Scale"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxScale"), new GUIContent("Birds Max Scale"));
			EditorGUILayout.LabelField("Randomize size of birds when added", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdRoll"), new GUIContent("Bird Roll"));
			if (target_cs._birdRoll)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdRollAmount"), new GUIContent("Roll Amount"));
			}
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("LimitPitchRotation"), new GUIContent("Disable Pitch Rotation"));
			EditorGUILayout.LabelField("Flattens out rotation when flying or soaring upwards", EditorStyles.miniLabel);
			if (target_cs.LimitPitchRotation)
			{
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_flatSoar"), new GUIContent("Flat Soar"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_flatFly"), new GUIContent("Flat Flap"));
			}
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Bird Trigger Flock Waypoint", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Birds own waypoit triggers a new flock waypoint", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_childTriggerPos"), new GUIContent("Bird Trigger Waypoint"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_waypointDistance"), new GUIContent("Distance To Waypoint"));
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Automatic Flock Waypoint", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Automaticly change the flock waypoint (0 = never)", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_randomPositionTimer"), new GUIContent("Auto Waypoint Delay"));
			GUIEndBox();
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Force Bird Waypoints", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Force all birds to change waypoints when flock changes waypoint", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_forceChildWaypoints"), new GUIContent("Force Bird Waypoints"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_forcedRandomDelay"), new GUIContent("Bird Waypoint Delay"));
			GUIEndBox();
		}
		GUIEndBox();

		GUIBeginBox();
		if (GUILayout.Button(GUIButtonText("Animations", animFold), EditorStyles.boldLabel))
			EditorPrefs.SetBool("UnluckSoftware.BF.Animations", !animFold);
		if (animFold)
		{
			GUIBeginBox("", true, 2);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_soarAnimation"), new GUIContent("Soar Animation"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_flapAnimation"), new GUIContent("Flap Animation"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_idleAnimation"), new GUIContent("Idle Animation"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_minAnimationSpeed"), new GUIContent("Min Anim Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxAnimationSpeed"), new GUIContent("Max Anim Speed"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_idleAnimationSpeed"), new GUIContent("Idle Anim Speed"));
			EditorGUILayout.LabelField("Animation speed when landed. Increase for fast birds like hummingbirds.", EditorStyles.miniLabel);
			GUIEndBox();
		}
		GUIEndBox();

		GUIBeginBox();
		if (GUILayout.Button(GUIButtonText("Avoidance", avoidFold), EditorStyles.boldLabel))
			EditorPrefs.SetBool("UnluckSoftware.BF.Avoidance", !avoidFold);
		if (avoidFold)
		{
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Avoidance", EditorStyles.boldLabel);
			EditorGUILayout.LabelField("Birds will steer away from colliders (Ray)", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoid"), new GUIContent("Bird Avoid"));
			if (target_cs._birdAvoid)
			{
				EditorGUILayout.PropertyField(avoidanceMask, new GUIContent("Collider Mask"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_disableLandingAvoidanceDistance"), new GUIContent("Disable Landing Avoidance Distance"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidHorizontalForce"), new GUIContent("Avoid Horizontal Force"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidDistanceMin"), new GUIContent("Avoid Distance Min"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidDistanceMax"), new GUIContent("Avoid Distance Max"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidDown"), new GUIContent("Avoid Colliders Under"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidUp"), new GUIContent("Avoid Colliders Over"));
				if (target_cs._birdAvoidDown || target_cs._birdAvoidUp)
				{
					EditorGUILayout.PropertyField(serializedObject.FindProperty("_birdAvoidVerticalForce"), new GUIContent("Avoid Vertical Force"));
				}
			}
			GUIEndBox();
		}
		GUIEndBox();

		GUIBeginBox();
		if (GUILayout.Button(GUIButtonText("Grouping", groupFold), EditorStyles.boldLabel))
			EditorPrefs.SetBool("UnluckSoftware.BF.Grouping", !groupFold);
		if (groupFold)
		{
			GUIBeginBox("", true, 2);
			EditorGUILayout.LabelField("Move birds into a parent transform", EditorStyles.miniLabel);
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_groupChildToFlock"), new GUIContent("Group to Flock"));
			if (target_cs._groupChildToFlock)
			{
				GUI.enabled = false;
			}
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_groupChildToNewTransform"), new GUIContent("Group to New GameObject"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("_groupName"), new GUIContent("Group Name"));
			GUI.enabled = true;
			GUIEndBox();
		}
		GUIEndBox();

		serializedObject.ApplyModifiedProperties();
	}

	string GUIButtonText(string s, bool b)
	{
		if (b) return "= " + s;
		return "+ " + s;
	}
	
	void GUIBeginBox(string label = "", bool white = false, int s = 0)
	{
		if (white)
		{
			if (EditorGUIUtility.isProSkin)
				GUI.color = new Color(1.8f, 1.8f, 1.8f);
			else
				GUI.color = new Color(1.2f, 1.2f, 1.2f);
		} else
		{
			if (EditorGUIUtility.isProSkin)
				GUI.color = new Color(.55f, .55f, .55f);
			else
				GUI.color = new Color(.95f, .95f, .95f);
		}
		GUIStyle b = new GUIStyle("Box");
		EditorGUILayout.BeginVertical(b);
		GUI.color = Color.white;
		if (label != "") EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
		if (s > 0) GUILayout.Space(s);
	}
	
	static void GUIEndBox()
	{
		EditorGUILayout.EndVertical();
	}
}
}