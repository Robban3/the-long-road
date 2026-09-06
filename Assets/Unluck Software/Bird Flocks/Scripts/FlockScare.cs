using UnityEngine;
using System.Collections;

namespace UnluckSoftware
{
	public class FlockScare : MonoBehaviour
	{
		[Tooltip("List of landingspot controllers containing landingspots to scare")]
		public LandingSpotController[] landingSpotControllers;
		[Tooltip("How often to check the next spot if it is close enough to scare (Every Nth second)")]
		public float scareInterval = 0.1f;
		[Tooltip("How far from a landingspot this transform can be to call the scare function")]
		public float distanceToScare = 2f;
		[Tooltip("Skip landingspots to move on to the next controller quicker (All spots will be included before the count restarts completely)")]
		public int checkEveryNthLandingSpot = 1;
		[Tooltip("How many instances of Invokes to run (Only go above 1 when more controllers than the Interval can handle)")]
		public int InvokeAmounts = 1;

		[Tooltip("If true, scares all birds under the controller; otherwise, only scares the close bird.")]
		public bool scareAll = true;

		private int currentControllerIndex;
		private int currentLandingSpotIndex;
		private Coroutine[] activeCoroutines;

		private void OnEnable()
		{
			StopAllScareRoutines();
			if (landingSpotControllers != null && landingSpotControllers.Length > 0)
			{
				StartScareRoutines();
			}
#if UNITY_EDITOR
			else
			{
				Debug.LogWarning("Please assign LandingSpotControllers to FlockScare", this);
			}
#endif
		}

		private void OnDisable()
		{
			StopAllScareRoutines();
		}

		private void StartScareRoutines()
		{
			if (InvokeAmounts <= 0) InvokeAmounts = 1;
			activeCoroutines = new Coroutine[InvokeAmounts];

			for (int i = 0; i < InvokeAmounts; i++)
			{
				float delayOffset = (scareInterval / InvokeAmounts) * i;
				activeCoroutines[i] = StartCoroutine(ScareRoutine(delayOffset));
			}
		}

		private void StopAllScareRoutines()
		{
			if (activeCoroutines != null)
			{
				for (int i = 0; i < activeCoroutines.Length; i++)
				{
					if (activeCoroutines[i] != null)
					{
						StopCoroutine(activeCoroutines[i]);
					}
				}
				activeCoroutines = null;
			}
		}

		private IEnumerator ScareRoutine(float initialDelay)
		{
			if (initialDelay > 0f)
			{
				yield return new WaitForSeconds(initialDelay);
			}

			WaitForSeconds wait = new WaitForSeconds(scareInterval);

			while (true)
			{
				CheckProximityToLandingSpots();
				yield return wait;
			}
		}

		private void CheckProximityToLandingSpots()
		{
			if (landingSpotControllers == null || landingSpotControllers.Length == 0) return;

			if (currentControllerIndex < 0 || currentControllerIndex >= landingSpotControllers.Length)
			{
				currentControllerIndex = 0;
			}

			LandingSpotController controller = landingSpotControllers[currentControllerIndex];
			if (controller == null)
			{
				currentControllerIndex = (currentControllerIndex + 1) % landingSpotControllers.Length;
				currentLandingSpotIndex = 0;
				return;
			}

			int childCount = controller.transform.childCount;
			if (childCount == 0)
			{
				currentControllerIndex = (currentControllerIndex + 1) % landingSpotControllers.Length;
				currentLandingSpotIndex = 0;
				return;
			}

			if (currentLandingSpotIndex < 0 || currentLandingSpotIndex >= childCount)
			{
				currentLandingSpotIndex = 0;
			}

			Transform spotTransform = controller.transform.GetChild(currentLandingSpotIndex);
			if (spotTransform != null)
			{
				LandingSpot spot = spotTransform.GetComponent<LandingSpot>();
				if (spot != null && spot._landingChild != null)
				{
					float distanceSqr = (spotTransform.position - transform.position).sqrMagnitude;
					if (distanceSqr < distanceToScare * distanceToScare)
					{
						if (scareAll)
						{
							controller.ScareAll();
						}
						else
						{
							spot.ReleaseFlockChild();
						}
					}
				}
			}

			currentLandingSpotIndex += checkEveryNthLandingSpot;
			if (currentLandingSpotIndex >= childCount)
			{
				currentLandingSpotIndex = 0;
				currentControllerIndex = (currentControllerIndex + 1) % landingSpotControllers.Length;
			}
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(transform.position, distanceToScare);
			if (InvokeAmounts <= 0) InvokeAmounts = 1;
		}
#endif
	}
}