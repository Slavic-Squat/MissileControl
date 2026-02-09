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
            public static long SelfID => ReferenceController.CubeGrid.EntityId;

            public MissileControl MissileControl { get; private set; }
            public long LauncherAddress { get; private set; }
            public long LauncherID { get; private set; }
            public MissileStage Stage => MissileControl.Stage;
            public MissileType Type => MissileControl.Type;
            public MissileGuidanceType GuidanceType => MissileControl.GuidanceType;
            public MissilePayload Payload => MissileControl.PayloadType;
            public EntityInfo Self { get; private set; }
            public EntityInfo Target { get; private set; }
            public double Time { get; private set; }

            private double _globalTimeOffset;
            public SystemCoordinator()
            {
                GetBlocks();
                Init();
            }

            private void Init()
            {
                Config.Set("Config", "MissileAddress", IGCS.Me);

                MissileControl = new MissileControl();

                CommunicationHandler0.RegisterTag("TARGET_INFO", true);
                CommunicationHandler0.RegisterTag("COMMANDS", true);
                CommandHandler0.RegisterCommand("SYNC_CLOCK", (args) => { if (args.Length > 0) SyncClock(args[0]); });
                CommandHandler0.RegisterCommand("ACTIVATE", (args) => { if (args.Length > 2) ActivateMissile(args[0], args[1], args[2]); });
                CommandHandler0.RegisterCommand("DEACTIVATE", (args) => DeactivateMissile());
                CommandHandler0.RegisterCommand("LAUNCH", (args) => LaunchMissile());
                CommandHandler0.RegisterCommand("ABORT", (args) => AbortMissile());
            }

            private void GetBlocks()
            {
                ReferenceController = AllGridBlocks.Where(b => b is IMyShipController && b.CustomName.ToUpper().Contains("MISSILE CONTROLLER")).FirstOrDefault() as IMyShipController;
                if (ReferenceController == null)
                {
                    DebugWrite("Error: missile controller not found!\n", true);
                    throw new Exception("missile controller not found!\n");
                }
            }

            public void Run(double time)
            {
                if (Time == 0)
                {
                    Time = time;
                    return;
                }

                GlobalTime = time + _globalTimeOffset;
                DebugEcho($"Global Time: {GlobalTime:F2}s");

                while (CommunicationHandler0.HasMessage("TARGET_INFO", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler0.TryRetrieveMessage("TARGET_INFO", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        byte[] bytes = Convert.FromBase64String(msg.Data as string);
                        Target = EntityInfo.Deserialize(bytes, 0);
                    }
                }

                while (CommunicationHandler0.HasMessage("COMMANDS", true))
                {
                    MyIGCMessage msg;
                    if (CommunicationHandler0.TryRetrieveMessage("COMMANDS", true, out msg))
                    {
                        if (msg.Source != LauncherAddress) continue;
                        string command = msg.Data as string;
                        CommandHandler0.RunCommands(command);
                    }
                }

                MissileControl.UpdateTarget(Target);
                MissileControl.Run(time);

                if (MissileControl.Stage > MissileStage.Launching)
                {
                    MissileInfo missileInfo = new MissileInfo(LauncherID, IGCS.Me, Target.EntityID, Stage, Type, GuidanceType, Payload);
                    MissileInfoLite missileInfoLite = new MissileInfoLite(LauncherID);
                    Self = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileInfo);
                    EntityInfo selfLite = new EntityInfo(SelfID, ReferencePosition, ReferenceVelocity, GlobalTime, missileInfoLite);

                    CommunicationHandler0.SendUnicast(Self.Serialize(), LauncherAddress, "MY_MISSILE_INFO", true);
                    CommunicationHandler0.SendBroadcast(selfLite.Serialize(), "ALL_MISSILE_INFO", false);
                }

                if (MissileControl.Stage >= MissileStage.Flying && (!CommunicationHandler0.CanReach(LauncherAddress) || !Target.IsValid))
                {
                    AbortMissile();
                }
                Time = time;
            }

            private void SyncClock(string timeString)
            {
                double time;
                if (double.TryParse(timeString, out time))
                {
                    _globalTimeOffset = time - Time;
                }
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
        }
    }
}
