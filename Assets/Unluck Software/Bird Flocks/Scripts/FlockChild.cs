/*
Copyright Unluck Software
www.chemicalbliss.com
*/

using UnityEngine;

namespace UnluckSoftware
{

	public class FlockChild : MonoBehaviour
	{
		[HideInInspector]
		public FlockController _spawner;        //Reference to the flock controller that spawned this bird
		[HideInInspector]
		public Vector3 _wayPoint;               //Waypoint used to steer towards
		[HideInInspector]
		public float _speed;                    //Current speed of bird
		[HideInInspector]
		public bool _dived = true;              //Indicates if this bird has recently performed a dive movement
												
		[HideInInspector]
		public float _damping;                  //Damping used for steering (steer speed)
		[HideInInspector]
		public bool _soar = true;               // Indicates if this is soaring
		[HideInInspector]
		public bool _landing;                   // Indicates if bird is landing or sitting idle
		[HideInInspector]
		public bool _landed;                   // Indicates if bird is landing or sitting idle
		[HideInInspector]
		public LandingSpot _landingSpot;
		[HideInInspector]
		public float _targetSpeed;              // Max bird speed
		[HideInInspector]
		public bool _move = true;               // Indicates if bird can fly
		public GameObject _model;               // Reference to bird model
		public Transform _modelT;               // Reference to bird model transform (caching tranform to avoid any extra getComponent calls)
		[HideInInspector]
		public float _avoidValue;               //Random value used to check for obstacles. Randomized to lessen uniformed behaviour when avoiding
		[HideInInspector]
		public float _avoidDistance;            //How far from an obstacle this can be before starting to avoid it
		float _soarTimer;
		float _stuckTimer;
		bool _instantiated;
		static int _updateNextSeed = 0;
		int _updateSeed = -1;
		[HideInInspector]
		public bool _avoid = true;
		[HideInInspector]
		public bool _avoidVert = true;
		public Transform _thisT;                //Reference to the transform component
		public Vector3 _landingPosOffset;
		[HideInInspector]
		public bool _animationIsBaked;
		public BakedMeshAnimator _bakedAnimator;
		public Animation _modelAnimation;
		public Animator _animator;
		[HideInInspector]
		public float _acceleration = 20f;
		[HideInInspector]
		public string currentAnim;
		[HideInInspector]
		public bool _closeToSpot;
		float distance;
		bool _avoiding;
		bool _dismounting;
		[HideInInspector]
		public bool _canLand = true;
		AnimationState flapState;
		AnimationState soarState;
		AnimationState idleState;
		public Vector3 _landingOffsetFix = new Vector3(0f, .1f, 0f); // Adjust this when using birds with different sizes so that the feet touch the ground.

		public Transform _childParent;

		bool canFindWaypoint = true;

#if UNITY_EDITOR
		public void OnDrawGizmos()
		{
			if (_landing) return;
			Gizmos.color = new Color(.3f, .3f, .3f);
			Gizmos.DrawWireSphere(_wayPoint, .05f);
			Gizmos.DrawLine(_thisT.position, _wayPoint);
		}
#endif

		public void Start()
		{
			FindRequiredComponents();           //Check if references to transform and model are set (These should be set in the prefab to avoid doind this once a bird is spawned, click "Fill" button in prefab)
			if (_spawner == null)
			{
				this.enabled = false;
				return;
			}
			SetRandomScale();
			FindWaypoint();
			_thisT.position = _wayPoint;
			Wander();
			RandomizeStartAnimationFrame();
			InitAvoidanceValues();
			InitAcceleration();
			_stuckTimer = Random.Range(0f, 30f);
			_speed = _spawner._minSpeed;
			_spawner._activeChildren++;
			_instantiated = true;
			if (_spawner._skipFrame)
			{
				_updateSeed = _updateNextSeed % 2;
				_updateNextSeed++;
			}
			_childParent = _thisT.parent;
		}

		public void InitAcceleration()
		{
			_acceleration = Random.Range(_spawner._minAcceleration, _spawner._maxAcceleration);
		}

		public void PauseLanding(float delay)
		{
			_canLand = false;
			CancelInvoke(nameof(PauseLanding));
			Invoke(nameof(ResumeLanding), delay);
		}

		public void ResumeLanding()
		{
			_canLand = true;
		}

