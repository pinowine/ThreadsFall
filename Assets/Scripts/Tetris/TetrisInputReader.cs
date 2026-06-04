using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class TetrisInputReader : MonoBehaviour
{
    public event Action<Vector2Int> MovePressed;
    public event Action RotateClockwisePressed;
    public event Action RotateCounterClockwisePressed;
    public event Action HardDropPressed;
    public event Action DebugSpawnPressed;

    private InputAction moveAction;
    private InputAction rotateClockwiseAction;
    private InputAction rotateCounterClockwiseAction;
    private InputAction hardDropAction;
    private InputAction debugSpawnAction;

    private void Awake()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        rotateClockwiseAction = new InputAction(
            "RotateClockwise",
            InputActionType.Button,
            "<Mouse>/leftButton"
        );

        rotateCounterClockwiseAction = new InputAction(
            "RotateCounterClockwise",
            InputActionType.Button,
            "<Mouse>/rightButton"
        );

        hardDropAction = new InputAction(
            "HardDrop",
            InputActionType.Button,
            "<Keyboard>/space"
        );

        debugSpawnAction = new InputAction(
            "DebugSpawnPiece",
            InputActionType.Button,
            "<Keyboard>/g"
        );
    }

    private void OnEnable()
    {
        moveAction.Enable();
        rotateClockwiseAction.Enable();
        rotateCounterClockwiseAction.Enable();
        hardDropAction.Enable();
        debugSpawnAction.Enable();

        moveAction.performed += HandleMove;
        rotateClockwiseAction.performed += HandleRotateClockwise;
        rotateCounterClockwiseAction.performed += HandleRotateCounterClockwise;
        hardDropAction.performed += HandleHardDrop;
        debugSpawnAction.performed += HandleDebugSpawn;
    }

    private void OnDisable()
    {
        moveAction.performed -= HandleMove;
        rotateClockwiseAction.performed -= HandleRotateClockwise;
        rotateCounterClockwiseAction.performed -= HandleRotateCounterClockwise;
        hardDropAction.performed -= HandleHardDrop;
        debugSpawnAction.performed -= HandleDebugSpawn;

        moveAction.Disable();
        rotateClockwiseAction.Disable();
        rotateCounterClockwiseAction.Disable();
        hardDropAction.Disable();
        debugSpawnAction.Disable();
    }

    private void HandleMove(InputAction.CallbackContext context)
    {
        Vector2 value = context.ReadValue<Vector2>();

        Vector2Int direction = Vector2Int.zero;

        if (value.x > 0.5f)
            direction = Vector2Int.right;
        else if (value.x < -0.5f)
            direction = Vector2Int.left;
        else if (value.y > 0.5f)
            direction = Vector2Int.up;
        else if (value.y < -0.5f)
            direction = Vector2Int.down;

        if (direction != Vector2Int.zero)
            MovePressed?.Invoke(direction);
    }

    private void HandleRotateClockwise(InputAction.CallbackContext context)
    {
        RotateClockwisePressed?.Invoke();
    }

    private void HandleRotateCounterClockwise(InputAction.CallbackContext context)
    {
        RotateCounterClockwisePressed?.Invoke();
    }

    private void HandleHardDrop(InputAction.CallbackContext context)
    {
        HardDropPressed?.Invoke();
    }

    private void HandleDebugSpawn(InputAction.CallbackContext context)
    {
        DebugSpawnPressed?.Invoke();
    }
}