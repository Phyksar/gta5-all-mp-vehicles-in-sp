using GTA;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Utilities;

public class MultiplayerVehiclesScript : Script
{
    private const string ParkingSettingsSection = "Parking";
    private const string DebugSettingsSection = "Debug";

    private const float SpawnRate = 5.0f;

    private ParkedVehicleSpawner ParkedVehicleSpawner;

    public string ExcludedVehiclesRelativePath => Path.Combine(
        Path.GetFileNameWithoutExtension(Filename),
        "ExcludedVehicles.txt"
    );

    public MultiplayerVehiclesScript()
    {
        const float PercentageToRatio = 1.0e-2f;

        ScriptLog.Open(Path.Combine(BaseDirectory, Path.ChangeExtension(Filename, ".log")));
        ScriptLog.EnableDebugLogging = Settings.GetValue(DebugSettingsSection, "VerboseLogging", false);

        var excludedVehicleModels = new HashSet<string>();
        if (LoadExcludedVehicles(ExcludedVehiclesRelativePath, BaseDirectory, excludedVehicleModels)) {
            ScriptLog.Message($"{excludedVehicleModels.Count} vehicles loaded from {ExcludedVehiclesRelativePath}");
        }

        var groupedVehicleModels = VehicleModelList.All();
        var invalidVehicleModels = new HashSet<string>();
        var totalVehicleModels = 0;
        foreach (var vehicleGroup in groupedVehicleModels.Keys.ToArray()) {
            var vehicleModels = groupedVehicleModels[vehicleGroup];
            AddInvalidVehicleModels(invalidVehicleModels, vehicleModels);
            excludedVehicleModels.UnionWith(invalidVehicleModels);
            vehicleModels = ArrayEx.Exclude(vehicleModels, excludedVehicleModels);
            groupedVehicleModels[vehicleGroup] = vehicleModels;
            totalVehicleModels += vehicleModels.Length;
        }
        ScriptLog.Message(
            $"{totalVehicleModels} vehicles are available, "
            + $"{invalidVehicleModels.Count} invalid vehicles excluded"
        );

        var minSpawnDistance = Settings.GetValue(DebugSettingsSection, "MinSpawnDistance", 300.0f);
        var maxSpawnDistance = Settings.GetValue(DebugSettingsSection, "MaxSpawnDistance", 500.0f);
        var despawnDistance = Settings.GetValue(DebugSettingsSection, "DespawnDistance", maxSpawnDistance + 20.0f);
        var benchmark = new Benchmark(new Stopwatch());
        var spawnpointCollection = new VehicleSpawnpointCollection();
        spawnpointCollection.AddRange(VehicleSpawnpointList.All());
        var elapsedTime = benchmark.Measure(out var parkedSpawnpointSearchQuery, () => {
            return spawnpointCollection.CreateSearchQuery(maxSpawnDistance);
        });
        LogBlockMapStatistics(parkedSpawnpointSearchQuery.BlockMap, elapsedTime);

        var random = new Random();
        ParkedVehicleSpawner = new ParkedVehicleSpawner(
            random,
            parkedSpawnpointSearchQuery,
            groupedVehicleModels,
            minSpawnDistance,
            despawnDistance,
            Settings.GetValue(ParkingSettingsSection, "ShowBlips", true),
            Settings.GetValue(ParkingSettingsSection, "LockDoors", true),
            Settings.GetValue(ParkingSettingsSection, "AlarmRatePercentage", 80.0f) * PercentageToRatio
        );

        Tick += CreateRateLimitedListener(SpawnParkedVehicles, SpawnRate);
        Tick += CheckPlayerTakesVehicle;
        Aborted += CleanUp;
    }

    private void SpawnParkedVehicles(object sender, EventArgs e)
    {
        var playerPosition = Game.Player.Character.Position;
        ParkedVehicleSpawner.DespawnVehicles(playerPosition);
        ParkedVehicleSpawner.SpawnVehicles(playerPosition);
    }

    private void CheckPlayerTakesVehicle(object sender, EventArgs e)
    {
        ParkedVehicleSpawner.CheckPlayerTakesVehicle(Game.Player.Character.CurrentVehicle);
    }

    private void CleanUp(object sender, EventArgs e)
    {
        ParkedVehicleSpawner?.Dispose();
        ParkedVehicleSpawner = null;
        ScriptLog.Close();
    }

    private static void AddInvalidVehicleModels(ISet<string> invalidNames, string[] modelNames)
    {
        foreach (var modelName in modelNames) {
            var model = new Model(modelName);
            if (!model.IsValid) {
                invalidNames.Add(modelName);
            }
        }
    }

    private static bool LoadExcludedVehicles(string localFilename, string baseDirectory, HashSet<string> nameSet)
    {
        string[] lines;
        try {
            lines = File.ReadAllLines(Path.Combine(baseDirectory, localFilename));
        } catch (IOException exception) {
            ScriptLog.ErrorMessage($"Failed to read {localFilename}\n  {exception.Message}");
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

    private static EventHandler CreateRateLimitedListener(EventHandler handler, float callRate)
    {
        const int MillisecondsPerSecond = 1000;

        var callInterval = (int)(MillisecondsPerSecond / callRate);
        var nextCallTime = (new Random()).Next(0, callInterval);
        return (object sender, EventArgs e) => {
            var gameTime = Game.GameTime;
            if (gameTime > nextCallTime) {
                handler(sender, e);
                nextCallTime = gameTime + callInterval;
            }
        };
    }

    private static void LogBlockMapStatistics<T>(BlockMap3<T> blockMap, in TimeSpan elapsedTime) where T : IPosition3
    {
        ScriptLog.DebugMessage(
            $"Created a spawnpoint query blockmap {blockMap.SegmentsX}x{blockMap.SegmentsY}x{blockMap.SegmentsZ}\n"
            + $"  SegmentSize=[{blockMap.SegmentSize}]\n"
            + $"  MinSegmentDensity={blockMap.MinSegmentDensity}\n"
            + $"  MaxSegmentDensity={blockMap.MaxSegmentDensity}\n"
            + $"  took {FormatTimeSpan(elapsedTime)}"
        );
    }

    private static string FormatTimeSpan(in TimeSpan timeSpan)
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
