using System.Collections.Generic;
using UnityEngine;
namespace UnluckSoftware
{
	public class BakedMeshAnimator : MonoBehaviour
	{
		public MeshRenderer animationMeshRenderer;
		public BakedMeshAnimation[] animations;
		public int startAnimation = 0;
		int currentAnimation = 0;
		MeshFilter meshFilter;
		public float currentFrame;
		int currentFrameInt;
		float currentSpeed;
		bool pingPongToggle;
		public float playSpeedMultiplier = 1f;
		int meshCacheCount;
		public float transitionFailsafe = 0.4f;
		float failsafe;
		int transitionFrame;
		int anim;
		bool transitioning = true;
		int lastRequestedAnimation = -1;

		public bool crossfade;
		public bool crossfadeNormalFix;
		public float crossfadeFrequency = 0.05f;
		public float crossfadeWeightAdd = 0.221f;

		public enum EasingFunction
		{
			None,
			EaseOutCubic,
			EaseInCubic,
			EaseInOutCubic,
			EaseInQuad,
			EaseOutQuad
		}
		public EasingFunction selectedEasingFunction = EasingFunction.EaseOutCubic;

		bool doCrossfade;
		float crossfadeWeight = 1f;
		Mesh crossfadeMeshEnd;

		List<Vector3> vertsStartList = new List<Vector3>();
		List<Vector3> normsStartList = new List<Vector3>();
		List<Vector3> vertsTargetList = new List<Vector3>();
		List<Vector3> normsTargetList = new List<Vector3>();
		List<Vector3> vertsCurrentList = new List<Vector3>();
		List<Vector3> normsCurrentList = new List<Vector3>();

		float nextUpdate;
		bool isFirstCrossfadeFrame = false;

		int crossfadeStartAnimation;
		float crossfadeStartFrame;
		float crossfadeStartSpeed;

		void Awake()
		{
			currentAnimation = Mathf.Clamp(startAnimation, 0, animations.Length - 1);
			if (animationMeshRenderer == null) animationMeshRenderer = GetComponent<MeshRenderer>();
			if (animationMeshRenderer == null)
			{
				Debug.LogError("BakedMeshAnimator: " + this + " has no assigned MeshRenderer!");
			}
			meshFilter = animationMeshRenderer.GetComponent<MeshFilter>();
			meshCacheCount = animations[currentAnimation].meshes.Length;
			currentSpeed = animations[currentAnimation].playSpeed;
			CreateCrossFadeMesh();

#if UNITY_EDITOR
		if (animations.Length == 0)
			Debug.LogWarning(this.gameObject.name + " has no animations attached");
#endif
		}

		public void AnimateUpdate()
		{
			if (animations.Length == 0) return;
			Animate();
		}

		public void SetAnimation(int _animation, bool forceAnimation = false)
		{
			if (!forceAnimation && (_animation == currentAnimation || _animation == lastRequestedAnimation || transitioning)) return;

			lastRequestedAnimation = _animation;
			transitionFrame = animations[currentAnimation].transitionFrame;
			anim = _animation;
			this.enabled = true;
			transitioning = true;
			StartCrossfade();
		}

		public void SetAnimation(string _animationName, bool forceAnimation = false)
		{
			int _animationIndex = GetAnimationIndexByName(_animationName);
			if (_animationIndex == -1) return;
			SetAnimation(_animationIndex, forceAnimation);
		}

		private int GetAnimationIndexByName(string _animationName)
		{
			for (int i = 0; i < animations.Length; i++)
			{
				if (animations[i].name == _animationName)
				{
					return i;
				}
			}
			return -1;
		}

		private void Animate()
		{
			if (!animationMeshRenderer.isVisible) return;

			if (transitioning)
			{
				if (crossfade || (int)currentFrame == transitionFrame || failsafe > transitionFailsafe)
				{
					failsafe = 0;
					transitioning = false;
					currentAnimation = anim;
					lastRequestedAnimation = -1;
					meshCacheCount = animations[currentAnimation].meshes.Length;
					currentSpeed = animations[currentAnimation].playSpeed;
					if (Time.time < 1f && animations[currentAnimation].randomStartFrame)
						currentFrame = Random.Range(0, meshCacheCount);
					else
						currentFrame = animations[currentAnimation].transitionFrame;
				}
				else
				{
					failsafe += Time.deltaTime;
				}
			}

			if (animations[currentAnimation].pingPong) PingPongFrame();
			else NextFrame();

			if (currentFrameInt != (int)currentFrame)
			{
				currentFrameInt = (int)currentFrame;
				if (!doCrossfade)
					meshFilter.sharedMesh = animations[currentAnimation].meshes[currentFrameInt];
			}

			UpdateCrossfade();
		}

		public bool NextFrame()
		{
			currentFrame += currentSpeed * Time.deltaTime * playSpeedMultiplier;
			if (currentFrame > meshCacheCount + 1)
			{
				currentFrame = 0.0f;
				if (!animations[currentAnimation].loop) this.enabled = false;
				return true;
			}
			if (currentFrame >= meshCacheCount)
			{
				currentFrame = meshCacheCount - currentFrame;
				if (!animations[currentAnimation].loop) this.enabled = false;
				return true;
			}
			return false;
		}

