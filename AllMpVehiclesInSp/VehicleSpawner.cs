using GTA;
using GTA.Math;
using System;

public abstract class VehicleSpawner
{
    protected const string BlipName = "Unique Vehicle";
    protected const float BlipScale = 0.75f;

    public Vehicle TrySpawnVehicle(in Model model, in Vector3 position, float heading)
    {
        if (model == default || !model.IsValid || !model.IsLoaded) {
            return null;
        }
        if (World.GetClosestVehicle(position, GetModelSmallestDimesion(model)) != null) {
            return null;
        }
        var vehicle = World.CreateVehicle(model, position, heading);
        vehicle.PlaceOnGround();
        return vehicle;
    }

    public Blip AddBlipForVehicle(Vehicle vehicle, BlipColor color)
    {
        if (!(vehicle?.Exists() ?? false)) {
            return null;
        }
        var blip = vehicle.AddBlip();
        blip.DisplayType = BlipDisplayType.MiniMapOnly;
        blip.Sprite = BlipSprite.Standard;
        blip.Scale = BlipScale;
        blip.Color = color;
        blip.IsShortRange = true;
        blip.Name = BlipName;
        return blip;
    }

    public void RemoveBlipFromVehicle(Vehicle vehicle)
    {
        foreach (var blip in vehicle.AttachedBlips) {
            if (blip.Exists() && blip.Name == BlipName) {
                blip.Delete();
            }
        }
    }

    public float GetModelSmallestDimesion(in Model model)
    {
        var (mins, maxs) = model.Dimensions;
        var range = maxs - mins;
        return Math.Min(range.X, range.Y);
    }
}
