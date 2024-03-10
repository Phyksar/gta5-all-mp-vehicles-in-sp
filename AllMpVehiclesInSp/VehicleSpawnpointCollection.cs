using GTA.Math;
using System.Collections.Generic;
using Utilities;

public class VehicleSpawnpointCollection
{
    private const int MinSegments = 1;
    private const int MaxSegments = 64;

    public int Count => Spawnpoints.Count;

    private List<VehicleSpawnpoint> Spawnpoints = new List<VehicleSpawnpoint>();

    public VehicleSpawnpointCollection AddRange(VehicleSpawnpointDesc[] spawnpoints)
    {
        foreach (var description in spawnpoints) {
            Spawnpoints.Add(new VehicleSpawnpoint(description));
        }
        return this;
    }

    public SearchQuery CreateSearchQuery(float radius)
    {
        ComputeBounds(out var mins, out var maxs);
        var segments = Vector3.Divide(maxs - mins, radius);
        return new SearchQuery(
            Spawnpoints.ToArray(),
            radius,
            mins,
            maxs,
            MathEx.Clamp((int)segments.X, MinSegments, MaxSegments),
            MathEx.Clamp((int)segments.Y, MinSegments, MaxSegments),
            MathEx.Clamp((int)segments.Z, MinSegments, MaxSegments)
        );
    }

    private void ComputeBounds(out Vector3 mins, out Vector3 maxs)
    {
        if (Spawnpoints.Count == 0) {
            mins = Vector3.Zero;
            maxs = Vector3.Zero;
            return;
        }
        mins = Spawnpoints[0].Position;
        maxs = mins;
        foreach (var spawnpoint in Spawnpoints) {
            mins = Vector3.Minimize(mins, spawnpoint.Position);
            maxs = Vector3.Maximize(maxs, spawnpoint.Position);
        }
    }

    public class SearchQuery : ISearchQuery<VehicleSpawnpoint>
    {
        private float Radius;

        public BlockMap3<VehicleSpawnpoint> BlockMap { get; private set; }

        public SearchQuery(
            VehicleSpawnpoint[] spawnpoints,
            float radius,
            in Vector3 mins,
            in Vector3 maxs,
            int segmentsX,
            int segmentsY,
            int segmentsZ)
        {
            Radius = radius;
            BlockMap = new BlockMap3<VehicleSpawnpoint>(mins, maxs, segmentsX, segmentsY, segmentsZ);
            BlockMap.Build(spawnpoints, BlockMap.SegmentSize);
        }

        public VehicleSpawnpoint[] FindInSphere(in Vector3 center)
        {
            var radiusSquared = Radius * Radius;
            var spawnpointList = new List<VehicleSpawnpoint>();
            foreach (var element in BlockMap[center]) {
                if (center.DistanceToSquared(element.Position) < radiusSquared) {
                    spawnpointList.Add(element);
                }
            }
            return spawnpointList.ToArray();
        }
    }

}
