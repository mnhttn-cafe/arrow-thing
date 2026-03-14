using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

/// <summary>
/// Unified input handler for both PC and mobile. Uses the Input System Gameplay
/// action map. Left-click / touch is disambiguated into tap (select arrow) vs
/// drag (pan camera) by a screen-space distance threshold.
/// </summary>
public sealed class InputHandler : MonoBehaviour
{
    [SerializeField] private float dragThresholdPixels = 10f;

    private Board _board = null!;
    private BoardView _boardView = null!;
    private CameraController _camCtrl = null!;

    private InputAction _pointAction = null!;
    private InputAction _selectAction = null!;
    private InputAction _zoomAction = null!;

    private Vector2 _pressStartScreen;
    private Vector3 _pressStartWorld;
    private bool _isDragging;
    private bool _isPressed;

    // Pinch state
    private float _lastPinchDistance;

    public void Init(Board board, BoardView boardView, CameraController camCtrl, InputActionAsset inputActions)
    {
        _board = board;
        _boardView = boardView;
        _camCtrl = camCtrl;

        var gameplay = inputActions.FindActionMap("Gameplay", true);
        _pointAction = gameplay.FindAction("Point", true);
        _selectAction = gameplay.FindAction("Select", true);
        _zoomAction = gameplay.FindAction("Zoom", true);

        gameplay.Enable();

        EnhancedTouchSupport.Enable();
    }

    private void OnDestroy()
    {
        if (EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Disable();
    }

    private void Update()
    {
        HandleTouchPinch();
        HandleScrollZoom();
        HandleSelectAndPan();
    }

    private void HandleSelectAndPan()
    {
        if (_selectAction.WasPressedThisFrame())
        {
            _pressStartScreen = _pointAction.ReadValue<Vector2>();
            _pressStartWorld = _camCtrl.Cam.ScreenToWorldPoint(_pressStartScreen);
            _isDragging = false;
            _isPressed = true;
        }

        if (_isPressed && _selectAction.IsPressed())
        {
            Vector2 currentScreen = _pointAction.ReadValue<Vector2>();
            float dist = Vector2.Distance(_pressStartScreen, currentScreen);

            if (!_isDragging && dist >= dragThresholdPixels)
                _isDragging = true;

            if (_isDragging)
            {
                Vector3 currentWorld = _camCtrl.Cam.ScreenToWorldPoint(currentScreen);
                Vector3 delta = _pressStartWorld - currentWorld;
                _camCtrl.Pan(delta);
                // Recalculate start world pos after pan so dragging feels smooth
                _pressStartWorld = _camCtrl.Cam.ScreenToWorldPoint(currentScreen);
            }
        }

        if (_isPressed && _selectAction.WasReleasedThisFrame())
        {
            _isPressed = false;

            if (!_isDragging)
            {
                // It was a tap — attempt to select/clear an arrow
                Vector3 worldPos = _camCtrl.Cam.ScreenToWorldPoint(_pressStartScreen);
                Cell cell = BoardCoords.WorldToCell(worldPos, _board.Width, _board.Height);

                if (_board.Contains(cell))
                {
                    Arrow arrow = _board.GetArrowAt(cell);
                    if (arrow != null)
                        _boardView.TryClearArrow(arrow);
                }
            }
        }
    }

    private void HandleScrollZoom()
    {
        float scroll = _zoomAction.ReadValue<float>();
        if (!Mathf.Approximately(scroll, 0f))
            _camCtrl.Zoom(scroll);
    }

    private void HandleTouchPinch()
    {
        if (Touch.activeTouches.Count < 2)
        {
            _lastPinchDistance = 0f;
            return;
        }

        var touch0 = Touch.activeTouches[0];
        var touch1 = Touch.activeTouches[1];
        float currentDistance = Vector2.Distance(touch0.screenPosition, touch1.screenPosition);

        if (_lastPinchDistance > 0f && currentDistance > 0f)
        {
            float ratio = currentDistance / _lastPinchDistance;
            _camCtrl.PinchZoom(ratio);
        }

        _lastPinchDistance = currentDistance;

        // Suppress tap when pinching
        _isDragging = true;
    }
}
