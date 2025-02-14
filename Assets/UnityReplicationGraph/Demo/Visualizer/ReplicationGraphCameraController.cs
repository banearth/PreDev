using UnityEngine;

namespace ReplicationGraph
{
    public class ReplicationGraphCameraController : MonoBehaviour
    {
        [Header("相机控制参数")]
        [SerializeField] private float _moveSpeed = 1f;         // 移动速度系数
        [SerializeField] private float _zoomSpeed = 2f;         // 缩放速度
        [SerializeField] private float _minZoom = 5f;           // 最小正交尺寸
        [SerializeField] private float _maxZoom = 30f;          // 最大正交尺寸

        private Camera _camera;
        private Vector3 _lastMousePosition;
        private Vector2 _dragStartPosition;
        private Vector3 _dragStartCameraPosition;

        private void Start()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            // 确保相机为正交模式
            _camera.orthographic = true;
            
            // 初始化正交尺寸
            _camera.orthographicSize = (_minZoom + _maxZoom) * 0.5f;
        }

        private void LateUpdate()
        {
            HandleMouseInput();
            HandleScrollWheel();
        }

        private void HandleMouseInput()
        {
            // 鼠标右键按下时记录起始位置
            if (Input.GetMouseButtonDown(1))
            {
                _dragStartPosition = Input.mousePosition;
                _dragStartCameraPosition = transform.position;
            }
            // 鼠标右键拖拽时直接计算位置差
            else if (Input.GetMouseButton(1))
            {
                Vector2 currentMousePosition = Input.mousePosition;
                Vector2 difference = currentMousePosition - _dragStartPosition;
                
                // 将屏幕空间的差值转换为世界空间的位移（注意：现在是在XZ平面上移动）
                float worldSpaceScale = 2f * _camera.orthographicSize / Screen.height;
                Vector3 move = new Vector3(
                    -difference.x * _moveSpeed * worldSpaceScale,
                    0,  // Y轴保持不变
                    -difference.y * _moveSpeed * worldSpaceScale  // 垂直方向映射到Z轴
                );

                // 直接设置位置，而不是增量移动
                transform.position = _dragStartCameraPosition + move;
            }
        }

        private void HandleScrollWheel()
        {
            float scrollDelta = Input.mouseScrollDelta.y;
            if (scrollDelta != 0)
            {
                // 调整正交尺寸
                _camera.orthographicSize = Mathf.Clamp(
                    _camera.orthographicSize - scrollDelta * _zoomSpeed,
                    _minZoom,
                    _maxZoom
                );
            }
        }

        // 公共方法：设置相机位置
        public void SetPosition(Vector3 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        // 公共方法：设置相机缩放
        public void SetZoom(float zoom)
        {
            _camera.orthographicSize = Mathf.Clamp(zoom, _minZoom, _maxZoom);
        }
    }
} 