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
        public class MissileControl
        {
            private Program program;
            private int ID;
            private bool clusterMissile;

            private MissileGuidance missileGuidance;

            private enum Direction
            {
                Forward, Backward, Leftward, Rightward, Upward, Downward
            }

            public enum Stage
            {
                Idle, Launching, Flying, Interception
            }

            private IMyBroadcastListener myBroadcastListener;

            private List<IMyGyro> gyros = new List<IMyGyro>();
            private IMyRemoteControl remoteControl;
            private IMyShipMergeBlock mergeBlock;
            private List<IMyWarhead> payload = new List<IMyWarhead>();
            private List<IMyMotorStator> attachmentRotors = new List<IMyMotorStator>();

            private List<IMyThrust> thrusters = new List<IMyThrust>();
            private float maxForwardThrust;
            private float maxBackwardThrust;
            private float maxLeftwardThrust;
            private float maxRightwardThrust;
            private float maxUpwardThrust;
            private float maxDownwardThrust;

            private float missileMass;

            private float maxForwardAccel;
            private float maxRadialAccel;

            private Vector3 missilePosition;
            private Vector3 missileVelocity;

            private Vector3 targetPosition;
            private Vector3 targetVelocity;
            private Vector3 launcherPosition;

            private Vector3 relativeTargetPosition;
            private Vector3 closingVelocity;
            private float distanceToTarget;
            private float closingSpeed;
            private float timeToTarget;

            public Stage stage = Stage.Idle;

            private Dictionary<IMyThrust, MyTuple<Vector3, Direction>> thrusterInfo = new Dictionary<IMyThrust, MyTuple<Vector3, Direction>>();

            private Vector3 localTotalAcceleration;
            private Vector3 localVectorToAlign;

            private PIDControl pitchController;
            private PIDControl yawController;

            public MissileControl(Program program, int ID, bool clusterMissile)
            {
                this.program = program;
                this.ID = ID;
                this.clusterMissile = clusterMissile;

                program.GridTerminalSystem.GetBlockGroupWithName($"Missile Thrusters {ID}").GetBlocksOfType<IMyThrust>(thrusters);
                program.GridTerminalSystem.GetBlockGroupWithName($"Missile Gyros {ID}").GetBlocksOfType<IMyGyro>(gyros);
                remoteControl = (IMyRemoteControl)program.GridTerminalSystem.GetBlockWithName($"Missile Controller {ID}");
                mergeBlock = (IMyShipMergeBlock)program.GridTerminalSystem.GetBlockWithName($"Missile Merge Block {ID}");
                program.GridTerminalSystem.GetBlockGroupWithName($"Payload {ID}").GetBlocksOfType(payload);

                if (clusterMissile == true)
                {
                    program.GridTerminalSystem.GetBlockGroupWithName($"Attachment Rotors {ID}").GetBlocksOfType(attachmentRotors);
                }

                foreach (IMyThrust thruster in thrusters)
                {
                    Vector3D localThrusterDirection = Vector3.Round(Vector3.TransformNormal(thruster.WorldMatrix.Backward, Matrix.Transpose(remoteControl.WorldMatrix)), 1);

                    if (localThrusterDirection.Z == -1)
                    {
                        maxForwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Forward));
                    }
                    else if (localThrusterDirection.Z == 1)
                    {
                        maxBackwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Backward));
                    }
                    else if (localThrusterDirection.X == -1)
                    {
                        maxLeftwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Leftward));
                    }
                    else if (localThrusterDirection.X == 1)
                    {
                        maxRightwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Rightward));
                    }
                    else if (localThrusterDirection.Y == 1)
                    {
                        maxUpwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Upward));
                    }
                    else if (localThrusterDirection.Y == -1)
                    {
                        maxDownwardThrust += thruster.MaxThrust;
                        thrusterInfo.Add(thruster, new MyTuple<Vector3, Direction>(localThrusterDirection, Direction.Downward));
                    }
                }

                missileMass = remoteControl.CalculateShipMass().PhysicalMass;
                maxForwardAccel = maxForwardThrust / missileMass;
                maxRadialAccel = maxRightwardThrust / missileMass;

                pitchController = new PIDControl(1.0f, 0, 0.2f);
                yawController = new PIDControl(1.0f, 0, 0.2f);

                missileGuidance = new MissileGuidance(maxForwardAccel, maxRadialAccel, 3.5f, 10);
            }

            public void Run()
            {
                if (myBroadcastListener != null)
                {
                    float timeDelta = (float)program.Runtime.TimeSinceLastRun.TotalSeconds;

                    missileMass = remoteControl.CalculateShipMass().PhysicalMass;
                    missilePosition = remoteControl.CubeGrid.GetPosition();
                    missileVelocity = remoteControl.GetShipVelocities().LinearVelocity;

                    if (myBroadcastListener.HasPendingMessage)
                    {
                        MyIGCMessage message = myBroadcastListener.AcceptMessage();
                        MyTuple<Vector3, Vector3, Matrix> laserTargetInfo = message.As<MyTuple<Vector3, Vector3, Matrix>>();

                        targetPosition = laserTargetInfo.Item1;
                        targetVelocity = laserTargetInfo.Item2;

                        launcherPosition = laserTargetInfo.Item3.Translation;
                    }
                    relativeTargetPosition = targetPosition - missilePosition;
                    distanceToTarget = relativeTargetPosition.Length();
                    closingVelocity = targetVelocity - missileVelocity;
                    closingSpeed = Vector3.Dot(Vector3.Normalize(relativeTargetPosition), Vector3.Normalize(closingVelocity)) * closingVelocity.Length();
                    timeToTarget = distanceToTarget / -closingSpeed;

                    switch (stage)
                    {
                        case Stage.Idle:

                            localVectorToAlign = -Vector3.UnitZ;
                            localTotalAcceleration = Vector3.Zero;

                            break;

                        case Stage.Launching:

                            mergeBlock.Enabled = false;
                            localVectorToAlign = -Vector3.UnitZ;
                            localTotalAcceleration = maxForwardAccel * localVectorToAlign;

                            if ((missilePosition - launcherPosition).Length() > 100)
                            {
                                stage = Stage.Flying;
                            }

                            break;

                        case Stage.Flying:

                            missileGuidance.Run(missileVelocity, missilePosition, targetVelocity, targetPosition);
                            localTotalAcceleration = Vector3.TransformNormal(missileGuidance.totalAcceleration, Matrix.Transpose(remoteControl.WorldMatrix));
                            localVectorToAlign = Vector3.TransformNormal(missileGuidance.vectorToAlign, Matrix.Transpose(remoteControl.WorldMatrix));

                            if (timeToTarget > 0 && timeToTarget < 7)
                            {
                                stage = Stage.Interception;
                            }

                            break;

                        case Stage.Interception:

                            missileGuidance.Run(missileVelocity, missilePosition, targetVelocity, targetPosition);
                            localTotalAcceleration = Vector3.TransformNormal(missileGuidance.totalAcceleration, Matrix.Transpose(remoteControl.WorldMatrix));
                            localVectorToAlign = Vector3.TransformNormal(missileGuidance.vectorToAlign, Matrix.Transpose(remoteControl.WorldMatrix));

                            if (clusterMissile == true)
                            {
                                foreach (IMyGyro gyro in gyros)
                                {
                                    gyro.Roll = (float)(2 * Math.PI);
                                }

                                if (timeToTarget > 0 && timeToTarget < 5)
                                {
                                    foreach (IMyWarhead warhead in payload)
                                    {
                                        warhead.IsArmed = true;
                                        warhead.DetonationTime = timeToTarget - 0.1f;
                                        warhead.StartCountdown();
                                    }
                                    foreach (IMyMotorStator attachmentRotor in attachmentRotors)
                                    {
                                        attachmentRotor.Detach();
                                    }
                                }
                            }
                            else
                            {
                                if (timeToTarget <= 0.5f)
                                {
                                    foreach (IMyWarhead warhead in payload)
                                    {
                                        warhead.IsArmed = true;
                                        warhead.Detonate();
                                    }
                                }
                            }
                            break;
                    }

                    float rotation = (float)Math.Acos(Vector3.Dot(Vector3.Forward, localVectorToAlign));
                    Vector3 rotationVector = Vector3.Cross(Vector3.Forward, localVectorToAlign);
                    rotationVector.Normalize();
                    Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, rotation);
                    Matrix alignedMatrix = Matrix.Transform(Matrix.Identity, quaternion);

                    float yawError = (float)Math.Atan2(-alignedMatrix.M13, alignedMatrix.M11);
                    program.Echo(yawError.ToString());
                    float pitchError = (float)Math.Atan2(-alignedMatrix.M32, alignedMatrix.M22);
                    program.Echo(pitchError.ToString());
                    float yawCorrection = yawController.Run(yawError, timeDelta);
                    float pitchCorrection = pitchController.Run(pitchError, timeDelta);

                    foreach (IMyGyro gyro in gyros)
                    {
                        gyro.Pitch = -pitchCorrection;
                        gyro.Yaw = -yawCorrection;
                    }


                    foreach (KeyValuePair<IMyThrust, MyTuple<Vector3, Direction>> thruster in thrusterInfo)
                    {
                        Vector3 localTargetThrustVector = localTotalAcceleration * missileMass;
                        float maxThrust = 0;
                        switch (thruster.Value.Item2)
                        {
                            case Direction.Forward:
                                maxThrust = maxForwardThrust;
                                break;

                            case Direction.Backward:
                                maxThrust = maxBackwardThrust;
                                break;

                            case Direction.Leftward:
                                maxThrust = maxLeftwardThrust;
                                break;

                            case Direction.Rightward:
                                maxThrust = maxRightwardThrust;
                                break;

                            case Direction.Upward:
                                maxThrust = maxUpwardThrust;
                                break;

                            case Direction.Downward:
                                maxThrust = maxDownwardThrust;
                                break;
                        }

                        if (Vector3.Dot(localVectorToAlign, Vector3.Forward) > 0.95f && maxThrust != 0)
                        {
                            thruster.Key.ThrustOverridePercentage = Vector3.Dot(localTargetThrustVector, thruster.Value.Item1) / maxThrust;
                        }
                        else
                        {
                            thruster.Key.ThrustOverridePercentage = 0;
                        }
                    }
                }
            }

            public void Launch(string broadcastTag)
            {
                myBroadcastListener = program.IGC.RegisterBroadcastListener(broadcastTag);

                stage = Stage.Launching;

                if (program.Runtime.UpdateFrequency != UpdateFrequency.Update1)
                {
                    program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                }
            }
        }
    }
}
