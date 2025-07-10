using System;
using System.Runtime.InteropServices;

namespace Code
{

    [StructLayout(LayoutKind.Sequential)]
    public struct MYSOFA_ATTRIBUTE
    {
        public IntPtr next; // Pointer to the next attribute
        public IntPtr name; // Pointer to a character string
        public IntPtr value; // Pointer to a character string
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MYSOFA_ARRAY
    {
        public IntPtr values; // Pointer to an array of floats
        public uint elements; // Number of elements in the array
        public IntPtr attributes; // Pointer to a MYSOFA_ATTRIBUTE
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MYSOFA_VARIABLE
    {
        public IntPtr next;
        public IntPtr name;
        public IntPtr value;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct MYSOFA_HRIR
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
        public MYSOFA_ARRAY DataIR;
        public MYSOFA_ARRAY DataSamplingRate;
        public MYSOFA_ARRAY DataDelay;

        public IntPtr attributes; // Zeiger auf MYSOFA_ATTRIBUTE
        public IntPtr variables; // Zeiger auf MYSOFA_VARIABLE
    }



}
