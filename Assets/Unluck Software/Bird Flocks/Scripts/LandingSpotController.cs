/*
Copyright Unluck Software
www.chemicalbliss.com
*/

using UnityEngine;
using System.Collections;

namespace UnluckSoftware {

public class LandingSpotController : MonoBehaviour
{
	[Tooltip("Rotates the bird to the rotation of the landing spot")]
	public bool _rotateAfterLanding = true;
	[Tooltip("Random rotation when a bird lands")]
	public bool _randomRotate = true;
	[Tooltip("Speed of rotation after landing.")]
	public float _landedRotateSpeed = 2f;
	[Tooltip("Random Min/Max time for landing spot to make a bird land")]
	public Vector2 _autoCatchDelay = new Vector2(10.0f, 20.0f);
	[Tooltip("Random Min/Max time for birds to automaticly fly away from landing spot")]
	public Vector2 _autoDismountDelay = new Vector2(10.0f, 20.0f);
	[Tooltip("The maximum distance to a bird for it to land")]
	public float _maxBirdDistance = 20.0f;
	[Tooltip("The minimum distance to a bird for it to land")]
	public float _minBirdDistance = 5.0f;
	[Tooltip("Toggle this to make landingspots make the closest bird to it land")]
	public bool _takeClosest;
	[Tooltip("Assign the FlockController to pick birds from")]
	public FlockController _flock;
	[Tooltip("Put birds on the landing spots at start")]
	public bool _landOnStart;
	[Tooltip("Birds will soar while aproaching landing spot")]
	public bool _soarLand = true;
	[Tooltip("Only birds above landing spot will land")]
	public bool _onlyBirdsAbove;
	[Tooltip("Adjust bird movement speed while close to the landing spot")]
	public float _landingSpeedModifier = .5f;
	[Tooltip("Speed modifier at the very end of the landing sequence")]
	public float _closeToSpotSpeedModifier = 1f;
	[Tooltip("Adjust bird movement speed when leaving the landing spot")]
	public float _releaseSpeedModifier = 3f;

	[Tooltip("Turn speed modifier during landing approach.")]
	public float _landingTurnSpeedModifier = 5.0f;
	[Tooltip("Reference to the feather particle system transform")]
	public Transform _featherPS;
	[HideInInspector]
	public ParticleSystem _featherParticles;
	[HideInInspector]
	public Transform _transformCache;
	[HideInInspector]
	public int _activeLandingSpots;
	[Tooltip("Distance to snap to landing spot.")]
	public float _snapLandDistance = 0.05f;
	[Tooltip("Enable gizmos in the scene view.")]
	public bool _drawGizmos = true;
	[Tooltip("Show all child landing spot gizmos.")]
	public bool _showAllLandingSpotGizmos;

	[Tooltip("Scale of the gizmos.")]
	public float _gizmoSize = 0.2f;
	[Tooltip("Used in cases where landing spots moves, makes it easier for birds to land")]
	public bool _parentBirdToSpot;
	[Tooltip("Abort landing if bird gets stuck.")]
	public bool _abortLanding;
	[Range(1, 20)]
	[Tooltip("If birds have a tendency to get stuck while landing this can be used as a safety measure")]
	public float _abortLandingTimer = 10f;

	[Tooltip("Min delay for idle animation.")]
	public float idleAnimationDelayMin = 0.1f;
	[Tooltip("Max delay for idle animation.")]
	public float idleAnimationDelayMax = 0.75f;


	public void Start() {
		//_spots = _thisT.GetComponentsInChildren<LandingSpot>();
		if (_transformCache == null) _transformCache = transform;
		if (_flock == null) {
			_flock = (FlockController)GameObject.FindObjectOfType(typeof(FlockController));
			Debug.Log(this + " has no assigned FlockController, a random FlockController has been assigned");
		}

		if (_randomRotate && _parentBirdToSpot) {
			Debug.LogWarning(this + "\nEnabling random rotate and parent bird to spot is not yet available, disabling random rotate");
			_randomRotate = false;
		}

		//#if UNITY_EDITOR
		//if(_autoCatchDelay.x >0 &&(_autoCatchDelay.x < 5||_autoCatchDelay.y < 5)){
		//	Debug.Log(this.name + ": autoCatchDelay values set low, this might result in strange behaviours");
		//}
		//#endif

		if (_featherPS) {
			_featherParticles = _featherPS.GetComponent<ParticleSystem>();
		}

		if (_landOnStart) {
			StartCoroutine(InstantLandOnStart(0f));
		}
	}

	public void ScareAll() {
		ScareAll(0.0f, 1.0f);
	}

	public void ScareAll(float minDelay, float maxDelay) {
		for (int i = 0; i < _transformCache.childCount; i++) {
			if (_transformCache.GetChild(i).GetComponent<LandingSpot>() != null) {
				LandingSpot spot = _transformCache.GetChild(i).GetComponent<LandingSpot>();
				//	spot.Invoke("ReleaseFlockChild", Random.Range(minDelay, maxDelay));

				spot.ReleaseFlockChild();
			}
		}
	}

	public void LandAll() {
		for (int i = 0; i < _transformCache.childCount; i++) {
			if (_transformCache.GetChild(i).GetComponent<LandingSpot>() != null) {
				LandingSpot spot = _transformCache.GetChild(i).GetComponent<LandingSpot>();
				StartCoroutine(spot.GetFlockChild(0.0f, 2.0f));
			}
		}
	}

	//This function was added to fix a error with having a button calling InstantLand
	public IEnumerator InstantLandOnStart(float delay) {
		yield return new WaitForSeconds(delay);
		for (int i = 0; i < _transformCache.childCount; i++) {
			if (_transformCache.GetChild(i).GetComponent<LandingSpot>() != null) {
				LandingSpot spot = _transformCache.GetChild(i).GetComponent<LandingSpot>();
				spot.InstantLand();
			}
		}
	}

	public IEnumerator InstantLand(float delay) {
		yield return new WaitForSeconds(delay);
		for (int i = 0; i < _transformCache.childCount; i++) {
			if (_transformCache.GetChild(i).GetComponent<LandingSpot>() != null) {
				LandingSpot spot = _transformCache.GetChild(i).GetComponent<LandingSpot>();
				spot.InstantLand();
			}
		}
	}
}
}
