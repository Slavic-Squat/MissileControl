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
            public MissilePayload Payload { get; private set; }
            public TrackStatus TrackStatus { get; private set; }
            public long TargetID { get; private set; }
            public bool IsValid { get; private set; }
            public bool Lite { get; private set; }

            public MissileInfo(long launcherID, long address, long targetID, MissileStage stage, MissilePayload payload, TrackStatus trackStatus)
            {
                LauncherID = launcherID;
                Address = address;
                TargetID = targetID;
                Stage = stage;
                Payload = payload;
                TrackStatus = trackStatus;
                IsValid = true;
                Lite = false;
            }

            public MissileInfo(long launhcerID)
            {
                LauncherID = launhcerID;
                Address = -1;
                TargetID = -1;
                Stage = MissileStage.Unknown;
                Payload = MissilePayload.Unknown;
                TrackStatus = TrackStatus.Unknown;
                IsValid = true;
                Lite = true;
            }

            public MissileInfo Merge(MissileInfo other)
            {
                if (!IsValid && other.IsValid)
                {
                    return other;
                }
                if (IsValid && !other.IsValid)
                {
                    return this;
                }
                if (Lite && !other.Lite)
                {
                    return other;
                }
                if (!Lite && other.Lite)
                {
                    return this;
                }
                return other;
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
                bytes[index++] = (byte)Payload;
                bytes[index++] = (byte)TrackStatus;
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
                if (bytes.Length - index < 19)
                {
                    return new MissileInfo();
                }
                long address = MiscUtilities.ReadInt64(bytes, index);
                index += 8;
                MissileStage stage = (MissileStage)bytes[index];
                index += 1;
                MissilePayload payload = (MissilePayload)bytes[index];
                index += 1;
                TrackStatus trackStatus = (TrackStatus)bytes[index];
                index += 1;
                long targetID = MiscUtilities.ReadInt64(bytes, index);
                index += 8;
                bytesRead = index - offset;
                return new MissileInfo(launcherID, address, targetID, stage, payload, trackStatus);
            }
        }
    }
}
