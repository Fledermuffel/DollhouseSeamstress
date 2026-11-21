using Godot;
using System;
using System.Linq;

public partial class StitchScript : Node3D
{
    public Node3D previousStitch;
    public Node3D nextStitch;

    public void AddPrevious(Node3D previous)
    {
        previousStitch = previous;
    }

    public void AddNext(Node3D next)
    {
        nextStitch = next;
    }

    public Vector3 GetDirectionToPrevious()
    {

        return new Vector3();
    }

    public Vector3 GetCurrentForce()
    {
        var force = new Vector3();
        var forceMultiplier = 5;

        if (previousStitch != null)
        {
            var distance = GlobalPosition.DistanceTo(previousStitch.GlobalPosition);

            force += (GlobalPosition - previousStitch.GlobalPosition).Normalized() * distance;
        }

        if (nextStitch != null)
        {
            var distance = GlobalPosition.DistanceTo(nextStitch.GlobalPosition);

            force += (GlobalPosition - nextStitch.GlobalPosition).Normalized() * distance;
        }

        return force * forceMultiplier;
    }
}
