using System;
using Godot;

public partial class PlayerScript : Node3D
{
    public override void _PhysicsProcess(double delta)
    {
        if (_controlMode != ControlMode.Pulling)
        {
            HandleControlMode();
        }
        else
        {
            HandlePull();
        }

        if (_controlMode == ControlMode.Movement)
        {
            HandleCameraMovement();
        }
        if (_controlMode == ControlMode.DragAndDrop)
        {
            HandleDragAndDrop();
        }
        if (_controlMode == ControlMode.Sewing)
        {
            HandleSewingObjects();
        }
    }

    #region ControlMode
    public enum ControlMode { Movement, DragAndDrop, Sewing, Pulling }
    private ControlMode _controlMode;

    private void HandleControlMode()
    {
        if (Input.IsActionJustPressed("movement_mode"))
        {
            _controlMode = ControlMode.Movement;
        }
        if (Input.IsActionJustPressed("drag_and_drop_mode"))
        {
            _controlMode = ControlMode.DragAndDrop;
        }
        if (Input.IsActionJustPressed("sewing_mode"))
        {
            _controlMode = ControlMode.Sewing;
        }
        if (Input.IsActionJustPressed("pulling_mode"))
        {
            _controlMode = ControlMode.Pulling;
        }
    }
    #endregion

    #region Camera
    [Export] public float rotationSpeed;
    [Export] public Node3D CameraContainer;
    [Export] public Camera3D Camera;

    private void HandleCameraMovement()
    {
        var rotationAngle = 0f;

        if (Input.IsActionPressed("move_right"))
        {
            rotationAngle = 1.0f;
        }
        if (Input.IsActionPressed("move_left"))
        {
            rotationAngle = -1.0f;
        }

        CameraContainer.RotateY(rotationAngle * rotationSpeed);
    }

    private void GetCameraVectors(out Vector3 origin, out Vector3 direction)
    {
        var mousePosition = GetViewport().GetMousePosition();
        origin = Camera.ProjectRayOrigin(mousePosition);
        direction = Camera.ProjectRayNormal(mousePosition);
    }

    #endregion

    #region Object
    [Export] public float DragForce = 30.0f;
    [Export] public float Damping = 5.0f;
    [Export] public float MaxForce = 100.0f;
    private RigidBody3D _draggedBody;
    private float _dragDistance;
    private void HandleDragAndDrop()
    {
        if (Input.IsActionJustPressed("interact"))
        {
            if (_draggedBody == null)
            {
                TryPickObject();
            }
            else
            {
                DropObject();
            }
        }

        if (_draggedBody != null)
        {
            HandleMoveObject();
            HandleRotateObject();
        }
    }

    private void TryPickObject()
    {
        GetCameraVectors(out var origin, out var direction);

        var end = origin + direction * 1000.0f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count == 0) return;

        var collider = result["collider"].AsGodotObject();

        if (collider is RigidBody3D body && body.IsInGroup("draggable"))
        {
            _draggedBody = body;
            _draggedBody.Sleeping = false;
            _draggedBody.LockRotation = true;
            _dragDistance = origin.DistanceTo(_draggedBody.Position);
        }
    }

    private void DropObject()
    {
        _draggedBody.Sleeping = true;
        _draggedBody.LockRotation = false;
        _draggedBody = null;
    }

    private Vector3 GetTargetPosition()
    {
        GetCameraVectors(out var origin, out var direction);

        if (Input.IsActionJustReleased("move_in") && _dragDistance > 3f)
        {
            _dragDistance -= 1f;
        }
        if (Input.IsActionJustReleased("move_out"))
        {
            _dragDistance += 1f;
        }
        //TODO: Handle scroll

        var mouseTarget = origin + direction * _dragDistance;

        return mouseTarget;
    }

    private void HandleMoveObject()
    {
        var targetPosition = GetTargetPosition();

        var displacement = targetPosition - _draggedBody.GlobalPosition;

        var force = displacement * DragForce - _draggedBody.LinearVelocity * Damping;

        force = force.LimitLength(MaxForce);

        _draggedBody.ApplyCentralForce(force);
    }

    private void HandleRotateObject()
    {
        Vector3 newRotation = new Vector3();
        var objectRotationSpeed = 0.05f;

        if (Input.IsActionPressed("rotate_right"))
        {
            newRotation.Y = 1;
        }
        if (Input.IsActionPressed("rotate_left"))
        {
            newRotation.Y = -1;
        }
        if (Input.IsActionPressed("rotate_up"))
        {
            newRotation.X = 1;
        }
        if (Input.IsActionPressed("rotate_down"))
        {
            newRotation.X = -1;
        }
        if (Input.IsActionPressed("rotate_around_right"))
        {
            newRotation.Z = 1;
        }
        if (Input.IsActionPressed("rotate_around_left"))
        {
            newRotation.Z = -1;
        }
        newRotation *= objectRotationSpeed;
        newRotation = newRotation.Rotated(Vector3.Up, CameraContainer.Rotation.Y);

        _draggedBody.RotateX(newRotation.X);
        _draggedBody.RotateY(newRotation.Y);
        _draggedBody.RotateZ(newRotation.Z);
    }
    #endregion

    #region Sewing

    private Godot.Collections.Array<Node3D> stitches = [];

    private void HandleSewingObjects()
    {
        if (Input.IsActionJustPressed("interact"))
        {
            TryPlaceStitch();
        }
    }

    private void TryPlaceStitch()
    {
        var stitchScene = ResourceLoader.Load<PackedScene>("res://scenes/stitch.tscn");

        GetCameraVectors(out var origin, out var direction);

        var end = origin + direction * 1000.0f;
        var query = PhysicsRayQueryParameters3D.Create(origin, end);
        var result = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count == 0) return;

        var collider = result["collider"].AsGodotObject();
        var hitGlobal = result["position"].AsVector3();

        if (collider is RigidBody3D body && body.IsInGroup("draggable"))
        {
            var stitch = stitchScene.Instantiate<Node3D>();
            body.AddChild(stitch);
            var hitLocal = body.ToLocal(hitGlobal);

            stitch.Position = hitLocal;

            if (stitches.Count > 0)
            {
                var previous = stitches[^1];
                stitch.Call("AddPrevious", previous);
                previous.Call("AddNext", stitch);
            }
            stitches.Add(stitch);
        }

    }

    #endregion

    #region Pulling

    private void HandlePull()
    {
        foreach (var stitch in stitches)
        {
            var parent = stitch.GetParent<RigidBody3D>();

            Vector3 force = (Vector3)stitch.Call("GetDirectionToPrevious");


            parent.ApplyForce(force, stitch.Position);
        }
    }

    #endregion
}