		public void BirdUpdate()
		{
			if (!_instantiated) return;
			//Skip frames
			if (!_spawner._skipFrame || _spawner._updateCounter == _updateSeed)
			{
				Vector3 landingPos = _landing ? GetLandingSpotPosition() : Vector3.zero;

				if (_spawner.LimitPitchRotation || _spawner._birdRoll || _landing) LimitRotationOfModel(landingPos);
				if (!_landing)
				{
					SoarTimeLimit();
					CheckForDistanceToWaypoint();
					RotationBasedOnWaypointOrAvoidance();
					Move();
				}
				else
				{
					if (!_move)
					{
						RotateBird();
						if (!_bakedAnimator) return;
						if (_bakedAnimator.isActiveAndEnabled) _bakedAnimator.AnimateUpdate();
						return;
					}
					float sqrDist = (landingPos - _thisT.position).sqrMagnitude;
					if (sqrDist > 25f)
					{
						_wayPoint = landingPos + _landingSpot._preLandWaypoint;
						if (_landingSpot._preLandWaypoint.sqrMagnitude > 0 && (_wayPoint - _thisT.position).sqrMagnitude < 1f)
						{
							_wayPoint = GetLandingSpotPosition();
							_landingSpot._preLandWaypoint = Vector3.zero;
						}
						//_avoid = true;
					}
					else
					{
						_wayPoint = _landingSpot._transformCache.position + _landingPosOffset + (_landingOffsetFix * _thisT.localScale.y);
						//	_avoid = false;
					}
					_damping = _landingSpot._controller._landingTurnSpeedModifier;
					RotationBasedOnWaypointOrAvoidance();
					Move();
					Landing(landingPos);
			}

			_stuckTimer += _spawner._newDelta;
			if (_stuckTimer > 30f)
			{
				_stuckTimer = Random.Range(0f, 5f);
				FindWaypoint(true);
				}
			}
			if (!_bakedAnimator) return;
			if (_bakedAnimator.isActiveAndEnabled) _bakedAnimator.AnimateUpdate();
		}

		void Landing(Vector3 landingPos)
		{
			if (currentAnim == "Idle") return;
			float sqrDist = (landingPos - _thisT.position).sqrMagnitude;
			//Start landing if distance is close enough
			if (sqrDist < 64f && sqrDist >= 1f)
			{
				if (sqrDist < 16f)
				{
					if (currentAnim != "Flap")
					{
						if (!_animationIsBaked)
						{
							if (!_animator) _modelAnimation.CrossFade(_spawner._flapAnimation, .5f);
							else _animator.CrossFade(_spawner._flapAnimation, .5f);
						}
						else
						{
							_bakedAnimator.SetAnimation(0, true);
							_bakedAnimator.SetSpeedMultiplier(_spawner._maxAnimationSpeed);
						}
						currentAnim = "Flap";
						AnimationSpeed();
					}
				}
				else if (_landingSpot._controller._soarLand)
				{
					if (currentAnim != "Soar")
					{
						if (!_animationIsBaked)
						{
							if (!_animator) _modelAnimation.CrossFade(_spawner._soarAnimation, .5f);
							else _animator.CrossFade(_spawner._soarAnimation, .5f);
						}
						else
						{
							_bakedAnimator.SetAnimation(1);
							_bakedAnimator.SetSpeedMultiplier(_spawner._minAnimationSpeed);
						}
						currentAnim = "Soar";
						AnimationSpeed();
					}
				}
				else if (currentAnim != "Flap")
				{
					if (!_animationIsBaked)
					{
						if (!_animator) _modelAnimation.CrossFade(_spawner._flapAnimation, .5f);
						else _animator.CrossFade(_spawner._flapAnimation, .5f);
					}
					else
					{
						_bakedAnimator.SetAnimation(0, true);
						_bakedAnimator.SetSpeedMultiplier(_spawner._maxAnimationSpeed);
					}
					currentAnim = "Flap";
					AnimationSpeed();
				}


				_targetSpeed = _spawner._maxSpeed * _landingSpot._controller._landingSpeedModifier;
			}
			else if (sqrDist < 1f)
			{
				distance = Mathf.Sqrt(sqrDist);
				Vector3 dir = (landingPos - _thisT.position).normalized;
				float moveAmt = _spawner._newDelta * _landingSpot._controller._closeToSpotSpeedModifier * _targetSpeed;
				if (moveAmt > distance)
				{
					_thisT.position = landingPos;
				}
				else
				{
					_thisT.position += dir * moveAmt;
				}
				_closeToSpot = true;
				if (distance <= _landingSpot._controller._snapLandDistance)
				{
					if (currentAnim != "Idle")
					{
						Invoke("Idle", Random.Range(_landingSpot._controller.idleAnimationDelayMin, _landingSpot._controller.idleAnimationDelayMax));   // Delay idle animation so landing looks more natural
						_landingSpot.Landed();

						currentAnim = "Idle";
					}
					_move = false;
					_thisT.position = GetLandingSpotPosition();
					_modelT.localRotation = Quaternion.identity;

					_thisT.eulerAngles = new Vector3(0, _thisT.rotation.eulerAngles.y, 0);
					_damping = .75f;
				}
				else
				{
					_speed = _spawner._minSpeed * 0.2f;
				}
			}
		}

