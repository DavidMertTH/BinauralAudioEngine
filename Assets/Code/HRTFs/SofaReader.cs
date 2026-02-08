using System;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Code
{
    internal enum CoordType
    {
        Cartesian,
        Spherical
    }

    public static class SofaReader
    {
        public static HeadRelatedImpulseResponses Read(string path)
        {
            var pointer = mysofa_load(path, out var err);
            if (err != 0)
                throw new FileLoadException($"Error loading SOFA file '{path}': Error code {err}");
            var file = Marshal.PtrToStructure<MYSOFA_HRIR>(pointer);
            var hrirs = ExtractHrirs(file.DataIR);
            var positions = ExtractSourcePositions(file.SourcePosition);
            return new HeadRelatedImpulseResponses(
                impulseResponses: hrirs,
                positions: positions,
                stride: (int)file.N);
        }

        private static CoordType IdentifyCoordType(MYSOFA_ARRAY array)
        {
            var ptr = array.attributes;
            while (ptr != IntPtr.Zero)
            {
                var attr = Marshal.PtrToStructure<MYSOFA_ATTRIBUTE>(ptr);
                var name = Marshal.PtrToStringUTF8(attr.name);
                if (name != null && name.ToLowerInvariant() == "type")
                {
                    var value = Marshal.PtrToStringUTF8(attr.value).ToLowerInvariant();
                    return value switch
                    {
                        "cartesian" => CoordType.Cartesian,
                        "spherical" => CoordType.Spherical,
                        _ => throw new ArgumentException("Unknown coord type: " + value)
                    };
                }

                ptr = attr.next;
            }

            Debug.Log("Coordinate type not specified in SOFA file. Assuming default (cartesian)");
            return CoordType.Cartesian;
        }

        private static NativeArray<float3> ExtractSourcePositions(MYSOFA_ARRAY array)
        {
            var positions = PtrToArray(array.values, (int)array.elements);
            var coordType = IdentifyCoordType(array);
            var ret = new NativeArray<float3>(positions.Length / 3, Allocator.Persistent);
            for (var i = 0; i < positions.Length; i += 3)
            {
                switch (coordType)
                {
                    case CoordType.Cartesian:
                        ret[i / 3] = new float3(positions[i], positions[i + 1], positions[i + 2]);
                        break;
                    case CoordType.Spherical:
                        var azimuth = math.radians(positions[i]);
                        var elevation = math.radians(positions[i + 1]);
                        var radius = positions[i + 2];
                        var cartesian = PolarToCartesian(azimuth, elevation, radius);
                        ret[i / 3] = cartesian;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            return ret;
        }

        private static NativeArray<float> ExtractHrirs(MYSOFA_ARRAY array)
        {
            var arr = PtrToArray(array.values, (int)array.elements);
            return new NativeArray<float>(arr, Allocator.Persistent);
        }

        private static float3 PolarToCartesian(float azimuth, float elevation, float radius)
        {
            var cosEl = math.cos(elevation);
            var sinEl = math.sin(elevation);

            var sinAz = math.sin(azimuth);
            var cosAz = math.cos(azimuth);

            var x = cosEl * sinAz;
            var y = sinEl;
            var z = cosEl * cosAz;

            return new float3(x, y, z) * radius;
        }

        private static float[] PtrToArray(IntPtr pointer, int elements)
        {
            var ret = new float[elements];
            Marshal.Copy(pointer, ret, 0, elements);
            return ret;
        }

        [DllImport("hrtf_import")]
        public static extern IntPtr mysofa_load(string filename, out int err);
    }

    public struct HeadRelatedImpulseResponses : IDisposable
    {
        private NativeArray<float> _impulseResponses;
        public NativeArray<float>.ReadOnly ImpulseResponses => _impulseResponses.AsReadOnly();
        private NativeArray<float3> _positions;
        public NativeArray<float3>.ReadOnly Positions => _positions.AsReadOnly();
        public readonly int Stride;

        public HeadRelatedImpulseResponses(NativeArray<float> impulseResponses,
            NativeArray<float3> positions, int stride)
        {
            _impulseResponses = impulseResponses;
            _positions = positions;
            Stride = stride;
        }

        public void Dispose()
        {
            _impulseResponses.Dispose();
            _positions.Dispose();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MYSOFA_ATTRIBUTE
    {
        public IntPtr next; // Pointer to the next attribute
        public IntPtr name; // Pointer to a character string
        public IntPtr value; // Pointer to a character string
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MYSOFA_ARRAY
    {
        public IntPtr values; // Pointer to an array of floats
        public uint elements; // Number of elements in the array
        public IntPtr attributes; // Pointer to a MYSOFA_ATTRIBUTE
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MYSOFA_HRIR
    {
        /* Dimensions defined in AES69
        M Number of measurements; must be integer greater than zero.
        R Number of receivers; must be integer greater than zero.
        E Number of emitters; must be integer greater than zero.
        N Number of data samples describing one measurement; must be integer greater than zero.
        S Number of characters in a string; must be integer greater than zero.
        I 1 Singleton dimension, defines a scalar value.
        C 3 Coordinate triplet, always three; the coordinate type defines the meaning of this dimension.
        */

        public uint I, C, R, E, N, M;

        public MYSOFA_ARRAY ListenerPosition;
        public MYSOFA_ARRAY ReceiverPosition;
        public MYSOFA_ARRAY SourcePosition;
        public MYSOFA_ARRAY EmitterPosition;
        public MYSOFA_ARRAY ListenerUp;
        public MYSOFA_ARRAY ListenerView;
        public MYSOFA_ARRAY DataIR; // Array of filter coefficients. Sizes are filters * filter_length.
        public MYSOFA_ARRAY DataSamplingRate; // The sampling rate used in this structure
        public MYSOFA_ARRAY DataDelay; // Array of min-phase delays. Sizes are filters
        public IntPtr attributes; // General file attributes */
        public IntPtr variables; // Additional variables that might be present in a SOFA file
    }
}