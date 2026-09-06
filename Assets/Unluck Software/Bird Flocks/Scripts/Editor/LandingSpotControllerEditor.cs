/*
Copyright Unluck Software
www.chemicalbliss.com
*/

using UnityEngine;
using UnityEditor;

namespace UnluckSoftware
{

	[CustomEditor(typeof(LandingSpotController))]
	[CanEditMultipleObjects]
	[System.Serializable]
	public class LandingSpotControllerEditor : Editor
	{
		LandingSpotController target_cs;

		public void OnEnable()
		{
			target_cs = (LandingSpotController)target;
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			bool setupFold = EditorPrefs.GetBool("UnluckSoftware.LS.Setup", true);
			bool timingFold = EditorPrefs.GetBool("UnluckSoftware.LS.Timings", true);
			bool movementFold = EditorPrefs.GetBool("UnluckSoftware.LS.Movement", false);
			bool visualFold = EditorPrefs.GetBool("UnluckSoftware.LS.Visuals", false);

			GUIBeginBox();
			if (GUILayout.Button(GUIButtonText("Landing Spot Setup", setupFold), EditorStyles.boldLabel))
				EditorPrefs.SetBool("UnluckSoftware.LS.Setup", !setupFold);
			if (setupFold)
			{
				GUIBeginBox("", true, 2);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_flock"), new GUIContent("Flock Controller"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_landOnStart"), new GUIContent("Land On Start"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_onlyBirdsAbove"), new GUIContent("Only Birds Above"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_parentBirdToSpot"), new GUIContent("Parent Bird To Spot"));
				GUIEndBox();

				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Detection Range", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_minBirdDistance"), new GUIContent("Min Distance"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxBirdDistance"), new GUIContent("Max Distance"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_takeClosest"), new GUIContent("Take Closest Bird"));
				GUIEndBox();
			}
			GUIEndBox();

			GUIBeginBox();
			if (GUILayout.Button(GUIButtonText("Timings and Safety", timingFold), EditorStyles.boldLabel))
				EditorPrefs.SetBool("UnluckSoftware.LS.Timings", !timingFold);
			if (timingFold)
			{
				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Landing & Dismount Delays", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_autoCatchDelay"), new GUIContent("Auto Catch Delay"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_autoDismountDelay"), new GUIContent("Auto Dismount Delay"));
				GUIEndBox();

				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Idle Animation Delay", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("idleAnimationDelayMin"), new GUIContent("Min Delay"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("idleAnimationDelayMax"), new GUIContent("Max Delay"));
				GUIEndBox();

				GUIBeginBox("", true, 2);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_abortLanding"), new GUIContent("Abort Stuck Landings"));
				if (target_cs._abortLanding)
				{
					EditorGUILayout.PropertyField(serializedObject.FindProperty("_abortLandingTimer"), new GUIContent("Abort Timer"));
				}
				GUIEndBox();
			}
			GUIEndBox();

			GUIBeginBox();
			if (GUILayout.Button(GUIButtonText("Movement and Rotation", movementFold), EditorStyles.boldLabel))
				EditorPrefs.SetBool("UnluckSoftware.LS.Movement", !movementFold);
			if (movementFold)
			{
				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Rotation Logic", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_rotateAfterLanding"), new GUIContent("Rotate After Landing"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_randomRotate"), new GUIContent("Random Rotate"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_landedRotateSpeed"), new GUIContent("Rotate Speed"));
				GUIEndBox();

				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Speed Modifiers", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_soarLand"), new GUIContent("Soar On Approach"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_landingSpeedModifier"), new GUIContent("Landing Speed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_closeToSpotSpeedModifier"), new GUIContent("Final Approach Speed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_releaseSpeedModifier"), new GUIContent("Release Speed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_landingTurnSpeedModifier"), new GUIContent("Turn Speed"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_snapLandDistance"), new GUIContent("Snap Distance"));
				GUIEndBox();
			}
			GUIEndBox();

			GUIBeginBox();
			if (GUILayout.Button(GUIButtonText("Visuals and Gizmos", visualFold), EditorStyles.boldLabel))
				EditorPrefs.SetBool("UnluckSoftware.LS.Visuals", !visualFold);
			if (visualFold)
			{
				GUIBeginBox("", true, 2);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_featherPS"), new GUIContent("Feather Particles"));
				GUIEndBox();

				GUIBeginBox("", true, 2);
				EditorGUILayout.LabelField("Gizmos", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_drawGizmos"), new GUIContent("Draw Gizmos"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_showAllLandingSpotGizmos"), new GUIContent("Show All Child Gizmos"));
				EditorGUILayout.PropertyField(serializedObject.FindProperty("_gizmoSize"), new GUIContent("Gizmo Size"));
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
			}
			else
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