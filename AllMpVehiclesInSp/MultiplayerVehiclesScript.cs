using GTA;
using GTA.Math;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

public class MultiplayerVehiclesScript : Script
{
    private const string ParkedVehiclesSettingsSection = "Parking";
    private const string DebugSettingsSection = "Debug";
    private const float SpawnpointActivationRadius = 300.0f;
    private const float SpawnpointDeactivationRadius = SpawnpointActivationRadius + 20.0f;
    private const int SpawnTimeout = 200;
    private const double PercentageToRatio = 1.0e-2;

    private Random Random;
    private Utilities.ScriptLog Log;
    private Dictionary<VehicleGroup, string[]> GroupedVehicleModels;
    private VehicleSpawnpointCollection SpawnpointCollection;
    private VehicleSpawnpointCollection.SearchQuery SpawnpointSearchQuery;
    private Dictionary<Vehicle, VehicleSpawnpoint> SpawnedVehicleSpawnpoints;

    private bool LockDoorsForParkedVehicles;
    private double AlarmRateForParkedVehicles;
    private bool ShowBlipsForParkedVehicles;

    private int NextSpawnTime;
    private Vehicle LastPlayerVehicle;

    public string ExcludedVehiclesRelativePath => Path.Combine(
        Path.GetFileNameWithoutExtension(Filename),
        "ExcludedVehicles.txt"
    );

    public MultiplayerVehiclesScript()
    {
        Random = new Random();
        Log = new Utilities.ScriptLog(Path.Combine(BaseDirectory, Path.ChangeExtension(Filename, ".log")));
        LockDoorsForParkedVehicles = Settings.GetValue(ParkedVehiclesSettingsSection, "LockDoors", true);
        AlarmRateForParkedVehicles = Settings.GetValue(ParkedVehiclesSettingsSection, "AlarmRatePercentage", 80.0)
            * PercentageToRatio;
        ShowBlipsForParkedVehicles = Settings.GetValue(ParkedVehiclesSettingsSection, "ShowBlips", true);
        Log.EnableDebugLogging = Settings.GetValue(DebugSettingsSection, "VerboseLogging", false);

        var excludedVehicleModels = new HashSet<string>();
        if (LoadExcludedVehicles(ExcludedVehiclesRelativePath, excludedVehicleModels)) {
            Log.Message($"{excludedVehicleModels.Count} vehicles loaded from {ExcludedVehiclesRelativePath}");
        }

        GroupedVehicleModels = VehicleModelList.All();
        SpawnedVehicleSpawnpoints = new Dictionary<Vehicle, VehicleSpawnpoint>();
        var invalidVehicleModels = new HashSet<string>();
        var totalVehicleModels = 0;
        foreach (var vehicleGroup in GroupedVehicleModels.Keys.ToArray()) {
            var vehicleModels = GroupedVehicleModels[vehicleGroup];
            AddInvalidVehicleModels(invalidVehicleModels, vehicleModels);
            excludedVehicleModels.UnionWith(invalidVehicleModels);
            vehicleModels = ArrayEx.Exclude(vehicleModels, excludedVehicleModels);
            GroupedVehicleModels[vehicleGroup] = vehicleModels;
            totalVehicleModels += vehicleModels.Length;
        }
        Log.Message(
            $"{totalVehicleModels} vehicles are available, "
            + $"{invalidVehicleModels.Count} invalid vehicles excluded"
        );

        SpawnpointCollection = new VehicleSpawnpointCollection();
        SpawnpointCollection.AddRange(VehicleSpawnpointList.All());

        var benchmark = new Utilities.Benchmark(new Stopwatch());
        var elapsedTime = benchmark.Measure(() => {
            SpawnpointSearchQuery = SpawnpointCollection.CreateSearchQuery(SpawnpointActivationRadius);
        });
        var blockMap = SpawnpointSearchQuery.BlockMap;
        Log.DebugMessage(
            $"Created a spawnpoint query blockmap {blockMap.SegmentsX}x{blockMap.SegmentsY}x{blockMap.SegmentsZ}\n"
            + $"  SegmentSize=[{blockMap.SegmentSize}]\n"
            + $"  MinimumSegmentDensity={blockMap.MinimumSegmentDensity}\n"
            + $"  MaximumSegmentDensity={blockMap.MaximumSegmentDensity}\n"
            + $"  took {FormatTimeSpan(elapsedTime)}"
        );

        NextSpawnTime = Game.GameTime;
        LastPlayerVehicle = null;

        Tick += UpdateTick;
        Aborted += CleanUp;
    }