		public bool PingPongFrame()
		{
			if (pingPongToggle)
				currentFrame += currentSpeed * Time.deltaTime * playSpeedMultiplier;
			else
				currentFrame -= currentSpeed * Time.deltaTime * playSpeedMultiplier;

			if (currentFrame <= 0)
			{
				currentFrame = 0.0f;
				pingPongToggle = true;
				return true;
			}
			if (currentFrame >= meshCacheCount)
			{
				pingPongToggle = false;
				currentFrame = meshCacheCount - 1;
				return true;
			}
			return false;
		}

		public void SetSpeedMultiplier(float speed)
		{
			playSpeedMultiplier = speed;
		}

		void CreateCrossFadeMesh()
		{
			if (!crossfade) return;
			if (animations.Length == 0 || animations[0].meshes.Length == 0) return;
			Mesh baseMesh = animations[0].meshes[0];

			if (crossfadeMeshEnd == null)
			{
				crossfadeMeshEnd = new Mesh();
				crossfadeMeshEnd.MarkDynamic();

				baseMesh.GetVertices(vertsStartList);
				baseMesh.GetNormals(normsStartList);

				crossfadeMeshEnd.SetVertices(vertsStartList);
				crossfadeMeshEnd.triangles = baseMesh.triangles;
				crossfadeMeshEnd.uv = baseMesh.uv;
				crossfadeMeshEnd.SetNormals(normsStartList);
			}
		}

		void StartCrossfade()
		{
			if (!crossfade) return;

			crossfadeStartAnimation = currentAnimation;
			crossfadeStartFrame = currentFrame;
			crossfadeStartSpeed = animations[crossfadeStartAnimation].playSpeed;

			doCrossfade = true;
			isFirstCrossfadeFrame = true;
			crossfadeWeight = 0f;

			if (meshFilter.mesh != crossfadeMeshEnd)
				meshFilter.mesh = crossfadeMeshEnd;

			int currentFrameIndex = Mathf.Clamp(Mathf.RoundToInt(currentFrame), 0, animations[currentAnimation].meshes.Length - 1);
			Mesh currentMesh = animations[currentAnimation].meshes[currentFrameIndex];

			vertsStartList.Clear();
			normsStartList.Clear();
			currentMesh.GetVertices(vertsStartList);
			currentMesh.GetNormals(normsStartList);

			int vertexCount = vertsStartList.Count;
			while (vertsCurrentList.Count < vertexCount) vertsCurrentList.Add(Vector3.zero);
			while (normsCurrentList.Count < vertexCount) normsCurrentList.Add(Vector3.zero);

			nextUpdate = 0f;
			UpdateCrossfade();
		}

		void UpdateCrossfade()
		{
			if (!crossfade || !doCrossfade) return;
			nextUpdate += Time.deltaTime;
			if (!isFirstCrossfadeFrame && nextUpdate < crossfadeFrequency) return;
			nextUpdate = 0f;
			isFirstCrossfadeFrame = false;

			if (crossfadeWeight >= 1f)
			{
				doCrossfade = false;
				return;
			}

			crossfadeStartFrame += crossfadeStartSpeed * Time.deltaTime * playSpeedMultiplier;

			int targetFrameIndex = Mathf.Clamp(Mathf.RoundToInt(currentFrame), 0, animations[currentAnimation].meshes.Length - 1);
			Mesh endMesh = animations[currentAnimation].meshes[targetFrameIndex];

			vertsTargetList.Clear();
			normsTargetList.Clear();
			endMesh.GetVertices(vertsTargetList);
			endMesh.GetNormals(normsTargetList);

			int count = vertsStartList.Count;
			if (count != vertsTargetList.Count)
			{
				Debug.LogWarning("Crossfade vertex count mismatch.");
				return;
			}

			float easedWeight = GetEasedWeight(crossfadeWeight);
			bool processNormals = normsStartList.Count == count && normsTargetList.Count == count;

			for (int i = 0; i < count; i++)
			{
				Vector3 startV = vertsStartList[i];
				Vector3 targetV = vertsTargetList[i];
				vertsCurrentList[i] = new Vector3(
					startV.x + (targetV.x - startV.x) * easedWeight,
					startV.y + (targetV.y - startV.y) * easedWeight,
					startV.z + (targetV.z - startV.z) * easedWeight
				);

				if (processNormals)
				{
					if (crossfadeNormalFix)
					{
						Vector3 startN = normsStartList[i];
						Vector3 targetN = normsTargetList[i];
						normsCurrentList[i] = new Vector3(
							startN.x + (targetN.x - startN.x) * easedWeight,
							startN.y + (targetN.y - startN.y) * easedWeight,
							startN.z + (targetN.z - startN.z) * easedWeight
						);
					}
					else
					{
						normsCurrentList[i] = normsTargetList[i];
					}
				}
			}

			crossfadeMeshEnd.SetVertices(vertsCurrentList);
			if (processNormals)
			{
				crossfadeMeshEnd.SetNormals(normsCurrentList);
			}

			crossfadeWeight += crossfadeWeightAdd;
		}

		float GetEasedWeight(float t)
		{
			switch (selectedEasingFunction)
			{
				case EasingFunction.None: return Mathf.SmoothStep(0, 1, t);
				case EasingFunction.EaseInCubic: return t * t * t;
				case EasingFunction.EaseOutCubic: return 1f - Mathf.Pow(1f - t, 3f);
				case EasingFunction.EaseInOutCubic:
					return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
				case EasingFunction.EaseInQuad: return t * t;
				case EasingFunction.EaseOutQuad: return t * (2f - t);
				default: return t;
			}
		}
	}
}