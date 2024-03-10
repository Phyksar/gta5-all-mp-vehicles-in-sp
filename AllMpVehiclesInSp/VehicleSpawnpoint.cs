using GTA;
using GTA.Math;
using System;
using Utilities;

public class VehicleSpawnpoint : IPosition3, IDisposable
{
    public Vector3 Position { get; private set; }
    public float Heading { get; private set; }
    public VehicleGroup GroupId { get; private set; }

    public Model Model = default;
    public Vehicle Vehicle = null;
    public bool WasTakenByPlayer = false;

    public bool IsModelAvailable => Model != default && Model.IsValid && Model.IsLoaded;

    public VehicleSpawnpoint(in VehicleSpawnpointDesc description)
    {
        Position = description.Position;
        Heading = description.Heading;
        GroupId = description.GroupId;
    }

    public void Dispose()
    {
        DespawnVehicle();
    }

    public void RequestModel(string modelName)
    {
        if (Model != default) {
            Model.MarkAsNoLongerNeeded();
        }
        Model = new Model(modelName);
        Model.Request();
    }

    public void MarkAsTakenByPlayer()
    {
        WasTakenByPlayer = true;
    }

    public void DespawnVehicle()
    {
        if (!WasTakenByPlayer && (Vehicle?.Exists() ?? false)) {
            Vehicle.MarkAsNoLongerNeeded();
        }
        Model.MarkAsNoLongerNeeded();
        Model = default;
        Vehicle = null;
        WasTakenByPlayer = false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Position, Heading, GroupId);
    }

    private static float GetModelSmallestDimesion(in Model model)
    {
        var (mins, maxs) = model.Dimensions;
        var range = maxs - mins;
        return Math.Min(range.X, range.Y);
    }
}
