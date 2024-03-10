using GTA.Math;
using System;
using System.Collections.Generic;

namespace Utilities
{
    public class BlockMap3<T> where T : IPosition3
    {
        public Vector3 Mins { get; private set; }
        public Vector3 Maxs { get; private set; }
        public int SegmentsX { get; private set; }
        public int SegmentsY { get; private set; }
        public int SegmentsZ { get; private set; }

        public Vector3 SegmentSize { get; private set; }
        public int MinSegmentDensity { get; private set; }
        public int MaxSegmentDensity { get; private set; }

        private int LastSegmentX;
        private int LastSegmentY;
        private int LastSegmentZ;
        private Vector3 CoordinateScale;
        private ArraySegment<T>[] Segments;

        public ArraySegment<T> this[int index] => Segments[index];
        public ArraySegment<T> this[in Vector3 position] => this[IndexPosition(position)];

        public BlockMap3(in Vector3 mins, in Vector3 maxs, int segmentsX, int segmentsY, int segmentsZ)
        {
            var range = maxs - mins;
            Mins = mins;
            Maxs = maxs;
            SegmentsX = segmentsX;
            SegmentsY = segmentsY;
            SegmentsZ = segmentsZ;
            SegmentSize = new Vector3(range.X / segmentsX, range.Y / segmentsY, range.Z / segmentsZ);
            MinSegmentDensity = 0;
            MaxSegmentDensity = 0;
            LastSegmentX = segmentsX - 1;
            LastSegmentY = segmentsY - 1;
            LastSegmentZ = segmentsZ - 1;
            CoordinateScale = new Vector3(
                (segmentsX + 1) / range.X,
                (segmentsY + 1) / range.Y,
                (segmentsZ + 1) / range.Z
            );
            Segments = Array.Empty<ArraySegment<T>>();
        }

        public void Build(in T[] elements, in Vector3 extends)
        {
            int NumberOfSegments = SegmentsX * SegmentsY * SegmentsZ;
            var elementList = new List<T>();
            var segmentLengths = new int[NumberOfSegments];
            for (var segmentIndex = 0; segmentIndex < NumberOfSegments; segmentIndex++) {
                ComputeSegmentBounds(segmentIndex, out var segmentMins, out var segmentMaxs);
                segmentMins -= extends;
                segmentMaxs += extends;
                var segmentLength = 0;
                foreach (var element in elements) {
                    if (IsPointIntersectsBox(element.Position, segmentMins, segmentMaxs)) {
                        elementList.Add(element);
                        segmentLength++;
                    }
                }
                segmentLengths[segmentIndex] = segmentLength;
            }
            var segmentStart = 0;
            var elementPool = elementList.ToArray();
            var segments = new ArraySegment<T>[NumberOfSegments];
            var minSegmentElements = segments[0].Count;
            var maxSegmentElements = segments[0].Count;
            for (var segmentIndex = 0; segmentIndex < NumberOfSegments; segmentIndex++) {
                segments[segmentIndex] = new ArraySegment<T>(elementPool, segmentStart, segmentLengths[segmentIndex]);
                segmentStart += segmentLengths[segmentIndex];
                minSegmentElements = Math.Min(segmentLengths[segmentIndex], minSegmentElements);
                maxSegmentElements = Math.Max(segmentLengths[segmentIndex], maxSegmentElements);
            }
            Segments = segments;
            MinSegmentDensity = minSegmentElements;
            MaxSegmentDensity = maxSegmentElements;
        }

        public int IndexPosition(in Vector3 position)
        {
            var coordinates = Vector3.Multiply(position - Mins, CoordinateScale);
            return MathEx.Clamp((int)coordinates.X, 0, LastSegmentX)
                + (
                    MathEx.Clamp((int)coordinates.Y, 0, LastSegmentY)
                        + MathEx.Clamp((int)coordinates.Z, 0, LastSegmentZ)
                        * SegmentsY
                )
                    * SegmentsX;
        }

        private void ComputeSegmentBounds(int index, out Vector3 mins, out Vector3 maxs)
        {
            var segmentCoordinates = new Vector3(
                index % SegmentsX,
                (index / SegmentsX) % SegmentsY,
                (index / (SegmentsX * SegmentsY)) % SegmentsZ
            );
            mins = Vector3.Multiply(segmentCoordinates, SegmentSize) + Mins;
            maxs = mins + SegmentSize;
        }

        private static bool IsPointIntersectsBox(in Vector3 point, in Vector3 boxMins, in Vector3 boxMaxs)
        {
            return (point.X >= boxMins.X && point.X <= boxMaxs.X)
                && (point.Y >= boxMins.Y && point.Y <= boxMaxs.Y)
                && (point.Z >= boxMins.Z && point.Z <= boxMaxs.Z);
        }
    }
}
