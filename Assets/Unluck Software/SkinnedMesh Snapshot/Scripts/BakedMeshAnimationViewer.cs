using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace UnluckSoftware
{
	[RequireComponent(typeof(BakedMeshAnimator))]
	public class BakedMeshAnimationViewer : MonoBehaviour
	{
		public float autoRandomTimer = 0f;
		private const float AutoPlayThreshold = 0.1f;
		private BakedMeshAnimator animator;

		void Start()
		{
			animator = GetComponent<BakedMeshAnimator>();
			if (autoRandomTimer > AutoPlayThreshold)
			{
				Invoke(nameof(RandomAnimation), autoRandomTimer);
			}
		}

		void RandomAnimation()
		{
			if (animator == null || animator.animations == null || animator.animations.Length == 0) return;
			animator.SetAnimation(Random.Range(0, animator.animations.Length));
			if (autoRandomTimer > AutoPlayThreshold)
			{
				Invoke(nameof(RandomAnimation), autoRandomTimer);
			}
		}
	}

#if UNITY_EDITOR
	[CustomEditor(typeof(BakedMeshAnimationViewer))]
	public class BakedMeshRandomAnimationEditor :Editor
	{
		private GUIStyle leftAlignStyle;

		public override void OnInspectorGUI()
		{
			BakedMeshAnimationViewer script = (BakedMeshAnimationViewer)target;
			DrawDefaultInspector();
			var animator = script.GetComponent<BakedMeshAnimator>();
			if (animator == null || animator.animations == null) return;
			if (leftAlignStyle == null)
			{
				leftAlignStyle = new GUIStyle(GUI.skin.button)
				{
					alignment = TextAnchor.MiddleLeft
				};
			}
			EditorGUILayout.Space();
			const float AutoPlayThreshold = 0.3f;
			EditorGUI.BeginDisabledGroup(script.autoRandomTimer > AutoPlayThreshold || !Application.isPlaying);
			for (int i = 0; i < animator.animations.Length; i++)
			{
				var anim = animator.animations[i];
				if (GUILayout.Button($"{i} - {anim.name}", leftAlignStyle))
				{
					animator.SetAnimation(i);
				}

				//if (GUILayout.Button($"{i} - {anim.name}", leftAlignStyle))
				//{
				//	animator.SetAnimation(i,0,false,false);
				//}
			}
			EditorGUI.EndDisabledGroup();
			if (GUI.changed)
			{
				EditorUtility.SetDirty(script);
			}
		}
	}
#endif
}