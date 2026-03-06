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
        public enum MissilePayload : byte
        {
            Unknown, HE, Nuclear, Kinectic, ClusterHE, ClusterNuclear
        }
        public enum MissileStage : byte
        {
            Unknown, Building, Fueling, Idle, Launching, Flying, Interception
        }
        public static class MissileEnumHelper
        {

            public static MissilePayload GetMissilePayload(string payloadStr)
            {
                switch (payloadStr.ToUpper())
                {
                    case "HE": return MissilePayload.HE;
                    case "NUKE": return MissilePayload.Nuclear;
                    case "KINECTIC": return MissilePayload.Kinectic;
                    case "CLUSTER-HE": return MissilePayload.ClusterHE;
                    case "CLUSTER-NUKE": return MissilePayload.ClusterNuclear;
                    default: return MissilePayload.Unknown;
                }
            }
            public static string GetMissilePayloadStr(MissilePayload payload)
            {
                switch (payload)
                {
                    case MissilePayload.Unknown: return "N/A";
                    case MissilePayload.HE: return "HE";
                    case MissilePayload.Nuclear: return "NUKE";
                    case MissilePayload.Kinectic: return "KINECTIC";
                    case MissilePayload.ClusterHE: return "CLUSTER-HE";
                    case MissilePayload.ClusterNuclear: return "CLUSTER-NUKE";
                    default: return "N/A";
                }
            }

            public static MissileStage GetMissileStage(string stageStr)
            {
                switch (stageStr.ToUpper())
                {
                    case "BUILDING": return MissileStage.Building;
                    case "FUELING": return MissileStage.Fueling;
                    case "IDLE": return MissileStage.Idle;
                    case "LAUNCHING": return MissileStage.Launching;
                    case "FLYING": return MissileStage.Flying;
                    case "INTERCEPTION": return MissileStage.Interception;
                    default: return MissileStage.Unknown;
                }
            }

            public static string GetMissileStageStr(MissileStage stage)
            {
                switch (stage)
                {
                    case MissileStage.Unknown: return "N/A";
                    case MissileStage.Building: return "BUILDING";
                    case MissileStage.Fueling: return "FUELING";
                    case MissileStage.Idle: return "IDLE";
                    case MissileStage.Launching: return "LAUNCHING";
                    case MissileStage.Flying: return "FLYING";
                    case MissileStage.Interception: return "INTERCEPTION";
                    default: return "N/A";
                }
            }
        }
    }
}
