using System;
using System.Linq;
using Godot;
using Starflake.Domain;

namespace Starflake.Sim;

/// <summary>The pilot ship's tractor pulls in nearby fuel pods and its scoop collects them, until the tank is full.</summary>
public static class FuelScoop
{
    public static void Step(PilotShip ship, StarSystemState state, float dt)
    {
        var hull = ship.ShipClass;
        var at = ship.Position.ToGodot();
        foreach (var pod in (state.FuelPods ?? []).ToList()) // collecting removes pods mid-loop
        {
            if (Rules.TankFull(ship))
                return;
            var position = pod.Position.ToGodot();
            var distance = position.DistanceTo(at);
            if (distance < hull.ScoopRadius)
            {
                ship.Fuel = Math.Min(hull.FuelCapacity, ship.Fuel + pod.FuelAmount); // the overflow is lost
                state.FuelPods!.Remove(pod);
            }
            else if (distance < hull.TractorRange)
                pod.Position = Vector3D.From(position.MoveToward(at, hull.TractorSpeed * dt)); // no direction needed: safe at zero distance
        }
    }
}