		void Idle()
		{
			if (currentAnim != "Idle") return;
			if (!_animationIsBaked)
			{
				if (!_animator) _modelAnimation.CrossFade(_spawner._idleAnimation, .5f);
				else _animator.CrossFade(_spawner._idleAnimation, .5f);
			}
			else _bakedAnimator.SetAnimation(2);
			AnimationSpeed();
		}

		public Vector3 GetLandingSpotPosition()
		{
			return _landingSpot._transformCache.position + _landingPosOffset + _landingOffsetFix * _thisT.localScale.y;
		}

		public void RotateBird()
		{
			if (!_landingSpot._controller._rotateAfterLanding) return;
			Vector3 targetRotE = _landingSpot._transformCache.rotation.eulerAngles;
			Vector3 currentRotE = _thisT.rotation.eulerAngles;
			Quaternion rot = _thisT.rotation;
			Vector3 rotE;
			rotE.x = Mathf.LerpAngle(currentRotE.x, targetRotE.x, _landingSpot._controller._landedRotateSpeed * _spawner._newDelta);
			rotE.z = Mathf.LerpAngle(currentRotE.z, targetRotE.z, _landingSpot._controller._landedRotateSpeed * _spawner._newDelta);
			rotE.y = Mathf.LerpAngle(currentRotE.y, targetRotE.y, _landingSpot._controller._landedRotateSpeed * _spawner._newDelta);
			rot.eulerAngles = rotE;
			_thisT.rotation = rot;
		}

		public void OnDisable()
		{
			CancelInvoke();
			if (_spawner)
				_spawner._activeChildren--;
		}

		public void OnEnable()
		{
			if (_instantiated)
			{
				_spawner._activeChildren++;
				if (_animationIsBaked)
				{
					if (_landing)
					{
						_bakedAnimator.SetAnimation(2, true);
					}
					else
					{
						_bakedAnimator.SetAnimation(0, true);
					}
					return;
				}
				if (_landing)
				{
					_modelAnimation.Play(_spawner._idleAnimation);
				}
				else
				{
					_modelAnimation.Play(_spawner._flapAnimation);
				}
			}
		}

		public void EnableVerticalAvoidance()
		{
			_avoidVert = true;
		}

		public void FindRequiredComponents()
		{
			if (_thisT == null) _thisT = transform;
			if (_model == null) _model = _thisT.Find("Model").gameObject;
			if (_modelT == null) _modelT = _model.transform;
			//Find what type of animation is used
			if (_bakedAnimator != null)
			{
				_animationIsBaked = true;
				return;
			}
			if (_modelAnimation != null) return;
			if (_animator != null) return;
			_modelAnimation = _model.GetComponent<Animation>();
			if (!_modelAnimation) _animator = _model.GetComponent<Animator>();
			if (!_modelAnimation && !_animator) _animationIsBaked = true;
			else _animationIsBaked = false;
			if (_bakedAnimator == null) _bakedAnimator = GetComponent<BakedMeshAnimator>();
			if (_bakedAnimator == null) _bakedAnimator = _model.GetComponent<BakedMeshAnimator>();
		}

		public void RandomizeStartAnimationFrame()
		{
			if (_animationIsBaked || _animator) return;
			foreach (AnimationState state in _modelAnimation)
			{
				state.time = Random.value * state.length;
			}
		}

		public void InitAvoidanceValues()
		{
			_avoidValue = Random.Range(.3f, .1f);
			if (_spawner._birdAvoidDistanceMax != _spawner._birdAvoidDistanceMin)
			{
				_avoidDistance = Random.Range(_spawner._birdAvoidDistanceMax, _spawner._birdAvoidDistanceMin);
				return;
			}
			_avoidDistance = _spawner._birdAvoidDistanceMin;
		}

