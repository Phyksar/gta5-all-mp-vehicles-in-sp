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
    private HashSet<VehicleSpawnpoint> ActiveVehicleSpawnpoints;
    private Dictionary<int, VehicleSpawnpoint> VehiclesToSpawnpoints;
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
        ActiveVehicleSpawnpoints = new HashSet<VehicleSpawnpoint>();
        VehiclesToSpawnpoints = new Dictionary<int, VehicleSpawnpoint>();
        LastPlayerVehicle = null;
        MinSpawnDistance = minSpawnDistance;
        DespawnDistance = despawnDistance;
        AddBlips = addBlips;
        LockDoors = lockDoors;
        AlarmRate = alarmRate;
    }

    public void Dispose()
    {
        foreach (var spawnpoint in ActiveVehicleSpawnpoints) {
            spawnpoint.Dispose();
        }
        ActiveVehicleSpawnpoints.Clear();
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
            ScriptLog.DebugMessage($"Model {modelName} requested for spawnpoint 0x{spawnpoint.GetHashCode():x8}");
        }
        var minSpawnDistanceSquared = MinSpawnDistance * MinSpawnDistance;
        foreach (var spawnpoint in spawnpoints) {
            if (maxSpawns <= 0) {
                break;
            }
            if (
                spawnpoint.Vehicle != null
                || !spawnpoint.IsModelAvailable
                || ActiveVehicleSpawnpoints.Contains(spawnpoint)
                || position.DistanceToSquared(spawnpoint.Position) < minSpawnDistanceSquared
            ) {
                continue;
            }
            var vehicle = TrySpawnVehicle(spawnpoint.Model, spawnpoint.Position, spawnpoint.Heading);
            spawnpoint.Vehicle = vehicle;
            maxSpawns--;
            ActiveVehicleSpawnpoints.Add(spawnpoint);
            if (vehicle == null) {
                ScriptLog.DebugMessage(
                    $"Failed to spawn vehicle at spawnpoint 0x{spawnpoint.GetHashCode():x8}, "
                    + $"potentially occupied position at [{spawnpoint.Position}]"
                );
                continue;
            }

            spawnpoint.FreeModel();
            VehiclesToSpawnpoints.Add(vehicle.Handle, spawnpoint);
            if (AddBlips) {
                AddBlipForVehicle(vehicle, BlipColor.WhiteNotPure);
            }
            if (LockDoors) {
                vehicle.LockStatus = VehicleLockStatus.CanBeBrokenInto;
                vehicle.IsAlarmSet = Random.NextDouble() < AlarmRate;
                vehicle.NeedsToBeHotwired = true;
            }
            ScriptLog.DebugMessage(
                $"Vehicle spawned at spawnpoint 0x{spawnpoint.GetHashCode():x8}\n"
                + $"  {ActiveVehicleSpawnpoints.Count} active vehicle spawnpoints\n"
                + $"  {VehiclesToSpawnpoints.Count} vehicles mapped to spawnpoints"
            );
        }
    }

    public void FreeVehicles(in Vector3 position)
    {
        var distanceSquared = DespawnDistance * DespawnDistance;
        foreach (var spawnpoint in ActiveVehicleSpawnpoints.ToArray()) {
            var isVehicleDead = (spawnpoint.Vehicle?.Exists() ?? false) && spawnpoint.Vehicle.IsDead;
            if (isVehicleDead) {
                RemoveBlipFromVehicle(spawnpoint.Vehicle);
            }
            if (isVehicleDead || position.DistanceToSquared(spawnpoint.Position) > distanceSquared) {
                if (spawnpoint.Vehicle != null) {
                    VehiclesToSpawnpoints.Remove(spawnpoint.Vehicle.Handle);
                }
                ActiveVehicleSpawnpoints.Remove(spawnpoint);
                spawnpoint.FreeVehicle();
                spawnpoint.FreeModel();
            }
        }
    }

    public void CheckPlayerTakesVehicle(Vehicle vehicle)
    {
        if (vehicle == null || vehicle == LastPlayerVehicle) {
            return;
        }
        if (VehiclesToSpawnpoints.TryGetValue(vehicle.Handle, out var spawnpoint)) {
            VehiclesToSpawnpoints.Remove(vehicle.Handle);
            spawnpoint.MarkAsTakenByPlayer();
            vehicle.MarkAsNoLongerNeeded();
        }
        RemoveBlipFromVehicle(vehicle);
        LastPlayerVehicle = vehicle;
    }
}
