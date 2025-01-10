using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 只考虑XZ方向
/// </summary>
public class CircleBehaviour : MonoBehaviour
{
	public bool IsInFade => _isInFade;

	private float _radius;
	private Vector3 _position;
	private float _fromRadius;
	private Vector3 _fromPosition;
	private float _toRadius;
	private Vector3 _toPosition;
	private float _fadeStartTime;
	private float _fadeDuration;
	private bool _isInFade = false;

	public Vector3 GenerateNextRandomPosition(float nextRadius)
	{
		var randomRadius = Mathf.Sqrt(Random.Range(0, 1f)) * (_radius - nextRadius);
		var randomAngle = Random.Range(0, Mathf.PI * 2);
		return new Vector3
		(
			_position.x + Mathf.Cos(randomAngle) * randomRadius,
			_position.y, 
			_position.z + Mathf.Sin(randomAngle) * randomRadius
		);
	}

	public void SetPositionAndRadius(Vector3 position, float radius)
	{
		_position = position;
		_radius = radius;
	}

	public void Fade(Vector3 targetPosition, float targetRadius, float duration)
	{
        _isInFade = true;
		_fromRadius = _radius;
		_fromPosition = _position;
		_toRadius = targetRadius;
		_toPosition = targetPosition;
		_fadeStartTime = Time.time;
		_fadeDuration = duration;
    }

	public bool IsOutOfCircle(Vector3 testPostion)
	{
		return new Vector2(testPostion.x - _position.x, testPostion.z - _position.z).sqrMagnitude > _radius * _radius;
	}

	private void Update()
	{
		ProcessFade();
	}

	private void ProcessFade()
	{
		if (_isInFade)
		{
			float progress = Mathf.Clamp01((Time.time - _fadeStartTime) / _fadeDuration);
			_position = Vector3.Lerp(_fromPosition, _toPosition, progress);
			_radius = Mathf.Lerp(_fromRadius, _toRadius, progress);
			if (progress >= 1)
			{
				_isInFade = false;
			}
		}
	}

	private void OnDrawGizmos()
    {
		if(Application.isPlaying)
		{
			Gizmos.color = Color.red;
			Gizmos.DrawWireSphere(_position, _radius);
			if(_isInFade)
			{
				Gizmos.color = Color.green;
				Gizmos.DrawWireSphere(_toPosition, _toRadius);
			}
		}
	}

}
