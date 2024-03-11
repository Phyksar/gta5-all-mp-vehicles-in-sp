using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

public class TrafficVehicleSpawner : VehicleSpawner, IDisposable
{
    private const float AheadSpawnVelocityThreshold = 18.0f;
    private const float WorldVehicleLookupDistance = 100.0f;
    private const int LodDistanceStep = 50;

    private Random Random;
    private ISearchQuery<VehicleSpawnpoint> SpawnpointSearchQuery;
    private IReadOnlyDictionary<VehicleGroup, string[]> GroupedVehicleModels;
    private HashSet<Vehicle> SpawnedVehicles;
    private Model NextModel;
    private Vehicle LastPlayerVehicle;
    private Vector3 ModelRequestPosition;

    private float SpawnDistance;
    private float DespawnDistance;
    private float ModelInvalidationDistance;
    private bool AddBlips;

    public int TotalVehicles => SpawnedVehicles.Count;

    public bool IsModelAvailable => NextModel != default && NextModel.IsValid & NextModel.IsLoaded;

    private int LodDistanceThreshold => (int)SpawnDistance / LodDistanceStep * LodDistanceStep;

    public TrafficVehicleSpawner(
        Random random,
        ISearchQuery<VehicleSpawnpoint> spawnpointSearchQuery,
        IReadOnlyDictionary<VehicleGroup, string[]> groupedVehicleModels,
        float spawnDistance,
        float despawnDistance,
        float modelInvalidationDistance,
        bool addBlips)
    {
        Random = random;
        SpawnpointSearchQuery = spawnpointSearchQuery;
        GroupedVehicleModels = groupedVehicleModels;
        SpawnedVehicles = new HashSet<Vehicle>();
        NextModel = default;
        LastPlayerVehicle = null;
        ModelRequestPosition = Vector3.Zero;
        SpawnDistance = spawnDistance;
        DespawnDistance = despawnDistance;
        ModelInvalidationDistance = modelInvalidationDistance;
        AddBlips = addBlips;
    }

    public void Dispose()
    {
        foreach (var vehicle in SpawnedVehicles) {
            if (vehicle.Exists()) {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Driver?.MarkAsNoLongerNeeded();
            }
        }
        SpawnedVehicles.Clear();
    }

    public bool TryRequestModel(in Vector3 position)
    {
        if (!GetVehicleModelNameFromSpawnpoints(SpawnpointSearchQuery.FindInSphere(position), out var modelName)) {
            return false;
        }
        if (NextModel != default && NextModel.IsValid) {
            NextModel.MarkAsNoLongerNeeded();
        }
        NextModel = new Model(modelName);
        NextModel.Request();
        ModelRequestPosition = position;
        ScriptLog.DebugMessage($"Requested next traffic vehicle {modelName}");
        return true;
    }

    public void FreeModel()
    {
        if (NextModel != default && NextModel.IsValid) {
            NextModel.MarkAsNoLongerNeeded();
        }
        NextModel = default;
    }

    public bool IsModelFree(in Vector3 position)
    {
        if (NextModel == default) {
            return true;
        }
        return ModelRequestPosition.DistanceToSquared(position)
            >= ModelInvalidationDistance * ModelInvalidationDistance;
    }

    public Vehicle FindProperWorldVehicleToReplace(in Vector3 position, in Vector3 velocity)
    {
        if (NextModel == default || !NextModel.IsValid || !NextModel.IsLoaded) {
            return null;
        }
        var vehicleLookupPosition = position.Around(SpawnDistance);
        if (velocity.LengthSquared() > AheadSpawnVelocityThreshold * AheadSpawnVelocityThreshold) {
            vehicleLookupPosition = position + velocity.Normalized * SpawnDistance;
        }
        var vehicle = World.GetClosestVehicle(vehicleLookupPosition, WorldVehicleLookupDistance);
        if (vehicle == null || !vehicle.Exists() || !ShouldReplaceVehicle(vehicle)) {
            return null;
        }
        if (vehicle.IsPersistent || vehicle.LodDistance < LodDistanceThreshold) {
            return null;
        }
        var driver = vehicle.Driver;
        if (driver == null || !driver.Exists() || driver.IsPlayer || driver.IsPersistent) {
            return null;
        }
        return vehicle;
    }

    public Vehicle SpawnVehicleReplacingWorldVehicle(Vehicle worldVehicle)
    {
        var driver = worldVehicle.Driver;
        driver.AlwaysKeepTask = true;
        driver.IsPersistent = true;
        var passengers = worldVehicle.Passengers;
        foreach (var passenger in passengers) {
            passenger.AlwaysKeepTask = true;
            passenger.IsPersistent = true;
        }

        var vehicle = World.CreateVehicle(NextModel, worldVehicle.Position, worldVehicle.Heading);
        vehicle.SetNoCollision(worldVehicle, true);
        vehicle.IsEngineRunning = worldVehicle.IsEngineRunning;
        vehicle.LockStatus = worldVehicle.LockStatus;
        vehicle.Velocity = worldVehicle.Velocity;
        vehicle.RotationVelocity = worldVehicle.RotationVelocity;

        driver.SetIntoVehicle(vehicle, VehicleSeat.Driver);
        driver.MarkAsNoLongerNeeded();
        driver.IsPersistent = false;
        foreach (var passenger in passengers) {
            if (!vehicle.IsSeatFree(passenger.SeatIndex)) {
                passenger.Delete();
                continue;
            }
            passenger.SetIntoVehicle(vehicle, passenger.SeatIndex);
            passenger.MarkAsNoLongerNeeded();
            passenger.IsPersistent = false;
        }

        if (AddBlips) {
            AddBlipForVehicle(vehicle, BlipColor.RedLight);
        }

        worldVehicle.Delete();
        vehicle.PlaceOnGround();
        SpawnedVehicles.Add(vehicle);
        return vehicle;
    }