		public void SetRandomScale()
		{
			float sc = Random.Range(_spawner._minScale, _spawner._maxScale);
			_thisT.localScale = new Vector3(sc, sc, sc);
		}
		//Soar Timeout - Limits how long a bird can soar
		public void SoarTimeLimit()
		{
			if (this._soar && _spawner._soarMaxTime > 0)
			{
				//Debug.Log(_soarTimer + "     " + _spawner._soarMaxTime);
				if (_soarTimer > _spawner._soarMaxTime)
				{
					this.Flap();
					_soarTimer = _spawner._soarMaxTime * Random.Range(-0.5f, 0.5f);
				}
				_soarTimer += _spawner._newDelta;
			}
		}

		public void CheckForDistanceToWaypoint()
		{
			if (!_landing && (_thisT.position - _wayPoint).sqrMagnitude < (_spawner._waypointDistance * _spawner._waypointDistance))
			{
				Wander();
			}
		}

		public void RotationBasedOnWaypointOrAvoidance()
		{
			if (_avoiding)
			{
				return;
			}
			Vector3 lookit = _wayPoint - _thisT.position;
			if (_targetSpeed > -1 && lookit != Vector3.zero)
			{
				Quaternion rotation = Quaternion.LookRotation(lookit);
				_thisT.rotation = Quaternion.Slerp(_thisT.rotation, rotation, _spawner._newDelta * _damping);
			}
			if (_spawner._childTriggerPos)
			{
				if ((_thisT.position - _spawner._posBuffer).sqrMagnitude < 1f)
				{
					_spawner.SetFlockRandomPosition();
				}
			}
		}

		void Move()
		{
			//Position forward based on object rotation
			if (_move)
			{
				ChangeSpeed();
				if (!_closeToSpot) _thisT.position += _thisT.forward * _speed * _spawner._newDelta;
				if (Avoidance() && !_avoiding)
				{
					_avoiding = true;
					Invoke("AvoidNoLonger", Random.Range(.25f, .5f));
				}
			}
		}

		void AvoidNoLonger()
		{
			_avoiding = false;
		}

		void ChangeSpeed()
		{
			if (_speed < _targetSpeed)
				_speed += _acceleration * _spawner._newDelta;
			else if (_speed > _targetSpeed)
				_speed -= _acceleration * _spawner._newDelta;
		}

		public bool Avoidance()
		{
			if (!_avoid || !_spawner._birdAvoid) return false;
			if (_landing && (GetLandingSpotPosition() - _thisT.position).sqrMagnitude < (_spawner._disableLandingAvoidanceDistance * _spawner._disableLandingAvoidanceDistance)) return false;

			RaycastHit hit;
			Vector3 fwd = _modelT.forward;
			bool r = false; Vector3 pos = _thisT.position; Quaternion rot = _thisT.rotation;
			Vector3 rotE = _thisT.rotation.eulerAngles;
			if (Physics.Raycast(_thisT.position, fwd + (_modelT.right * _avoidValue), out hit, _avoidDistance, _spawner._avoidanceMask))
			{
				if (_landing) _damping = _spawner._minDamping;  // Avoids jerky movement avoiding when landing
				rotE.y -= _spawner._birdAvoidHorizontalForce * _spawner._newDelta * _damping;
				rot.eulerAngles = rotE;
				_thisT.rotation = rot;
				r = true;
			}
			else if (Physics.Raycast(_thisT.position, fwd + (_modelT.right * -_avoidValue), out hit, _avoidDistance, _spawner._avoidanceMask))
			{
				if (_landing) _damping = _spawner._minDamping;
				rotE.y += _spawner._birdAvoidHorizontalForce * _spawner._newDelta * _damping;
				rot.eulerAngles = rotE;
				_thisT.rotation = rot;
				r = true;
			}
			if (!_avoidVert) return r;
			if (_spawner._birdAvoidDown && !this._landing && Physics.Raycast(_thisT.position, -Vector3.up, out hit, _avoidDistance, _spawner._avoidanceMask))
			{
				rotE.x -= _spawner._birdAvoidVerticalForce * _spawner._newDelta * _damping;
				rot.eulerAngles = rotE;
				_thisT.rotation = rot;
				pos.y += _spawner._birdAvoidVerticalForce * _spawner._newDelta * .01f;
				_thisT.position = pos;
				r = true;
			}
			if (_spawner._birdAvoidUp && !this._landing && Physics.Raycast(_thisT.position, Vector3.up, out hit, _avoidDistance, _spawner._avoidanceMask))
			{
				rotE.x += _spawner._birdAvoidVerticalForce * _spawner._newDelta * _damping;
				rot.eulerAngles = rotE;
				_thisT.rotation = rot;
				pos.y -= _spawner._birdAvoidVerticalForce * _spawner._newDelta * .01f;
				_thisT.position = pos;
				r = true;
			}
			return r;
		}

