using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
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
        public struct MissileInfo
        {
            public long LauncherID { get; private set; }
            public long Address { get; private set; }
            public MissileStage Stage { get; private set; }
            public MissileType Type { get; private set; }
            public MissileGuidanceType GuidanceType { get; private set; }
            public MissilePayload Payload { get; private set; }
            public long TargetID { get; private set; }
            public bool IsValid { get; private set; }
            public bool Lite { get; private set; }

            public MissileInfo(long launcherID, long address, long targetID, MissileStage stage, MissileType type, MissileGuidanceType guidanceType, MissilePayload payload)
            {
                LauncherID = launcherID;
                Address = address;
                TargetID = targetID;
                Stage = stage;
                Type = type;
                Payload = payload;
                GuidanceType = guidanceType;
                IsValid = true;
                Lite = false;
            }

            public MissileInfo(long launhcerID)
            {
                LauncherID = launhcerID;
                Address = -1;
                TargetID = -1;
                Stage = MissileStage.Unknown;
                Type = MissileType.Unknown;
                Payload = MissilePayload.Unknown;
                GuidanceType = MissileGuidanceType.Unknown;
                IsValid = true;
                Lite = true;
            }

            public int Serialize(byte[] bytes, int offset)
            {
                int index = offset;
                MiscUtilities.WriteInt64(bytes, index, LauncherID);
                index += 8;
                if (Lite)
                {
                    bytes[index++] = 1;
                    return index - offset;
                }
                bytes[index++] = 0;
                MiscUtilities.WriteInt64(bytes, index, Address);
                index += 8;
                bytes[index++] = (byte)Stage;
                bytes[index++] = (byte)Type;
                bytes[index++] = (byte)GuidanceType;
                bytes[index++] = (byte)Payload;
                MiscUtilities.WriteInt64(bytes, index, TargetID);
                index += 8;
                return index - offset;
            }

            public static MissileInfo Deserialize(ImmutableArray<byte> bytes, int offset, out int bytesRead)
            {
                bytesRead = 0;
                int index = offset;
                if (bytes.Length - index < 9)
                {
                    return new MissileInfo();
                }
                long launcherID = MiscUtilities.ReadInt64(bytes, index);
                index += 8;
                bool lite = bytes[index] == 1;
                index += 1;
                if (lite)
                {
                    bytesRead = index - offset;
                    return new MissileInfo(launcherID);
                }
                if (bytes.Length - index < 20)
                {
                    return new MissileInfo();
                }
                long address = MiscUtilities.ReadInt64(bytes, index);
                index += 8;
                MissileStage stage = (MissileStage)bytes[index];
                index += 1;
                MissileType type = (MissileType)bytes[index];
                index += 1;
                MissileGuidanceType guidanceType = (MissileGuidanceType)bytes[index];
                index += 1;
                MissilePayload payload = (MissilePayload)bytes[index];
                index += 1;
                long targetID = MiscUtilities.ReadInt64(bytes, index);
                index += 8;
                bytesRead = index - offset;
                return new MissileInfo(launcherID, address, targetID, stage, type, guidanceType, payload);
            }
        }
    }
}