    private void UpdateTick(object sender, EventArgs e)
    {
        if (Game.GameTime > NextSpawnTime) {
            var playerPosition = Game.Player.Character.Position;
            SpawnParkedVehicles(playerPosition, SpawnpointSearchQuery);
            DespawnParkedVehicles(playerPosition, SpawnpointDeactivationRadius);
            NextSpawnTime = Game.GameTime + SpawnTimeout;
        }
        var playerVehicle = Game.Player.Character.CurrentVehicle;
        if (playerVehicle != null && playerVehicle != LastPlayerVehicle) {
            if (SpawnedVehicleSpawnpoints.TryGetValue(playerVehicle, out var spawnpoint)) {
                spawnpoint.MarkAsTakenByPlayer();
            }
            LastPlayerVehicle = playerVehicle;
        }
    }

    private void CleanUp(object sender, EventArgs e)
    {
        foreach (var spawnpoint in SpawnedVehicleSpawnpoints.Values) {
            spawnpoint.Dispose();
        }
        SpawnedVehicleSpawnpoints.Clear();
        Log?.Dispose();
        Log = null;
    }

    private void AddInvalidVehicleModels(ISet<string> invalidNames, string[] modelNames)
    {
        foreach (var modelName in modelNames) {
            var model = new Model(modelName);
            if (!model.IsValid) {
                invalidNames.Add(modelName);
            }
        }
    }

    private void SpawnParkedVehicles(in Vector3 position, VehicleSpawnpointCollection.SearchQuery searchQuery)
    {
        var foundSpawnpoints = new List<VehicleSpawnpoint>(SpawnpointCollection.Count);
        searchQuery.FindInSphere(position, foundSpawnpoints);
        foreach (var spawnpoint in foundSpawnpoints) {
            if (spawnpoint.Model != default) {
                continue;
            }
            if (!GroupedVehicleModels.TryGetValue(spawnpoint.GroupId, out var vehicleModelNames)) {
                continue;
            }
            var modelName = ArrayEx.Random(Random, vehicleModelNames);
            if (!string.IsNullOrEmpty(modelName)) {
                spawnpoint.RequestModel(modelName);
            }
        }
        foreach (var spawnpoint in foundSpawnpoints) {
            if (spawnpoint.Vehicle == null) {
                if (spawnpoint.TrySpawnVehicle(out var vehicle)) {
                    SpawnedVehicleSpawnpoints.Add(vehicle, spawnpoint);
                    if (ShowBlipsForParkedVehicles) {
                        spawnpoint.AddBlipForVehicle();
                    }
                    if (LockDoorsForParkedVehicles) {
                        vehicle.LockStatus = VehicleLockStatus.CanBeBrokenInto;
                        vehicle.IsAlarmSet = Random.NextDouble() < AlarmRateForParkedVehicles;
                    }
                }
            }
        }
    }

    private void DespawnParkedVehicles(in Vector3 position, float radius)
    {
        var radiusSquared = radius * radius;
        foreach (var pair in SpawnedVehicleSpawnpoints.ToArray()) {
            if (position.DistanceToSquared(pair.Value.Position) > radiusSquared) {
                pair.Value.DespawnVehicle();
                SpawnedVehicleSpawnpoints.Remove(pair.Key);
            }
        }
    }

    private bool LoadExcludedVehicles(string localFilename, HashSet<string> nameSet)
    {
        string[] lines;
        try {
            lines = File.ReadAllLines(Path.Combine(BaseDirectory, localFilename));
        } catch (IOException exception) {
            Log.ErrorMessage($"Failed to read {localFilename}\n  {exception.Message}");
            return false;
        }
        foreach (var line in lines) {
            var name = line.Trim();
            var commentIndex = name.IndexOf('#');
            if (commentIndex >= 0) {
                name = name.Substring(0, commentIndex).Trim();
            }
            if (!string.IsNullOrEmpty(name)) {
                nameSet.Add(name);
            }
        }
        return true;
    }

    private string FormatTimeSpan(in TimeSpan timeSpan)
    {
        const double NanosecondsPerMillisecond = 1.0e6;
        const double MicrosecondsPerMillisecond = 1.0e3;

        var milliseconds = (double)timeSpan.Ticks / TimeSpan.TicksPerMillisecond;
        if (milliseconds < 1.0e-3) {
            return $"{milliseconds * NanosecondsPerMillisecond:n0} ns";
        } else if (milliseconds < 1.0) {
            return $"{milliseconds * MicrosecondsPerMillisecond:n1} μs";
        } else {
            return $"{milliseconds:n3} ms";
        }
    }
}
