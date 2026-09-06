//	Unluck Software	
// 	www.chemicalbliss.com

using UnityEngine;
using UnityEditor;
namespace UnluckSoftware
{

	[CustomEditor(typeof(BakedMeshAnimator))]
	[CanEditMultipleObjects]
	[System.Serializable]
	public class BakedMeshAnimatorEditor : Editor
	{
		public override void OnInspectorGUI()
		{
			BakedMeshAnimator m_target = (BakedMeshAnimator)target;
			BakedMeshAnimatorUpdater updater = m_target.GetComponent<BakedMeshAnimatorUpdater>();

			DrawDefaultInspector();

			if (GUI.changed)
			{
				EditorUtility.SetDirty(m_target);
			}
			GUILayout.Space(15);


			if (m_target.crossfade)
			{
				EditorGUILayout.HelpBox("All meshes must have the same vert count for crossfade to function.", MessageType.Info);
			}



			if (!updater)
			{
				EditorGUILayout.HelpBox("BakedMeshAnimatorUpdater is needed to play animations.", MessageType.Info);
			}



		}
	}
}