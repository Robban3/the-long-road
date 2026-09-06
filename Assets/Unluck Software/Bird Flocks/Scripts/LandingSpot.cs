/*
Copyright Unluck Software
www.chemicalbliss.com
*/

using UnityEngine;
using System.Collections;

namespace UnluckSoftware
{

	public class LandingSpot : MonoBehaviour
	{
		[HideInInspector] public FlockChild _landingChild;
		[HideInInspector] public bool _landing;
		[Tooltip("Reference to the parent LandingSpotController")]
		public LandingSpotController _controller;
		[Tooltip("Cached transform reference for performance")]
		public Transform _transformCache;
		[Tooltip("Waypoint the bird steers toward before performing the final landing approach")]
		public Vector3 _preLandWaypoint;


		private Vector3 _cachePreLandingWaypoint;
		private float _distance;

		private const float FeatherEmitChance = 3f;
		private const float RandomRotateMax = 360f;
		private const float WaypointUpOffset = 3f;
		private const float FlapDelay = 0.1f;
		private bool _landed;

		private void Start()
		{
			if (_transformCache == null) _transformCache = transform;
			_cachePreLandingWaypoint = _preLandWaypoint;
			if (_controller == null) _controller = _transformCache.parent.GetComponent<LandingSpotController>();

			if (_controller._autoCatchDelay.x > 0)
				StartCoroutine(GetFlockChild(_controller._autoCatchDelay.x, _controller._autoCatchDelay.y));

			RandomRotate();
		}

#if UNITY_EDITOR
		private void OnDrawGizmos()
		{
			if (_transformCache == null) _transformCache = transform;
			if (_controller == null) _controller = _transformCache.parent.GetComponent<LandingSpotController>();
			if (!_controller._drawGizmos || _landed) return;

			if (!_landing) Gizmos.color = Color.yellow;
			else Gizmos.color = Color.cyan;
			if (_landingChild != null && _landing)
				Gizmos.DrawLine(_transformCache.position, _landingChild._thisT.position);

			Gizmos.DrawCube(_transformCache.position, Vector3.one * _controller._gizmoSize);
			Gizmos.color = Color.blue;
			Gizmos.DrawCube(_transformCache.position + (_transformCache.forward * _controller._gizmoSize), Vector3.one * _controller._gizmoSize * 0.25f);
			Gizmos.color = Color.green;
			Gizmos.DrawCube(_transformCache.position + (_transformCache.up * _controller._gizmoSize), Vector3.one * _controller._gizmoSize * 0.25f);

			if (_preLandWaypoint != Vector3.zero)
			{
				Gizmos.color = Color.blue;
				Gizmos.DrawCube(_transformCache.position + _preLandWaypoint, Vector3.one * _controller._gizmoSize * 0.25f);
			}

			if (!_controller._showAllLandingSpotGizmos && _transformCache.parent.GetChild(0) != _transformCache) return;

			Gizmos.color = new Color(1f, 1f, 0f, 1f);
			Gizmos.DrawWireSphere(_transformCache.position, _controller._maxBirdDistance);
			Gizmos.color = new Color(1f, 0f, 0f, 1f);
			Gizmos.DrawWireSphere(_transformCache.position, _controller._minBirdDistance);
		}
#endif

		private bool IsValidLandingCandidate(FlockChild child)
		{
			if (child == null || child._landing || !child._canLand || !child.gameObject.activeInHierarchy) return false;
			Vector3 pos = child._thisT.position;
			float sqrDist = (pos - _transformCache.position).sqrMagnitude;
			if (_controller._onlyBirdsAbove && pos.y <= _transformCache.position.y) return false;
			return sqrDist < (_controller._maxBirdDistance * _controller._maxBirdDistance) &&
				   sqrDist > (_controller._minBirdDistance * _controller._minBirdDistance);
		}

		public IEnumerator GetFlockChild(float minDelay, float maxDelay)
		{
			yield return new WaitForSeconds(Random.Range(minDelay, maxDelay));
			if (!_controller._flock.gameObject.activeInHierarchy || _landingChild != null) yield break;

			FlockChild foundChild = null;
			foreach (var child in _controller._flock._roamers)
			{
				if (child == null) continue;
				if (!IsValidLandingCandidate(child)) continue;
				float d = (child._thisT.position - _transformCache.position).sqrMagnitude;
				if (foundChild == null || (_controller._takeClosest && d < _distance))
				{
					foundChild = child;
					_distance = d;
				}
				if (!_controller._takeClosest) break;
			}

			if (foundChild != null)
			{
				if (_controller._abortLanding)
					Invoke(nameof(AbortLanding), _controller._abortLandingTimer);
				AssignFlockChild(foundChild);
			}
			else if (_controller._autoCatchDelay.x > 0)
			{
				StartCoroutine(GetFlockChild(_controller._autoCatchDelay.x, _controller._autoCatchDelay.y));
			}
		}

