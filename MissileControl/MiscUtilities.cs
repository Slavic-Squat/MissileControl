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