		public void LimitRotationOfModel(Vector3 landingPos)
		{
			if (!_move) return;
			Quaternion rot = _modelT.localRotation;
			Vector3 rotE = rot.eulerAngles;
			Vector3 localE = _modelT.localEulerAngles;
			Vector3 parentE = _thisT.localEulerAngles;

			if (((_soar && _spawner._flatSoar) || (_spawner._flatFly && !_soar)) && _wayPoint.y > _thisT.position.y || (_landing && (landingPos - _thisT.position).sqrMagnitude < 16f))
			{
				rotE.x = Mathf.LerpAngle(localE.x, -parentE.x, _spawner._newDelta * 1.75f);
				rot.eulerAngles = rotE;
				_modelT.localRotation = rot;
			}
			else
			{
				rotE.x = Mathf.LerpAngle(localE.x, 0.0f, _spawner._newDelta * 1.75f);
				rot.eulerAngles = rotE;
				_modelT.localRotation = rot;
			}

			if (_spawner._birdRoll && !_landing && _move)
			{
				Vector3 localTarget = _thisT.InverseTransformPoint(_wayPoint).normalized;
				rotE.z = Mathf.LerpAngle(localE.z, -localTarget.x * _spawner._birdRollAmount, _spawner._newDelta * _damping);
			}
			else
			{
				rotE.z = Mathf.LerpAngle(localE.z, 0.0f, _spawner._newDelta * _damping);
			}
			rot.eulerAngles = rotE;
			_modelT.localRotation = rot;
		}

		public void Wander()
		{
			if (!_landing)
			{
				_damping = Random.Range(_spawner._minDamping, _spawner._maxDamping);
				_targetSpeed = Random.Range(_spawner._minSpeed, _spawner._maxSpeed);
				if (this != null) SetRandomMode();
			}
		}

		public void Wander(float delay)
		{
			if (!_landing)
			{
				_damping = Random.Range(_spawner._minDamping, _spawner._maxDamping);
				_targetSpeed = Random.Range(_spawner._minSpeed, _spawner._maxSpeed);
				if (this != null) Invoke("SetRandomMode", delay);
			}
		}

		public void SetRandomMode()
		{
			CancelInvoke("SetRandomMode");
			if (!_dived && Random.value < _spawner._soarFrequency)
			{
				Soar();
			}
			else if (!_dived && Random.value < _spawner._diveFrequency)
			{
				Dive();
			}
			else
			{
				Flap();
			}
		}

		public void Flap(float crossfadeSeconds = .5f, bool findWaypoint = true)
		{
			if (findWaypoint) if (!FindWaypoint()) return;
			if (currentAnim == "Flap" || !_move) return;
			if (!_animationIsBaked)
			{
				if (!_animator) _modelAnimation.CrossFade(_spawner._flapAnimation, crossfadeSeconds);
				else _animator.CrossFade(_spawner._flapAnimation, crossfadeSeconds);
			}
			else CachedAnimationHandler("flap");
			_soar = false;
			_dived = false;
			currentAnim = "Flap";
			AnimationSpeed();
		}

		public bool FindWaypoint(bool force = false)
		{
			if (!canFindWaypoint && !force) return false;
			Vector3 t = Vector3.zero;
			t.x = Random.Range(-_spawner._spawnSphere, _spawner._spawnSphere) + _spawner._posBuffer.x;
			t.z = Random.Range(-_spawner._spawnSphereDepth, _spawner._spawnSphereDepth) + _spawner._posBuffer.z;
			t.y = Random.Range(-_spawner._spawnSphereHeight, _spawner._spawnSphereHeight) + _spawner._posBuffer.y;
			_wayPoint = t;
			canFindWaypoint = false;
			Invoke(nameof(CanFindWaypoint), 0.25f);
			return true;
		}

		void CanFindWaypoint()
		{
			canFindWaypoint = true;
		}

		public void Soar(float crossfadeSeconds = .75f)
		{
			if (!FindWaypoint()) return;
			if (_wayPoint.y > _thisT.position.y) return;
			if (_dismounting || currentAnim == "Soar" || !_move) return;
			if (!_animationIsBaked)
			{
				if (!_animator) _modelAnimation.CrossFade(_spawner._soarAnimation, crossfadeSeconds);
				else _animator.CrossFade(_spawner._soarAnimation, crossfadeSeconds);
			}
			else CachedAnimationHandler("soar");
			_soar = true;
			currentAnim = "Soar";
			AnimationSpeed();
		}