		public void InstantLand()
		{
			if (!_controller._flock.gameObject.activeInHierarchy || _landingChild != null) return;

			foreach (var child in _controller._flock._roamers)
			{
				if (!child._landing)
				{
					AssignFlockChild(child);
					_landed = true;
					child._move = false;
					child._speed = 0;
					child._targetSpeed = 0;
					child._thisT.position = child.GetLandingSpotPosition();
					child._thisT.rotation = _transformCache.rotation;
					child._wayPoint = _transformCache.position;
					if (!child._animationIsBaked)
					{
						if (!child._animator) child._modelAnimation.Play(child._spawner._idleAnimation);
						else child._animator.Play(child._spawner._idleAnimation);
					}
					else child._bakedAnimator.SetAnimation(2, true);
					break;
				}
			}

			if (_controller._autoDismountDelay.x > 0)
				Invoke(nameof(ReleaseFlockChild), Random.Range(_controller._autoDismountDelay.x, _controller._autoDismountDelay.y));
		}

		private void AssignFlockChild(FlockChild child)
		{
			_landingChild = child;
			_landing = true;
			_landingChild._landing = true;
			_landingChild._landingSpot = this;
			_controller._activeLandingSpots++;

			if (_controller._parentBirdToSpot)
				_landingChild.transform.SetParent(transform);

			RandomRotate();
		}

		private void AbortLanding()
		{
			CancelInvoke(nameof(AbortLanding));
			if (_landingChild == null)
			{
				if (_landing)
				{
					_controller._activeLandingSpots--;
				}
				_landing = false;
				return;
			}

			if (_controller._parentBirdToSpot)
				_landingChild.transform.SetParent(_landingChild._childParent);

			_landing = false;
			_landingChild._landing = false;
			_landingChild._landingSpot = null;
			_controller._activeLandingSpots--;
			_landingChild.currentAnim = string.Empty;
			_landingChild.Flap(FlapDelay, false);
			_landingChild.PauseLanding(5f);
			_landingChild = null;
		}

		public void Landed()
		{
			_landed = true;
			CancelInvoke(nameof(AbortLanding));
			CancelInvoke(nameof(ReleaseFlockChild));
			if (_controller._autoDismountDelay.x > 0)
				Invoke(nameof(ReleaseFlockChild), Random.Range(_controller._autoDismountDelay.x, _controller._autoDismountDelay.y));
		}

		public void ReleaseFlockChild()
		{
			_landed = false;
			if (!_controller._flock.gameObject.activeInHierarchy || _landingChild == null) return;
			//CancelInvoke(nameof(ReleaseFlockChild));
			_preLandWaypoint = _cachePreLandingWaypoint;
			EmitFeathers();
			_landingChild._modelT.localEulerAngles = Vector3.zero;
			_landing = false;
			_landingChild.Invoke("EnableVerticalAvoidance", 0.5f);

			_landingChild._speed = 0f;
			_landingChild._acceleration = .5f;
			_landingChild.Invoke("InitAcceleration", .5f);

			_landingChild._closeToSpot = false;
			_landingChild._damping = _landingChild._spawner._maxDamping;
			_landingChild._avoidVert = false;
			_landingChild._targetSpeed = _landingChild._spawner._minSpeed;
			_landingChild._move = true;
			_landingChild._landing = false;
			_landingChild.currentAnim = string.Empty;
			_landingChild.CrossfadeDismount();
			_landingChild.Flap(FlapDelay, false);
			_landingChild._wayPoint = _transformCache.position + (_transformCache.up * 1.5f) + (_transformCache.forward * 1.0f);
			_landingChild.PauseLanding(5f);
			if (_controller._parentBirdToSpot)
				_landingChild._spawner.AddChildToParent(_landingChild._thisT);

			if (_controller._autoCatchDelay.x > 0)
				StartCoroutine(GetFlockChild(_controller._autoCatchDelay.x + 0.1f, _controller._autoCatchDelay.y + 0.1f));
			_controller._activeLandingSpots--;
			_landingChild = null;
		}



		private void RandomRotate()
		{
			if (_controller._randomRotate)
				transform.Rotate(transform.up * Random.Range(0f, RandomRotateMax), Space.World);
		}

		private void EmitFeathers()
		{
			if (_controller._featherPS == null) return;
			_controller._featherPS.position = _landingChild._thisT.position;
			_controller._featherParticles.Emit(Random.Range(0, (int)FeatherEmitChance));
		}
	}
}