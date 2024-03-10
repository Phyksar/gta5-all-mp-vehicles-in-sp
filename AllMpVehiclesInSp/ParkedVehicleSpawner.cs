using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Linq;

public class ParkedVehicleSpawner : VehicleSpawner, IDisposable
{
    private Random Random;
    private ISearchQuery<VehicleSpawnpoint> SpawnpointSearchQuery;
    private IReadOnlyDictionary<VehicleGroup, string[]> GroupedVehicleModels;
    private Dictionary<int, VehicleSpawnpoint> SpawnedVehicleSpawnpoints;
    private Vehicle LastPlayerVehicle;

    private float MinSpawnRadius;
    private float DespawnRadius;
    private bool AddBlips;
    private bool LockDoors;
    private float AlarmRate;

    public ParkedVehicleSpawner(
        Random random,
        ISearchQuery<VehicleSpawnpoint> spawnpointSearchQuery,
        IReadOnlyDictionary<VehicleGroup, string[]> groupedVehicleModels,
        float minSpawnRadius,
        float despawnRadius,
        bool addBlips,
        bool lockDoors,
        float alarmRate)
    {
        Random = random;
        SpawnpointSearchQuery = spawnpointSearchQuery;
        GroupedVehicleModels = groupedVehicleModels;
        SpawnedVehicleSpawnpoints = new Dictionary<int, VehicleSpawnpoint>();
        LastPlayerVehicle = null;
        MinSpawnRadius = minSpawnRadius;
        DespawnRadius = despawnRadius;
        AddBlips = addBlips;
        LockDoors = lockDoors;
        AlarmRate = alarmRate;
    }

    public void Dispose()
    {
        foreach (var spawnpoint in SpawnedVehicleSpawnpoints.Values) {
            spawnpoint.Dispose();
        }
        SpawnedVehicleSpawnpoints.Clear();
    }

    public void SpawnVehicles(in Vector3 position)
    {
        var spawnpoints = SpawnpointSearchQuery.FindInSphere(position);
        foreach (var spawnpoint in spawnpoints) {
            if (spawnpoint.Model != default) {
                continue;
            }
            if (!GroupedVehicleModels.TryGetValue(spawnpoint.GroupId, out var vehicleModelNames)) {
                continue;
            }
            if (vehicleModelNames.Length == 0) {
                continue;
            }
            var modelName = ArrayEx.Random(Random, vehicleModelNames);
            spawnpoint.RequestModel(modelName);
            ScriptLog.DebugMessage($"Model {modelName} requested for spawnoint 0x{spawnpoint.GetHashCode():x8}");
        }
        var minSpawnRadiusSquared = MinSpawnRadius * MinSpawnRadius;
        foreach (var spawnpoint in spawnpoints) {
            if (spawnpoint.Vehicle != null || !spawnpoint.IsModelAvailable) {
                continue;
            }
            if (position.DistanceToSquared(spawnpoint.Position) < minSpawnRadiusSquared) {
                continue;
            }
            var vehicle = TrySpawnVehicle(spawnpoint.Model, spawnpoint.Position, spawnpoint.Heading);
            if (vehicle == null) {
                ScriptLog.DebugMessage(
                    $"Failed to spawn vehicle at spawnpoint 0x{spawnpoint.GetHashCode():x8}, "
                    + $"potentially occupied position at [{spawnpoint.Position}]"
                );
                continue;
            }
            spawnpoint.Vehicle = vehicle;
            SpawnedVehicleSpawnpoints.Add(vehicle.Handle, spawnpoint);
            if (AddBlips) {
                AddBlipForVehicle(vehicle, BlipColor.WhiteNotPure);
            }
            if (LockDoors) {
                vehicle.LockStatus = VehicleLockStatus.CanBeBrokenInto;
                vehicle.IsAlarmSet = Random.NextDouble() < AlarmRate;
                vehicle.NeedsToBeHotwired = true;
            }
            ScriptLog.DebugMessage($"Vehicle spawned at spawnpoint 0x{spawnpoint.GetHashCode():x8}");
        }
    }

    public void DespawnVehicles(in Vector3 position)
    {
        var radiusSquared = DespawnRadius * DespawnRadius;
        foreach (var pair in SpawnedVehicleSpawnpoints.ToArray()) {
            if (position.DistanceToSquared(pair.Value.Position) > radiusSquared) {
                pair.Value.DespawnVehicle();
                SpawnedVehicleSpawnpoints.Remove(pair.Key);
            }
        }
    }

    public void CheckPlayerTakesVehicle(Vehicle vehicle)
    {
        if (vehicle != null && vehicle != LastPlayerVehicle) {
            if (SpawnedVehicleSpawnpoints.TryGetValue(vehicle.Handle, out var spawnpoint)) {
                spawnpoint.MarkAsTakenByPlayer();
                vehicle.MarkAsNoLongerNeeded();
            }
            RemoveBlipFromVehicle(vehicle);
            LastPlayerVehicle = vehicle;
        }
    }
}