		public void CachedAnimationHandler(string type)
		{
			if (!_bakedAnimator) return;
			if (type == "flap")
			{
				if (currentAnim != "Flap") _bakedAnimator.SetAnimation(0);
				//Debug.Log(currentAnim + "!" + "if(currentAnim!=Flap) _bakedAnimator.SetAnimation(0);");
				return;
			}
			if (type == "soar")
			{
				if (currentAnim != "Soar") _bakedAnimator.SetAnimation(1);
				return;
			}
			if (type == "idle")
			{
				if (currentAnim != "Idle") _bakedAnimator.SetAnimation(2, true);
				return;
			}
		}

		public void CrossfadeDismount()
		{
			_dismounting = true;
			if (_bakedAnimator) _bakedAnimator.crossfadeWeightAdd *= 2f;
			Invoke("CrossfadeDismountRevert", 1f);
		}

		public void CrossfadeDismountRevert()
		{
			if (_bakedAnimator) _bakedAnimator.crossfadeWeightAdd *= .5f;
			_dismounting = false;
			AnimationSpeed();
		}


		public void Dive()
		{
			if (!_animationIsBaked)
			{
				if (!_animator) _modelAnimation.CrossFade(_spawner._soarAnimation, .2f);
				else _animator.CrossFade(_spawner._soarAnimation, .2f);
			}
			else CachedAnimationHandler("soar");
			currentAnim = "Soar";
			_wayPoint = _thisT.position;
			_wayPoint.y -= _spawner._diveValue;
			_dived = true;
		}

		public void AnimationSpeed()
		{
			if (_animationIsBaked)
			{
				if (currentAnim == "Idle")
				{
					_bakedAnimator.SetSpeedMultiplier(_spawner._idleAnimationSpeed);
					return;
				}
				if (!_dived && !_landing)
				{
					if (_dismounting) _bakedAnimator.SetSpeedMultiplier(_spawner._maxAnimationSpeed * 1.15f);
					else _bakedAnimator.SetSpeedMultiplier(Random.Range(_spawner._minAnimationSpeed, _spawner._maxAnimationSpeed));
				}
				else
				{
					_bakedAnimator.SetSpeedMultiplier(_spawner._maxAnimationSpeed);
				}
				return;
			}

			if (!_animator)
			{
				if (flapState == null)
				{
					flapState = _modelAnimation[_spawner._flapAnimation];
					idleState = _modelAnimation[_spawner._idleAnimation];
					soarState = _modelAnimation[_spawner._soarAnimation];
				}
				float spd;
				if (!_dived && !_landing)
				{
					if (_dismounting) spd = _spawner._maxAnimationSpeed * 1.15f;
					else spd = Random.Range(_spawner._minAnimationSpeed, _spawner._maxAnimationSpeed);
				}
				else
				{
					spd = _spawner._maxAnimationSpeed;
				}
				if (currentAnim == "Flap")
				{
					flapState.speed = spd;
				}
				else if (currentAnim == "Soar")
				{
					soarState.speed = spd;
				}
				else if (currentAnim == "Idle")
				{
					idleState.speed = _spawner._idleAnimationSpeed;
				}
			}
			else
			{
				if (currentAnim == "Idle")
				{
					_animator.speed = _spawner._idleAnimationSpeed;
					return;
				}
				if (!_dived && !_landing)
				{
					if (_dismounting) _animator.speed = _spawner._maxAnimationSpeed * 1.15f;
					else _animator.speed = Random.Range(_spawner._minAnimationSpeed, _spawner._maxAnimationSpeed);
				}
				else
				{
					_animator.speed = _spawner._maxAnimationSpeed;
				}
			}
		}
	}
}
//void MoveAwayFromOthers()
//{
//	Vector3 dis;
//	for (int i = 0; i < _spawner._roamers.Count; i++)
//	{
//		if (_spawner._roamers[i] == this) continue;
//		dis =  _thisT.position - _spawner._roamers[i]._thisT.position;
//		//Debug.Log(dis.magnitude);
//		if (dis.sqrMagnitude < .3f)
//		{
//			_thisT.position = _thisT.position + (dis.normalized * .02f);
//		}
//	}
//	Invoke("MoveAwayFromOthers", .01f*Random.Range(1, 1.5f));
//}