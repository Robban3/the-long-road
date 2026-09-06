/*
Copyright Unluck Software
www.chemicalbliss.com
*/

// DEV NOTES 
// LimitPitchRotation not doing anything - flat landing always enabled.

using UnityEngine;
using System.Collections.Generic;

namespace UnluckSoftware {

public class FlockController :MonoBehaviour
{
	[Tooltip("Assign prefab with FlockChild script attached")]
	public FlockChild _childPrefab;
	[Tooltip("Number of birds in the flock")]
	public int _childAmount = 250;
	[Tooltip("If enabled, birds will be instantiated one by one over time instead of all at once at start.")]
	public bool _slowSpawn;
	[Tooltip("Width of the area around the spawner where waypoints will be created.")]
	public float _spawnSphere = 3.0f;
	[Tooltip("Height of the spawn sphere.")]
	public float _spawnSphereHeight = 3.0f;
	[Tooltip("Depth of the spawn sphere.")]
	public float _spawnSphereDepth = 3.0f;
	[Tooltip("Minimum random speed for birds.")]
	public float _minSpeed = 6.0f;
	[Tooltip("Maximum random speed for birds.")]
	public float _maxSpeed = 10.0f;
	[Tooltip("Minimum acceleration for birds.")]
	public float _minAcceleration = 10f;
	[Tooltip("Maximum acceleration for birds.")]
	public float _maxAcceleration = 20f;
	[Tooltip("Minimum random size for birds.")]
	public float _minScale = .7f;
	[Tooltip("Maximum random size for birds.")]
	public float _maxScale = 1.0f;
	[Tooltip("How often soar is initiated (1 = always, 0 = never).")]
	public float _soarFrequency = 0.0f;
	[Tooltip("Animation (required) for soar functionality.")]
	public string _soarAnimation = "Soar";
	[Tooltip("Animation used for flapping.")]
	public string _flapAnimation = "Flap";
	[Tooltip("Animation (required) for sitting idle functionality.")]
	public string _idleAnimation = "Idle";
	[Tooltip("Dive depth for birds.")]
	public float _diveValue = 7.0f;
	[Tooltip("How often dive (1 = always, 0 = never).")]
	public float _diveFrequency = 0.5f;
	[Tooltip("Rotation tween damping, lower number = smooth/slow rotation (if this gets stuck in a loop, increase this value).")]
	public float _minDamping = 1.0f;
	[Tooltip("Maximum rotation damping.")]
	public float _maxDamping = 2.0f;
	[Tooltip("How close a bird can get to a waypoint before creating a new one (also helps fix getting stuck in a loop).")]
	public float _waypointDistance = 1.0f;
	[Tooltip("Minimum animation speed.")]
	public float _minAnimationSpeed = 2.0f;
	[Tooltip("Maximum animation speed.")]
	public float _maxAnimationSpeed = 4.0f;
	[Tooltip("Time in seconds before the flock controller moves to a new random position (0 = never).")]
	public float _randomPositionTimer = 10.0f;
	[Tooltip("If 'Auto Waypoint Delay' is greater than zero, the controller will be moved to a random position within this sphere.")]
	public float _positionSphere = 25.0f;
	[Tooltip("Overrides height of the position sphere for more control.")]
	public float _positionSphereHeight = 25.0f;
	[Tooltip("Depth of the position sphere.")]
	public float _positionSphereDepth = -1.0f;
	[Tooltip("Runs the random position function when a child reaches the controller's current waypoint.")]
	public bool _childTriggerPos;
	[Tooltip("Forces all children to change waypoints when the flock controller changes its position.")]
	public bool _forceChildWaypoints;
	[Tooltip("Random delay added before forcing new waypoint on children.")]
	public float _forcedRandomDelay = 1.5f;
	[Tooltip("Birds will not rotate upwards as much when flapping.")]
	public bool _flatFly;
	[Tooltip("Birds will not rotate upwards as much when soaring.")]
	public bool _flatSoar;
	[Tooltip("Birds will bank (roll) when turning.")]
	public bool _birdRoll = true;
	[Tooltip("How much the bird will bank when turning.")]
	public float _birdRollAmount = 45.0f;
	[Tooltip("Birds will steer away from colliders (Raycast).")]
	public bool _birdAvoid;
	[Tooltip("Distance from landing spot where avoidance is disabled.")]
	public float _disableLandingAvoidanceDistance = 4f;
	[Tooltip("How much a bird will react to avoid collision left and right.")]
	public int _birdAvoidHorizontalForce = 1000;
	[Tooltip("Avoid colliders below the bird.")]
	public bool _birdAvoidDown;
	[Tooltip("Avoid colliders above the bird.")]
	public bool _birdAvoidUp;
	[Tooltip("How much a bird will react to avoid collision down and up.")]
	public int _birdAvoidVerticalForce = 300;
	[Tooltip("Maximum distance to check for collision to avoid.")]
	public float _birdAvoidDistanceMax = 4.5f;
	[Tooltip("Minimum distance to check for collision to avoid.")]
	public float _birdAvoidDistanceMin = 5.0f;
	[Tooltip("Stops soaring after X seconds (0 = never), use to avoid birds soaring for too long.")]
	public float _soarMaxTime;
	[Tooltip("LayerMask for colliders that birds should avoid.")]
	public LayerMask _avoidanceMask = (LayerMask)(-1);
	public List<FlockChild> _roamers;
	public Vector3 _posBuffer;
	[Tooltip("If enabled, birds will update every other frame to improve performance.")]
	public bool _skipFrame;
	// _updateDivisor is NO LONGER USED
	public float _newDelta;
	public int _updateCounter;
	public float _activeChildren;
	[Tooltip("If enabled, birds will be grouped under a new GameObject.")]
	public bool _groupChildToNewTransform;
	public Transform _groupTransform;
	[Tooltip("Name for the new GameObject if 'Group to New GameObject' is enabled.")]
	public string _groupName = "";
	[Tooltip("If enabled, birds will be grouped under the FlockController GameObject.")]
	public bool _groupChildToFlock;
	[Tooltip("Offset for the flock's starting position relative to the controller.")]
	public Vector3 _startPosOffset;
	public Transform _thisT;
	[Tooltip("Limits the bird's pitch rotation when flying or soaring upwards.")]
	public bool LimitPitchRotation = true;
	public int _childCountCache;
	[Tooltip("Min delay for idle animation.")]
	public float idleAnimationDelayMin = 0.1f;
	[Tooltip("Max delay for idle animation.")]
	public float idleAnimationDelayMax = 0.75f;
	[Tooltip("Animation speed multiplier when landed (idle). Increase for fast birds like hummingbirds.")]
	public float _idleAnimationSpeed = 1.0f;
	//
	public void Start()
	{
		//Application.targetFrameRate = 10;
		_roamers.Clear();
		//Invoke("LateStart", 0f);
		LateStart();
	}
	//
	public void LateStart()
	{
		if (!_childPrefab)
		{
			Debug.LogWarning("Bird Flock does not have a bird prefab assigned, aborting.");
			this.enabled = false;
			return;
		}
		_thisT = transform;
		_posBuffer = _thisT.position + _startPosOffset;
		if (!_slowSpawn)
		{
			AddChild(_childAmount);
			_childCountCache = _roamers.Count;
		}
		if (_randomPositionTimer > 0) InvokeRepeating("SetFlockRandomPosition", _randomPositionTimer, _randomPositionTimer); // > C
	}
	//
	public void AddChild(int amount)
	{
		if (_groupChildToNewTransform) InstantiateGroup();
		for (int i = 0; i < amount; i++)
		{
			FlockChild obj = (FlockChild)Instantiate(_childPrefab);
			obj._spawner = this;
			
			_roamers.Add(obj);
#if UNITY_EDITOR
			obj.name = "bird " + _roamers.Count;
#endif
			AddChildToParent(obj.transform);
		}

		_childCountCache += amount;
	}
	//
	public void AddChildToParent(Transform obj)
	{
		if (_groupChildToFlock)
		{
			_groupTransform = _thisT;
		}
		obj.parent = _groupTransform;
	}
	//
	public void RemoveChild(int amount)
	{
		for (int i = 0; i < amount; i++)
		{
			FlockChild dObj = _roamers[_roamers.Count - 1];
			_roamers.RemoveAt(_roamers.Count - 1);
			Destroy(dObj.gameObject);
		}
		_childCountCache -= amount;
	}
	//
	public void Update()
	{
		if (_activeChildren > 0)
		{
			if (_skipFrame)
			{
				_updateCounter++;
				_updateCounter = _updateCounter % 2;
				_newDelta = Time.deltaTime * 2;
			} else
			{
				_newDelta = Time.deltaTime;
			}
		}
		UpdateChildAmount();
		for (int i = 0; i < _roamers.Count; i++)
		{
			_roamers[i].BirdUpdate();
		}
	}
	//
	public void InstantiateGroup()
	{
		if (_groupTransform != null) return;
		GameObject g = new GameObject();
		_groupTransform = g.transform;
		_groupTransform.position = _thisT.position;
		if (_groupName != "")
		{
			g.name = _groupName;
			return;
		}
		g.name = _thisT.name + " Bird Container";
	}
	//
	public void UpdateChildAmount()
	{
		if (_childAmount < _roamers.Count && _childAmount >= 0) RemoveChild(1);
		else if (_childAmount > _roamers.Count)
		{
			AddChild(1);
		}
	}
	// Draws scene gizmos for flock sizes
	public void OnDrawGizmos()
	{
		if (_thisT == null) _thisT = transform;
		if (!Application.isPlaying)
		{
			if (_posBuffer != _thisT.position + _startPosOffset)
			{
				_posBuffer = _thisT.position + _startPosOffset;
			}
		}
		Gizmos.color = new Color(.6f, .6f, .6f);
		Gizmos.DrawWireCube(_posBuffer, new Vector3(_spawnSphere * 2, _spawnSphereHeight * 2, _spawnSphereDepth * 2));
		Gizmos.color = new Color(.1f, .1f, .1f);
		Gizmos.DrawWireCube(_thisT.position, new Vector3((_positionSphere * 2) + _spawnSphere * 2, (_positionSphereHeight * 2) + _spawnSphereHeight * 2, (_positionSphereDepth * 2) + _spawnSphereDepth * 2));
	}
	//Set waypoint randomly inside box
	public void SetFlockRandomPosition()
	{
		Vector3 t = Vector3.zero;
		t.x = Random.Range(-_positionSphere, _positionSphere) + _thisT.position.x;
		t.z = Random.Range(-_positionSphereDepth, _positionSphereDepth) + _thisT.position.z;
		t.y = Random.Range(-_positionSphereHeight, _positionSphereHeight) + _thisT.position.y;
		_posBuffer = t;
		if (_forceChildWaypoints)
		{
			for (int i = 0; i < _roamers.Count; i++)
			{
				(_roamers[i]).Wander(Random.value * _forcedRandomDelay);
			}
		}
	}
	//Instantly destroys all birds
	public void destroyBirds()
	{
		for (int i = 0; i < _roamers.Count; i++)
		{
			if (_roamers[i] != null) Destroy((_roamers[i]).gameObject);
		}
		_childCountCache = 0;
		_childAmount = 0;
		_roamers.Clear();
	}
}
}