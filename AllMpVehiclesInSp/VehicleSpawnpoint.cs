using GTA;
using GTA.Math;
using GTA.Native;
using System;

public class VehicleSpawnpoint : Utilities.IPosition3, IDisposable
{
    private const string BlipName = "Unique Vehicle";
    private const float BlipScale = 0.75f;

    public Vector3 Position { get; private set; }
    public float Heading { get; private set; }
    public VehicleGroup GroupId { get; private set; }

    public Model Model = default;
    public Vehicle Vehicle = null;
    public Blip Blip = null;
    public bool WasTakenByPlayer = false;

    public bool IsModelAvailable => Model != default && Model.IsLoaded;

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

    public bool TrySpawnVehicle(out Vehicle vehicle)
    {
        if (!IsModelAvailable) {
            vehicle = null;
            return false;
        }
        if (World.GetClosestVehicle(Position, GetModelSmallestDimesion(Model)) != null) {
            vehicle = null;
            return false;
        }
        Vehicle = World.CreateVehicle(Model, Position, Heading);
        Vehicle.PlaceOnGround();
        vehicle = Vehicle;
        return true;
    }

    public bool AddBlipForVehicle()
    {
        if (!(Vehicle?.Exists() ?? false)) {
            return false;
        }
        Blip = Function.Call<Blip>(Hash.ADD_BLIP_FOR_ENTITY, Vehicle);
        Blip.Name = BlipName;
        Blip.DisplayType = BlipDisplayType.MiniMapOnly;
        Blip.Sprite = BlipSprite.Standard;
        Blip.Scale = BlipScale;
        Blip.Color = BlipColor.WhiteNotPure;
        Blip.IsShortRange = true;
        return true;
    }

    public void MarkAsTakenByPlayer()
    {
        if (Vehicle?.Exists() ?? false) {
            Vehicle.MarkAsNoLongerNeeded();
        }
        if (Blip?.Exists() ?? false) {
            Blip.Delete();
        }
        Blip = null;
        WasTakenByPlayer = true;
    }

    public void DespawnVehicle()
    {
        if (!WasTakenByPlayer && (Vehicle?.Exists() ?? false)) {
            Vehicle.Delete();
        }
        WasTakenByPlayer = false;
        if (Blip?.Exists() ?? false) {
            Blip.Delete();
        }
        Model.MarkAsNoLongerNeeded();
        Model = default;
        Vehicle = null;
        Blip = null;
    }

    private static float GetModelSmallestDimesion(in Model model)
    {
        var (mins, maxs) = model.Dimensions;
        var range = maxs - mins;
        return Math.Min(range.X, range.Y);
    }
}
