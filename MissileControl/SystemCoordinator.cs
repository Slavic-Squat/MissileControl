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
            public long LauncherAddress { get; private set; }
            public long LauncherID { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type => MissileControl.Type;
            public MissileGuidanceType GuidanceType => MissileControl.GuidanceType;
            public MissilePayload Payload => MissileControl.PayloadType;
            public EntityInfo Target => MissileControl.Target;

            private double _time;
            private double _globalTimeOffset;
            private byte[] _selfBuffer = new byte[128];
            public SystemCoordinator()
            {
                Init();
            }

            private void Init()
            {
                ReferenceController = AllGridBlocks.FirstOrDefault(b => b is IMyShipController && b.CustomName.ToUpper().Contains("MISSILE CONTROLLER")) as IMyShipController;
                if (ReferenceController == null)
                {
                    throw new Exception("missile controller not found!");
                }

                Config.Set("Config", "MissileAddress", IGCS.Me);
                MePb.CustomData = Config.ToString();

                MissileControl = new MissileControl();

                CommunicationHandler0.RegisterTag("TARGET_INFO", true);
                CommunicationHandler0.RegisterTag("COMMANDS", true);
                CommandHandler0.RegisterCommand("SYNC_CLOCK", (args) => { if (args.Length > 0) SyncClock(args[0]); });
                CommandHandler0.RegisterCommand("ACTIVATE", (args) => { if (args.Length > 2) ActivateMissile(args[0], args[1], args[2]); });
                CommandHandler0.RegisterCommand("DEACTIVATE", (args) => DeactivateMissile());
                CommandHandler0.RegisterCommand("LAUNCH", (args) => LaunchMissile());
                CommandHandler0.RegisterCommand("ABORT", (args) => AbortMissile());
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

                MissileControl.UpdateTarget(Target);
                MissileControl.Run(time);

                if (Stage > MissileStage.Launching)
                {
                    Transmit();
                }

                if (Stage >= MissileStage.Flying && (!CommunicationHandler0.CanReach(LauncherAddress) || !Target.IsValid))
                {
                    AbortMissile();
                }
                _time = time;
            }

            private void SyncClock(string timeString)
            {
                double time;
                if (!double.TryParse(timeString, out time))
                    return;
                _globalTimeOffset = time - _time;
            }

            private void ActivateMissile(string launcherAddressString, string launcherIDString, string timeString)
            {
                long launcherAddress;
                if (!long.TryParse(launcherAddressString, out launcherAddress)) return;
                long launcherID;
                if (!long.TryParse(launcherIDString, out launcherID)) return;
                LauncherAddress = launcherAddress;
                LauncherID = launcherID;
                SyncClock(timeString);
                MissileControl.Activate();
            }

            private void DeactivateMissile()
            {
                MissileControl.Deactivate();
            }

            private void LaunchMissile()
            {
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

                MissileInfo missile = new MissileInfo(LauncherID, IGCS.Me, Target.EntityID, Stage, Type, GuidanceType, Payload);
                MissileInfo missileLite = new MissileInfo(LauncherID);
                EntityInfo entity = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missile);
                EntityInfo entityLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileLite);

                int bytesWritten = entity.Serialize(_selfBuffer, index);
                _selfBuffer[sizeIndex] = (byte)bytesWritten;
                index += bytesWritten;

                if (index > 1)
                {
                    ImmutableArray<byte> bytes = ImmutableArray.Create(_selfBuffer, 0, index);
                    CommunicationHandler0.SendUnicast(bytes, LauncherAddress, "MY_MISSILE_INFO", true);
                }

                index = 0;
                bytesWritten = entityLite.Serialize(_selfBuffer, index);
                _selfBuffer[sizeIndex] = (byte)bytesWritten;
                index += bytesWritten;

                if (index > 1)
                {
                    ImmutableArray<byte> bytes = ImmutableArray.Create(_selfBuffer, 0, index);
                    CommunicationHandler0.SendBroadcast(bytes, "ALL_MISSILE_INFO", false);
                }
            }

            private void Receive()
            {
                while (CommunicationHandler0.HasMessage("TARGET_INFO", true))
                {
                    MyIGCMessage message;
                    if (CommunicationHandler0.TryRetrieveMessage("TARGET_INFO", true, out message))
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

                while (CommunicationHandler0.HasMessage("COMMANDS", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler0.TryRetrieveMessage("COMMANDS", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        string command = msg.As<string>();
                        CommandHandler0.RunCommands(command);
                    }
                }
            }
        }
    }
}
