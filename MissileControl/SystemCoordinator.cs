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
        public class SystemCoordinator
        {
            public static double GlobalTime { get; private set; }
            public static IMyShipController ReferenceController { get; private set; }
            public static MatrixD ReferenceWorldMatrix => ReferenceController.WorldMatrix;
            public static Vector3D ReferencePosition => ReferenceController.GetPosition();
            public static Vector3D ReferenceVelocity => ReferenceController.GetShipVelocities().LinearVelocity;
            public static Vector3D ReferenceGravity => ReferenceController.GetNaturalGravity();
            public static float ReferenceMass => ReferenceController.CalculateShipMass().TotalMass;
            public static long SelfID => ReferenceController.CubeGrid.EntityId;

            public MissileControl MissileControl { get; private set; }

            private double _time;
            private double _globalTimeOffset;
            private byte[] _selfBuffer = new byte[128];

            private long _launcherID;
            private long _launcherAddress;
            private IMyProgrammableBlock _launcherPb;
            private string _bayID;
            private StringBuilder _cmdSb = new StringBuilder();
            public SystemCoordinator()
            {
                Init();
            }

            private void Init()
            {
                ReferenceController = AllBlocks.FirstOrDefault(b => b is IMyShipController && b.CustomName.ToUpper().Contains("MISSILE CONTROLLER")) as IMyShipController;
                if (ReferenceController == null)
                {
                    throw new Exception("missile controller not found!");
                }

                Config.Set("Config", "MissileAddress", IGCS.Me);
                MePb.CustomData = Config.ToString();

                MissileControl = new MissileControl();

                CommunicationHandlerInst.RegisterTag("TARGET_INFO", true);
                CommunicationHandlerInst.RegisterTag("COMMANDS", true);
                CommandHandlerInst.RegisterCommand("HANDSHAKE", (args) => { if (args.Length > 3) Handshake(args[0], args[1], args[2], args[3]); });
                CommandHandlerInst.RegisterCommand("UPDATE_BAY", (args) => UpdateBay());
                CommandHandlerInst.RegisterCommand("SYNC_CLOCK", (args) => { if (args.Length > 0) SyncClock(args[0]); });
                CommandHandlerInst.RegisterCommand("LAUNCH", (args) => { if (args.Length > 0) LaunchMissile(args[0]); });
                CommandHandlerInst.RegisterCommand("ABORT", (args) => AbortMissile());
            }

            public void Run(double time)
            {
                if (_time == 0)
                {
                    _time = time;
                    return;
                }

                GlobalTime = time + _globalTimeOffset;

                Receive();

                MissileControl.Run(time);
                MissileStage stage = MissileControl.GetStage();

                if (stage > MissileStage.Launching)
                {
                    Transmit();
                }

                if (stage >= MissileStage.Flying && (!CommunicationHandlerInst.CanReach(_launcherAddress) || !MissileControl.Target.IsValid))
                {
                    AbortMissile();
                }
                _time = time;
            }

            private void Handshake(string launcherPbIDStr, string bayID, string launcherAddressString, string launcherIDString)
            {
                long launcherPbID;
                if (!long.TryParse(launcherPbIDStr, out launcherPbID)) return;
                _launcherPb = GTS.GetBlockWithId(launcherPbID) as IMyProgrammableBlock;
                if (_launcherPb == null) return;
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return;
                long launcherID;
                if (!long.TryParse(launcherIDString, out launcherID)) return;
                _launcherAddress = launcherAddress;
                _launcherID = launcherID;
                _bayID = bayID;

                _cmdSb.Clear();
                _cmdSb.Append("HANDSHAKE ").Append(bayID);
                _cmdSb.Append(" ").Append(IGCS.Me);
                _cmdSb.Append(" ").Append(MissileEnumHelper.GetMissileTypeStr(MissileControl.Type));
                _cmdSb.Append(" ").Append(MissileEnumHelper.GetMissileGuidanceStr(MissileControl.GuidanceType));
                _cmdSb.Append(" ").Append(MissileEnumHelper.GetMissilePayloadStr(MissileControl.PayloadType));
                _launcherPb.TryRun(_cmdSb.ToString());
            }

            private void UpdateBay()
            {
                if (_launcherPb == null || string.IsNullOrEmpty(_bayID)) return;
                _cmdSb.Clear();
                _cmdSb.Append("UPDATE_BAY ").Append(_bayID);
                MissileStage stage = MissileControl.GetStage();
                _cmdSb.Append(" ").Append(MissileEnumHelper.GetMissileStageStr(stage));
                _launcherPb.TryRun(_cmdSb.ToString());
            }

            private void SyncClock(string timeString)
            {
                double time;
                if (!double.TryParse(timeString, out time))
                    return;
                _globalTimeOffset = time - _time;
            }

            private void LaunchMissile(string timeString)
            {
                RuntimeInfo.UpdateFrequency = UpdateFrequency.Update1;
                SyncClock(timeString);
                MissileControl.Launch();
            }

            private void AbortMissile()
            {
                MissileControl.Abort();
            }

            private void Transmit()
            {
                int index = 0;
                int sizeIndex = index++;

                EntityInfo target = MissileControl.Target;
                MissileStage stage = MissileControl.GetStage();
                MissileType type = MissileControl.Type;
                MissileGuidanceType guidanceType = MissileControl.GuidanceType;
                MissilePayload payload = MissileControl.PayloadType;
                MissileInfo missile = new MissileInfo(_launcherID, IGCS.Me, target.EntityID, stage, type, guidanceType, payload);
                MissileInfo missileLite = new MissileInfo(_launcherID);
                EntityInfo entity = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missile);
                EntityInfo entityLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileLite);

                int bytesWritten = entity.Serialize(_selfBuffer, index);
                _selfBuffer[sizeIndex] = (byte)bytesWritten;
                index += bytesWritten;

                if (index > 1)
                {
                    ImmutableArray<byte> bytes = ImmutableArray.Create(_selfBuffer, 0, index);
                    CommunicationHandlerInst.SendUnicast(bytes, _launcherAddress, "MY_MISSILE_INFO", true);
                }

                index = 0;
                bytesWritten = entityLite.Serialize(_selfBuffer, index);
                _selfBuffer[sizeIndex] = (byte)bytesWritten;
                index += bytesWritten;

                if (index > 1)
                {
                    ImmutableArray<byte> bytes = ImmutableArray.Create(_selfBuffer, 0, index);
                    CommunicationHandlerInst.SendBroadcast(bytes, "ALL_MISSILE_INFO", false);
                }
            }

            private void Receive()
            {
                while (CommunicationHandlerInst.HasMessage("TARGET_INFO", true))
                {
                    MyIGCMessage message;
                    if (CommunicationHandlerInst.TryRetrieveMessage("TARGET_INFO", true, out message))
                    {
                        ImmutableArray<byte> bytes = message.As<ImmutableArray<byte>>();
                        int index = 0;
                        byte size = bytes[index++];
                        int bytesRead;
                        EntityInfo target = EntityInfo.Deserialize(bytes, index, out bytesRead);
                        if (!target.IsValid || size != bytesRead)
                        {
                            continue;
                        }
                        MissileControl.UpdateTarget(target);
                    }
                }

                while (CommunicationHandlerInst.HasMessage("COMMANDS", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandlerInst.TryRetrieveMessage("COMMANDS", true, out msg))
                    {
                        if (msg.Source != _launcherAddress) continue;
                        string command = msg.As<string>();
                        CommandHandlerInst.RunCommands(command);
                    }
                }
            }
        }
    }
}
