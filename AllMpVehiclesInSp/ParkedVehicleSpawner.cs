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

    private float MinSpawnDistance;
    private float DespawnDistance;
    private bool AddBlips;
    private bool LockDoors;
    private float AlarmRate;

    public ParkedVehicleSpawner(
        Random random,
        ISearchQuery<VehicleSpawnpoint> spawnpointSearchQuery,
        IReadOnlyDictionary<VehicleGroup, string[]> groupedVehicleModels,
        float minSpawnDistance,
        float despawnDistance,
        bool addBlips,
        bool lockDoors,
        float alarmRate)
    {
        Random = random;
        SpawnpointSearchQuery = spawnpointSearchQuery;
        GroupedVehicleModels = groupedVehicleModels;
        SpawnedVehicleSpawnpoints = new Dictionary<int, VehicleSpawnpoint>();
        LastPlayerVehicle = null;
        MinSpawnDistance = minSpawnDistance;
        DespawnDistance = despawnDistance;
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

    public void SpawnVehicles(in Vector3 position, int maxSpawns = 1)
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
        var minSpawnDistanceSquared = MinSpawnDistance * MinSpawnDistance;
        foreach (var spawnpoint in spawnpoints) {
            if (spawnpoint.Vehicle != null || !spawnpoint.IsModelAvailable || spawnpoint.WasOccupied) {
                continue;
            }
            if (position.DistanceToSquared(spawnpoint.Position) < minSpawnDistanceSquared) {
                continue;
            }
            var vehicle = TrySpawnVehicle(spawnpoint.Model, spawnpoint.Position, spawnpoint.Heading);
            if (vehicle == null) {
                spawnpoint.MarkAsOccupied();
                ScriptLog.DebugMessage(
                    $"Failed to spawn vehicle at spawnpoint 0x{spawnpoint.GetHashCode():x8}, "
                    + $"potentially occupied position at [{spawnpoint.Position}]"
                );
                continue;
            }
            if (--maxSpawns < 0) {
                return;
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
        var distanceSquared = DespawnDistance * DespawnDistance;
        foreach (var pair in SpawnedVehicleSpawnpoints.ToArray()) {
            if (position.DistanceToSquared(pair.Value.Position) > distanceSquared) {
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
