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
    private const string TrafficSettingsSection = "Traffic";
    private const string DebugSettingsSection = "Debug";

    private const float SpawnRate = 5.0f;

    private Random Random;
    private Benchmark Benchmark;
    private ParkedVehicleSpawner ParkedVehicleSpawner;
    private TrafficVehicleSpawner TrafficVehicleSpawner;

    private int MaxTrafficVehicles;
    private int MinTrafficSpawnMilliseconds;
    private int MaxTrafficSpawnMilliseconds;
    private int NextTrafficSpawnTime;

    public string ExcludedVehiclesRelativePath => Path.Combine(
        Path.GetFileNameWithoutExtension(Filename),
        "ExcludedVehicles.txt"
    );

    public MultiplayerVehiclesScript()
    {
        const float PercentageToRatio = 1.0e-2f;
        const int MillisecondsPerSecond = 1000;

        Random = new Random();
        Benchmark = new Benchmark(new Stopwatch());
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

        var minSpawnDistance = Settings.GetValue(DebugSettingsSection, "MinSpawnDistance", 200.0f);
        var maxSpawnDistance = Settings.GetValue(DebugSettingsSection, "MaxSpawnDistance", 300.0f);
        var despawnDistance = Settings.GetValue(DebugSettingsSection, "DespawnDistance", maxSpawnDistance + 20.0f);
        var spawnpointCollection = new VehicleSpawnpointCollection();
        spawnpointCollection.AddRange(VehicleSpawnpointList.All());
        var elapsedTime = Benchmark.Measure(out var parkedSpawnpointSearchQuery, () => {
            return spawnpointCollection.CreateSearchQuery(maxSpawnDistance);
        });
        LogBlockMapStatistics(parkedSpawnpointSearchQuery.BlockMap, elapsedTime);

        var localTrafficDistance = Settings.GetValue(TrafficSettingsSection, "LocalTrafficDistance", 1000.0f);
        elapsedTime = Benchmark.Measure(out var trafficSpawnpointSearchQuery, () => {
            return spawnpointCollection.CreateSearchQuery(localTrafficDistance);
        });
        LogBlockMapStatistics(parkedSpawnpointSearchQuery.BlockMap, elapsedTime);

        ParkedVehicleSpawner = new ParkedVehicleSpawner(
            Random,
            parkedSpawnpointSearchQuery,
            groupedVehicleModels,
            minSpawnDistance,
            despawnDistance,
            Settings.GetValue(ParkingSettingsSection, "ShowBlips", true),
            Settings.GetValue(ParkingSettingsSection, "LockDoors", true),
            Settings.GetValue(ParkingSettingsSection, "AlarmRatePercentage", 80.0f) * PercentageToRatio
        );

        MaxTrafficVehicles = Settings.GetValue(TrafficSettingsSection, "MaxVehicles", 5);
        MinTrafficSpawnMilliseconds = (int)
            (Settings.GetValue(TrafficSettingsSection, "MinSpawnSeconds", 10.0f) * MillisecondsPerSecond);
        MaxTrafficSpawnMilliseconds = (int)
            (Settings.GetValue(TrafficSettingsSection, "MaxSpawnSeconds", 60.0f) * MillisecondsPerSecond);
        TrafficVehicleSpawner = new TrafficVehicleSpawner(
            Random,
            trafficSpawnpointSearchQuery,
            groupedVehicleModels,
            minSpawnDistance,
            despawnDistance,
            Settings.GetValue(TrafficSettingsSection, "ModelInvalidationDistance", 500.0f),
            Settings.GetValue(TrafficSettingsSection, "ShowBlips", true)
        );

        NextTrafficSpawnTime = Game.GameTime + Random.Next(MinTrafficSpawnMilliseconds, MaxTrafficSpawnMilliseconds);
        ScriptLog.DebugMessage($"Next traffic spawn time is {LogFile.FormatGameTime(NextTrafficSpawnTime)}");

        Tick += CreateRateLimitedListener(SpawnParkedVehicles, SpawnRate);
        Tick += CreateRateLimitedListener(SpawnTrafficVehicles, SpawnRate);
        Tick += CheckPlayerTakesVehicle;
        Aborted += CleanUp;
    }

    private void SpawnParkedVehicles(object sender, EventArgs e)
    {
        var playerPosition = Game.Player.Character.Position;
        ParkedVehicleSpawner.DespawnVehicles(playerPosition);
        ParkedVehicleSpawner.SpawnVehicles(playerPosition);
    }

    private void SpawnTrafficVehicles(object sender, EventArgs e)
    {
        var gameTime = Game.GameTime;
        var playerPosition = Game.Player.Character.Position;
        TrafficVehicleSpawner.DespawnVehicles(Game.Player.Character.Position);
        if (TrafficVehicleSpawner.IsModelFree(playerPosition)) {
            TrafficVehicleSpawner.TryRequestModel(playerPosition);
        }
        if (gameTime > NextTrafficSpawnTime && TrafficVehicleSpawner.IsModelAvailable) {
            Vehicle vehicle = null;
            int worldVehiclePassengers = 0;
            TimeSpan elapsedTime = new TimeSpan(0);
            if (TrafficVehicleSpawner.TotalVehicles < MaxTrafficVehicles) {
                var worldVehicle = TrafficVehicleSpawner.FindProperWorldVehicleToReplace(
                    playerPosition,
                    Game.Player.Character.Velocity
                );
                if (worldVehicle != null) {
                    worldVehiclePassengers = worldVehicle.PassengerCount;
                    elapsedTime = Benchmark.Measure(out vehicle, () => {
                        return TrafficVehicleSpawner.SpawnVehicleReplacingWorldVehicle(worldVehicle);
                    });
                }
            }
            if (vehicle != null) {
                TrafficVehicleSpawner.FreeModel();
                NextTrafficSpawnTime = gameTime + Random.Next(MinTrafficSpawnMilliseconds, MaxTrafficSpawnMilliseconds);
                ScriptLog.DebugMessage(
                    $"Traffic vehicle 0x{vehicle.Handle:x8} spawned, took {FormatTimeSpan(elapsedTime)}\n"
                    + $"{vehicle.PassengerCount} of {worldVehiclePassengers} passengers was transferred\n"
                    + $"Next traffic spawn time is {LogFile.FormatGameTime(NextTrafficSpawnTime)}"
                );
            }
        }
    }

    private void CheckPlayerTakesVehicle(object sender, EventArgs e)
    {
        var playerVehicle = Game.Player.Character.CurrentVehicle;
        ParkedVehicleSpawner.CheckPlayerTakesVehicle(playerVehicle);
        TrafficVehicleSpawner.CheckPlayerTakesVehicle(playerVehicle);
    }

    private void CleanUp(object sender, EventArgs e)
    {
        ParkedVehicleSpawner?.Dispose();
        ParkedVehicleSpawner = null;
        TrafficVehicleSpawner?.Dispose();
        TrafficVehicleSpawner = null;
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

    private EventHandler CreateRateLimitedListener(EventHandler handler, float callRate)
    {
        const int MillisecondsPerSecond = 1000;

        var callInterval = (int)(MillisecondsPerSecond / callRate);
        var nextCallTime = Random.Next(0, callInterval);
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
