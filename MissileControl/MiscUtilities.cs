using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public static class MiscUtilities
        {
            public static float LoopInRange(float value, float min, float max)
            {
                if (min >= max)
                    throw new ArgumentException("min must be less than max");

                if (value >= min && value < max)
                    return value;

                float range = max - min;
                float shifted = value - min;
                shifted -= range * (float)Math.Floor(shifted / range);
                return min + shifted;
            }

            public static int LoopInRange(int value, int min, int max)
            {
                if (min >= max)
                    throw new ArgumentException("min must be less than max");

                if (value >= min && value < max)
                    return value;

                int range = max - min;
                int shifted = value - min;
                shifted %= range;
                if (shifted < 0)
                    shifted += range;
                return min + shifted;
            }

            public static float Clamp(float value, float min, float max)
            {
                if (min > max)
                    throw new ArgumentException("min must be less than or equal to max");
                if (value < min)
                    return min;
                if (value > max)
                    return max;
                return value;
            }

            public static float Lerp(float a, float b, float t)
            {
                return a + (b - a) * t;
            }

            public static float InverseLerp(float a, float b, float value)
            {
                if (a == b)
                    throw new ArgumentException("a and b must be different values");
                return (value - a) / (b - a);
            }

            public static float Remap(float inMin, float inMax, float outMin, float outMax, float value, bool clamp = false)
            {
                float t = InverseLerp(inMin, inMax, value);
                if (clamp)
                    t = Clamp(t, 0, 1);
                return Lerp(outMin, outMax, t);
            }

            public static void WriteInt64(byte[] buffer, int offset, long value)
            {
                buffer[offset] = (byte)value;
                buffer[offset + 1] = (byte)(value >> 8);
                buffer[offset + 2] = (byte)(value >> 16);
                buffer[offset + 3] = (byte)(value >> 24);
                buffer[offset + 4] = (byte)(value >> 32);
                buffer[offset + 5] = (byte)(value >> 40);
                buffer[offset + 6] = (byte)(value >> 48);
                buffer[offset + 7] = (byte)(value >> 56);
            }

            public static long ReadInt64(ImmutableArray<byte> buffer, int offset)
            {
                return (long)buffer[offset]
                    | ((long)buffer[offset + 1] << 8)
                    | ((long)buffer[offset + 2] << 16)
                    | ((long)buffer[offset + 3] << 24)
                    | ((long)buffer[offset + 4] << 32)
                    | ((long)buffer[offset + 5] << 40)
                    | ((long)buffer[offset + 6] << 48)
                    | ((long)buffer[offset + 7] << 56);
            }
        }
    }
}
