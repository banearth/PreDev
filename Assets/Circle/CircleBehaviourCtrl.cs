using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CircleBehaviourCtrl : MonoBehaviour
{
	[SerializeField]
	private float[] _radiusConfigArray;

	[SerializeField]
	private float _fadeDuration = 10;

	[SerializeField]
	public CircleBehaviour _circleBehaviour;

	[SerializeField]
	private Material _redMaterial;

	[SerializeField]
	private Material _greenMaterial;

	[SerializeField]
	private float _sceneSize = 10;

	[SerializeField]
	private GameObject _simulatePlayerPrefab;

	[SerializeField]
	private int _simulatePlayerCount = 100;

	private List<GameObject> _simulatePlayerList = new List<GameObject>();

	private int _nextRadiusIndex = 0;
	private void Start()
	{
		_circleBehaviour.SetPositionAndRadius(this.transform.position,_sceneSize/2);
		for(int i = 0; i < _simulatePlayerCount; i++) 
		{
			_simulatePlayerList.Add(Instantiate(_simulatePlayerPrefab));
		}
		ArrangePlayerWithRandomPosition();
	}

	private void Update()
	{
		AffectOutOfCircle();
	}

	private bool _hasNextRadnomPosition;
	private Vector3 _nextRadnomPosition;

	public void DisplayNextRandomPosition()
	{
		if (_circleBehaviour.IsInFade)
		{
			Debug.Log("正在缩圈");
			return;
		}
		if (_nextRadiusIndex >= _radiusConfigArray.Length)
		{
			Debug.Log("已完成");
		}
		var nextRadius = _radiusConfigArray[_nextRadiusIndex];
		var nextPosition = Vector3.zero;
		for(int i = 0;i<100;i++)
		{
			nextPosition = _circleBehaviour.GenerateNextRandomPosition(nextRadius);
			DrawCross(nextPosition, 1, Color.green, 1f);
		}
		_hasNextRadnomPosition = true;
		_nextRadnomPosition = nextPosition;
	}

	private void DrawCross(Vector3 center, float size, Color color, float duration)
	{
		Debug.DrawLine(center - Vector3.right * size / 2, center + Vector3.right * size / 2, color, duration);
		Debug.DrawLine(center - Vector3.up * size / 2, center + Vector3.up * size / 2, color, duration);
		Debug.DrawLine(center - Vector3.forward * size / 2, center + Vector3.forward * size / 2, color, duration);
	}

	public void ShrinkNext()
	{
		if(_circleBehaviour.IsInFade)
		{
			Debug.Log("正在缩圈");
			return;
		}
		if (_nextRadiusIndex >= _radiusConfigArray.Length)
		{
			Debug.Log("已完成");
			return;
		}
		var nextRadius = _radiusConfigArray[_nextRadiusIndex++];
		Vector3 nextPosition;
		if (_hasNextRadnomPosition)
		{
			nextPosition = _nextRadnomPosition;
			_hasNextRadnomPosition= false;
		}
		else
		{
			nextPosition = _circleBehaviour.GenerateNextRandomPosition(nextRadius);
		}
		_circleBehaviour.Fade(nextPosition, nextRadius, _fadeDuration);
	}

	public void ArrangePlayerWithRandomPosition()
	{
		foreach (var player in _simulatePlayerList) 
		{
			player.transform.position = new Vector3(Random.Range(-_sceneSize / 2, _sceneSize / 2), 0, Random.Range(-_sceneSize / 2, _sceneSize / 2));
		}
	}

	public void Reload()
	{
		SceneManager.LoadScene(0);
	}

	private void AffectOutOfCircle()
	{
		foreach (GameObject go in _simulatePlayerList) 
		{
			go.GetComponent<Renderer>().material = _circleBehaviour.IsOutOfCircle(go.transform.position) ? _redMaterial : _greenMaterial;
		}
	}

	private void OnDrawGizmos()
	{
		Gizmos.color = Color.white;
		Gizmos.DrawWireCube(this.transform.position, new Vector3(_sceneSize, 0, _sceneSize));
	}

}