    public void DespawnVehicles(in Vector3 position)
    {
        var despawnDistanceSquared = DespawnDistance * DespawnDistance;
        foreach (var vehicle in SpawnedVehicles.ToArray()) {
            if (!vehicle.Exists()) {
                SpawnedVehicles.Remove(vehicle);
                continue;
            }
            var isVehicleDead = vehicle.IsDead;
            if (isVehicleDead) {
                RemoveBlipFromVehicle(vehicle);
            }
            if (position.DistanceToSquared(vehicle.Position) > despawnDistanceSquared || isVehicleDead) {
                vehicle.MarkAsNoLongerNeeded();
                vehicle.Driver?.MarkAsNoLongerNeeded();
            }
        }
    }

    public void CheckPlayerTakesVehicle(Vehicle vehicle)
    {
        if (vehicle == null || vehicle == LastPlayerVehicle) {
            return;
        }
        if (SpawnedVehicles.Contains(vehicle)) {
            vehicle.MarkAsNoLongerNeeded();
            SpawnedVehicles.Remove(vehicle);
        }
        RemoveBlipFromVehicle(vehicle);
        LastPlayerVehicle = vehicle;
    }

    private bool GetVehicleModelNameFromSpawnpoints(VehicleSpawnpoint[] spawnpoints, out string randomModelName)
    {
        var nearbyVehicleGroups = new HashSet<VehicleGroup>();
        foreach (var spawnpoint in spawnpoints) {
            nearbyVehicleGroups.Add(spawnpoint.GroupId);
        }
        var nearbyModelNames = new HashSet<string>();
        foreach (var vehicleGroup in nearbyVehicleGroups) {
            if (!ShouldSpawnVehicleGroup(vehicleGroup)) {
                continue;
            }
            if (GroupedVehicleModels.TryGetValue(vehicleGroup, out var modelNames)) {
                foreach (var modelName in modelNames) {
                    nearbyModelNames.Add(modelName);
                }
            }
        }
        if (nearbyModelNames.Count == 0) {
            randomModelName = null;
            return false;
        }
        randomModelName = ArrayEx.Random(Random, nearbyModelNames.ToArray());
        return true;
    }

    private static bool ShouldReplaceVehicle(Vehicle vehicle)
    {
        switch (vehicle.Type) {
            case VehicleType.Plane:
            case VehicleType.Trailer:
            case VehicleType.SubmarineCar:
            case VehicleType.Helicopter:
            case VehicleType.Blimp:
            case VehicleType.Bicycle:
            case VehicleType.Boat:
            case VehicleType.Train:
            case VehicleType.Submarine:
                return false;
        }
        switch (vehicle.ClassType) {
            case VehicleClass.Emergency:
            case VehicleClass.Industrial:
            case VehicleClass.Utility:
            case VehicleClass.Cycles:
            case VehicleClass.Boats:
            case VehicleClass.Helicopters:
            case VehicleClass.Planes:
            case VehicleClass.Service:
            case VehicleClass.Military:
            case VehicleClass.Commercial:
            case VehicleClass.Trains:
            case VehicleClass.OpenWheel:
                return false;
        }
        return true;
    }

    private static bool ShouldSpawnVehicleGroup(VehicleGroup vehicleGroup)
    {
        switch (vehicleGroup) {
            case VehicleGroup.Cemetery:
            case VehicleGroup.Cheburek:
            case VehicleGroup.Cluckin:
            case VehicleGroup.Compacts:
            case VehicleGroup.Coupes:
            case VehicleGroup.Cycles:
            case VehicleGroup.Ghetto:
            case VehicleGroup.HumaneLabs:
            case VehicleGroup.Industrial:
            case VehicleGroup.MilitaryBikes:
            case VehicleGroup.Motorcycles:
            case VehicleGroup.Muscle:
            case VehicleGroup.Offroad:
            case VehicleGroup.Sedans:
            case VehicleGroup.SportClassic:
            case VehicleGroup.Supers:
            case VehicleGroup.SUVs:
            case VehicleGroup.Terrorbyte:
            case VehicleGroup.Towtruck:
            case VehicleGroup.Tuners:
            case VehicleGroup.Valentine:
            case VehicleGroup.Vans:
            case VehicleGroup.Vetir:
                return true;
            default:
                return false;
        }
    }
}
