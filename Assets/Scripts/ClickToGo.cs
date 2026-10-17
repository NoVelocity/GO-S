using UnityEngine;
using UnityEngine.InputSystem;

public class ClickToGo : MonoBehaviour
{
    [SerializeField] PlayerInput _playerInput;
    [SerializeField] GameObject _destination;
    
    private RobotMover _robotMover;
    [SerializeField] private GameObject _selectedRobot;
    
    private Camera _camera;
    
    private InputAction _clickAction;
    private InputAction _altClickAction;
    private InputAction _positionAction;
    
    void Awake()
    {
        _camera = Camera.main;
        
        _clickAction = _playerInput.actions["SetNavMeshCommand/MousePress"];
        _altClickAction = _playerInput.actions["SetNavMeshCommand/MouseAltPress"];
        _positionAction = _playerInput.actions["SetNavMeshCommand/MousePos"];
    }

    void OnEnable()
    {
        _clickAction.performed += OnClickPerformed;
        _altClickAction.performed += OnAltClickPerformed;
    }
    
    void OnDisable()
    {
        _clickAction.performed -= OnClickPerformed;
        _altClickAction.performed -= OnAltClickPerformed;
    }

    void OnClickPerformed(InputAction.CallbackContext context)
    {
        Vector2 mousePos = _positionAction.ReadValue<Vector2>();
        Ray ray = _camera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            _destination.transform.position = hit.point;
            _robotMover?.SetNavMeshDestination(hit.point);
        }
    }
    
    void OnAltClickPerformed(InputAction.CallbackContext context)
    {
        Vector2 mousePos = _positionAction.ReadValue<Vector2>();
        Ray ray = _camera.ScreenPointToRay(mousePos);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            _selectedRobot = hit.transform.gameObject;
            _robotMover =  _selectedRobot.GetComponent<RobotMover>();
            print($"Robot selected: {_selectedRobot.name}");
        }
    }
}
