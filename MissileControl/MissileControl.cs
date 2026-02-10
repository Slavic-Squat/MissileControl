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
            private double _time;
            private List<Gyro> _gyros = new List<Gyro>();
            private List<IMyWarhead> _payload = new List<IMyWarhead>();
            private List<ThrusterGroup> _thrusterGroups = new List<ThrusterGroup>();
            private Dictionary<Direction, float> _maxThrust = new Dictionary<Direction, float>();
            private IMyRadioAntenna _antenna;
            private List<GasTank> _h2Tanks = new List<GasTank>();
            private List<Battery> _batteries = new List<Battery>();
            private IMyRemoteControl _remoteCtrl;
            private IMyCameraBlock _proxySensor;

            private MissileGuidance _missileGuidance;
            private PIDControl _pitchController;
            private PIDControl _yawController;

            private float _missileMass;
            private float _maxSpeed;
            private float _m;
            private float _n;
            private float _kp;
            private float _ki;
            private float _kd;
            private float _maxForwardAccel;
            private float _maxRadialAccel;
            private float _maxAccel;

            private MissileType _type;
            private MissileGuidanceType _guidanceType;
            private MissilePayload _payloadType;
            private Direction _launchDirection;
            private double _launchPeriod = 3;
            private Direction _dismountDirection;
            private double _dismountPeriod = 0;
            private float _proxySensorRange = 5;

            private EntityInfo _target;
            private double _launchTime;
            public MissileStage Stage { get; private set; } = MissileStage.Building;
            public MissileType Type => _type;
            public MissileGuidanceType GuidanceType => _guidanceType;
            public MissilePayload PayloadType => _payloadType;
            public EntityInfo Target => _target;

            public MissileControl()
            {
                GetBlocks();
                Init();
            }

            private void GetBlocks()
            {
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 0")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 1")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 2")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 3")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 4")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllGridBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 5")).Select(b => new Thruster(b as IMyThrust)).ToArray()));

                if (_thrusterGroups.Count(tg => tg.Thrusters.Count > 0) == 0)
                {
                    DebugEcho("Error: no thrusters found!");
                    throw new Exception("No thrusters found!");
                }
                _gyros = AllGridBlocks.Where(b => b is IMyGyro).Select(b => new Gyro(b as IMyGyro)).ToList();
                if (_gyros.Count == 0)
                {
                    DebugEcho("Error: no gyros found!");
                    throw new Exception("No gyros found!");
                }

                _payload = AllGridBlocks.Where(b => b is IMyWarhead).Cast<IMyWarhead>().ToList();
                if (_payload.Count == 0)
                {
                    DebugEcho("Error: no warheads found!");
                    throw new Exception("No warheads found!");
                }

                _antenna = AllGridBlocks.Where(b => b is IMyRadioAntenna).FirstOrDefault() as IMyRadioAntenna;
                if (_antenna == null)
                {
                    DebugEcho("Error: no antenna found!");
                    throw new Exception("No antenna found!");
                }

                _h2Tanks = AllGridBlocks.Where(b => b is IMyGasTank).Select(b => new GasTank(b as IMyGasTank)).ToList();
                if (_h2Tanks.Count == 0)
                {
                    DebugEcho("Error: no hydrogen tanks found!");
                    throw new Exception("No hydrogen tanks found!");
                }

                _batteries = AllGridBlocks.Where(b => b is IMyBatteryBlock).Select(b => new Battery(b as IMyBatteryBlock)).ToList();
                if (_batteries.Count == 0)
                {
                    DebugEcho("Error: no batteries found!");
                    throw new Exception("No batteries found!");
                }

                _remoteCtrl = AllGridBlocks.Where(b => b is IMyRemoteControl).FirstOrDefault() as IMyRemoteControl;
                if (_remoteCtrl == null)
                {
                    DebugEcho("Error: no remote control found!");
                    throw new Exception("No remote control found!");
                }

                _proxySensor = AllGridBlocks.Where(b => b is IMyCameraBlock).FirstOrDefault() as IMyCameraBlock;
                if (_proxySensor == null)
                {
                    DebugEcho("Error: no proxy sensor found!");
                    throw new Exception("No proxy sensor found!");
                }
            }

            private void Init()
            {
                _type = MissileEnumHelper.GetMissileType(Config.Get("Config", "Type").ToString(MissileEnumHelper.GetMissileTypeStr(MissileType.Unknown)));
                Config.Set("Config", "Type", MissileEnumHelper.GetMissileTypeStr(_type));

                _guidanceType = MissileEnumHelper.GetMissileGuidanceType(Config.Get("Config", "GuidanceType").ToString(MissileEnumHelper.GetMissileGuidanceStr(MissileGuidanceType.Unknown)));
                Config.Set("Config", "GuidanceType", MissileEnumHelper.GetMissileGuidanceStr(_guidanceType));

                _payloadType = MissileEnumHelper.GetMissilePayload(Config.Get("Config", "Payload").ToString(MissileEnumHelper.GetMissilePayloadStr(MissilePayload.Unknown)));
                Config.Set("Config", "Payload", MissileEnumHelper.GetMissilePayloadStr(_payloadType));

                _missileMass = Config.Get("Config", "Mass").ToSingle(10000);
                Config.Set("Config", "Mass", _missileMass);

                _maxSpeed = Config.Get("Config", "MaxSpeed").ToSingle(100);
                Config.Set("Config", "MaxSpeed", _maxSpeed);

                _m = Config.Get("Config", "M").ToSingle(0.35f);
                Config.Set("Config", "M", _m);

                _n = Config.Get("Config", "N").ToSingle(5f);
                Config.Set("Config", "N", _n);

                _kp = Config.Get("Config", "Kp").ToSingle(2.5f);
                Config.Set("Config", "Kp", _kp);

                _ki = Config.Get("Config", "Ki").ToSingle(0f);
                Config.Set("Config", "Ki", _ki);

                _kd = Config.Get("Config", "Kd").ToSingle(0f);
                Config.Set("Config", "Kd", _kd);

                _launchDirection = MiscEnumHelper.GetDirection(Config.Get("Config", "LaunchDirection").ToString("FORWARD"));
                Config.Set("Config", "LaunchDirection", MiscEnumHelper.GetDirectionStr(_launchDirection));
                _launchPeriod = Config.Get("Config", "LaunchPeriod").ToDouble(3);
                Config.Set("Config", "LaunchPeriod", _launchPeriod);

                _dismountDirection = MiscEnumHelper.GetDirection(Config.Get("Config", "DismountDirection").ToString("UP"));
                Config.Set("Config", "DismountDirection", MiscEnumHelper.GetDirectionStr(_dismountDirection));
                _dismountPeriod = Config.Get("Config", "DismountPeriod").ToDouble(0);
                Config.Set("Config", "DismountPeriod", _dismountPeriod);

                _proxySensorRange = Config.Get("Config", "ProxySensorRange").ToSingle(5);
                Config.Set("Config", "ProxySensorRange", _proxySensorRange);

                MatrixD referenceOrientation = SystemCoordinator.ReferenceWorldMatrix.GetOrientation();

                _maxThrust[Direction.Backward] = 0;
                _maxThrust[Direction.Forward] = 0;
                _maxThrust[Direction.Right] = 0;
                _maxThrust[Direction.Left] = 0;
                _maxThrust[Direction.Up] = 0;
                _maxThrust[Direction.Down] = 0;

                foreach (var thrusterGroup in _thrusterGroups)
                {
                    Vector3 thrust = Vector3.TransformNormal(thrusterGroup.Vector, MatrixD.Transpose(referenceOrientation)) * thrusterGroup.MaxThrust;

                    if (thrust.X > 0)
                    {
                        _maxThrust[Direction.Right] += thrust.X;
                    }
                    else if (thrust.X < 0)
                    {
                        _maxThrust[Direction.Left] += -thrust.X;
                    }

                    if (thrust.Y > 0)
                    {
                        _maxThrust[Direction.Up] += thrust.Y;
                    }
                    else if (thrust.Y < 0)
                    {
                        _maxThrust[Direction.Down] += -thrust.Y;
                    }

                    if (thrust.Z > 0)
                    {
                        _maxThrust[Direction.Backward] += thrust.Z;
                    }
                    else if (thrust.Z < 0)
                    {
                        _maxThrust[Direction.Forward] += -thrust.Z;
                    }
                }

                _maxForwardAccel = _maxThrust[Direction.Forward] / _missileMass;
                _maxRadialAccel = _maxThrust[Direction.Right] / _missileMass;
                _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);

                _pitchController = new PIDControl(_kp, _ki, _kd);
                _yawController = new PIDControl(_kp, _ki, _kd);

                _missileGuidance = new MissileGuidance(_maxAccel, _m, _n, maxSpeed: _maxSpeed);

                _antenna.Enabled = false;
                _gyros.ForEach(g => g.GyroBlock.GyroOverride = true);
                _gyros.ForEach(g => g.GyroBlock.Enabled = false);
                _payload.ForEach(w => w.IsArmed = false);
                _remoteCtrl.DampenersOverride = false;
                _remoteCtrl.SetAutoPilotEnabled(false);
                _remoteCtrl.ControlThrusters = true;
                _remoteCtrl.ControlWheels = false;
                _remoteCtrl.SetValue("ControlGyros", true);
                _proxySensor.Enabled = false;
                _proxySensor.EnableRaycast = true;

                _h2Tanks.ForEach(t => t.TankBlock.Stockpile = true);
                _batteries.ForEach(b => b.BatteryBlock.ChargeMode = ChargeMode.Recharge);

                foreach (var thrusterGroup in _thrusterGroups)
                {
                    foreach (var thruster in thrusterGroup.Thrusters)
                    {
                        thruster.ThrusterBlock.Enabled = false;
                    }
                }
                
            }

            public void Run(double time)
            {
                if (_time == 0)
                {
                    _time = time;
                    return;
                }
                double globalTime = SystemCoordinator.GlobalTime;

                if (Stage < MissileStage.Idle)
                {
                    switch (Stage)
                    {
                        case MissileStage.Building:
                            if (!_remoteCtrl.IsFunctional) break;
                            if (_h2Tanks.Any(t => !t.TankBlock.IsFunctional)) break;
                            if (_batteries.Any(b => !b.BatteryBlock.IsFunctional)) break;
                            if (_thrusterGroups.Any(tg => tg.Thrusters.Any(thruster => !thruster.ThrusterBlock.IsFunctional))) break;
                            if (_gyros.Any(g => !g.GyroBlock.IsFunctional)) break;
                            if (_payload.Any(w => !w.IsFunctional)) break;
                            if (!_antenna.IsFunctional) break;
                            if (!_proxySensor.IsFunctional) break;
                            Stage = MissileStage.Fueling;
                            break;
                        case MissileStage.Fueling:
                            if (_h2Tanks.Any(t => !t.IsFull)) break;
                            if (_batteries.Any(b => !b.IsFull)) break;
                            Stage = MissileStage.Idle;
                            break;
                    }
                    Config.Set("Config", "Stage", MissileEnumHelper.GetMissileStageStr(Stage));
                    MePb.CustomData = Config.ToString();
                }
                else if (Stage > MissileStage.Active)
                {
                    double timeDelta = time - _time;

                    _missileMass = _remoteCtrl.CalculateShipMass().TotalMass;
                    _maxForwardAccel = _maxThrust[Direction.Forward] / _missileMass;
                    _maxRadialAccel = _maxThrust[Direction.Right] / _missileMass;
                    _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);
                    _missileGuidance.MaxAccel = _maxAccel;

                    Vector3D missilePos = SystemCoordinator.ReferencePosition;
                    Vector3D missileVel = SystemCoordinator.ReferenceVelocity;

                    Vector3D estimatedTargetPos = _target.Position;
                    if (_target.TimeRecorded < globalTime)
                    {
                        double secSinceLastUpdate = globalTime - _target.TimeRecorded;
                        estimatedTargetPos = _target.Position + _target.Velocity * secSinceLastUpdate;
                    }
                    Vector3D range = estimatedTargetPos - missilePos;
                    double dist = range.Length();
                    Vector3D rangeUnit = dist == 0 ? Vector3D.Zero : range / dist;
                    Vector3D relVel = _target.Velocity - missileVel;
                    double closingSpeed = -Vector3D.Dot(rangeUnit, relVel);
                    double timeToTarget = dist / closingSpeed;

                    Vector3D gravVector = SystemCoordinator.ReferenceGravity;
                    MatrixD referenceOrientation = SystemCoordinator.ReferenceWorldMatrix.GetOrientation();

                    Vector3D vectorToAlign;
                    Vector3D accelVector;
                    switch (Stage)
                    {
                        case MissileStage.Launching:
                            if (time - _launchTime < _dismountPeriod)
                            {
                                Vector3D dismountVector;
                                double dismountAccel = _maxThrust[_dismountDirection] / _missileMass;
                                switch (_dismountDirection)
                                {
                                    case Direction.Up:
                                        dismountVector = referenceOrientation.Up;
                                        break;
                                    case Direction.Down:
                                        dismountVector = referenceOrientation.Down;
                                        break;
                                    case Direction.Left:
                                        dismountVector = referenceOrientation.Left;
                                        break;
                                    case Direction.Right:
                                        dismountVector = referenceOrientation.Right;
                                        break;
                                    case Direction.Forward:
                                        dismountVector = referenceOrientation.Forward;
                                        break;
                                    case Direction.Backward:
                                        dismountVector = referenceOrientation.Backward;
                                        break;
                                    default:
                                        dismountVector = referenceOrientation.Up;
                                        break;
                                }
                                accelVector = dismountVector * dismountAccel;
                            }
                            else
                            {
                                Vector3D launchVector;
                                double launchAccel = _maxThrust[_launchDirection] / _missileMass;
                                switch (_launchDirection)
                                {
                                    case Direction.Up:
                                        launchVector = referenceOrientation.Up;
                                        break;
                                    case Direction.Down:
                                        launchVector = referenceOrientation.Down;
                                        break;
                                    case Direction.Left:
                                        launchVector = referenceOrientation.Left;
                                        break;
                                    case Direction.Right:
                                        launchVector = referenceOrientation.Right;
                                        break;
                                    case Direction.Forward:
                                        launchVector = referenceOrientation.Forward;
                                        break;
                                    case Direction.Backward:
                                        launchVector = referenceOrientation.Backward;
                                        break;
                                    default:
                                        launchVector = referenceOrientation.Forward;
                                        break;
                                }
                                accelVector = launchVector * launchAccel;
                            }

                            if (gravVector.LengthSquared() > 0)
                            {
                                double accelMag = accelVector.Length();
                                Vector3D accelUnit = accelMag != 0 ? accelVector / accelMag : Vector3D.Zero;
                                Vector3D gravCompensation = -gravVector - Vector3D.Dot(-gravVector, accelUnit) * accelUnit;
                                accelVector += gravCompensation;
                            }
                            vectorToAlign = referenceOrientation.Forward;

                            if (time - _launchTime > _launchPeriod)
                            {
                                Stage = MissileStage.Flying;
                            }

                            break;

                        case MissileStage.Flying:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
                            if (gravVector.LengthSquared() > 0)
                            {
                                double accelMag = accelVector.Length();
                                Vector3D accelUnit = accelMag != 0 ? accelVector / accelMag : Vector3D.Zero;
                                Vector3D gravCompensation = -gravVector - Vector3D.Dot(-gravVector, accelUnit) * accelUnit;
                                accelVector += gravCompensation;
                            }
                            vectorToAlign = rangeUnit;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            if (timeToTarget > 0 && timeToTarget < 10)
                            {
                                Stage = MissileStage.Interception;
                                _payload.ForEach(w => w.IsArmed = true);
                                _proxySensor.Enabled = true;
                            }

                            break;

                        case MissileStage.Interception:

                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTargetPos, _target.Velocity, missilePos, missileVel);
                            if (gravVector.LengthSquared() > 0)
                            {
                                double accelMag = accelVector.Length();
                                Vector3D accelUnit = accelMag != 0 ? accelVector / accelMag : Vector3D.Zero;
                                Vector3D gravCompensation = -gravVector - Vector3D.Dot(-gravVector, accelUnit) * accelUnit;
                                accelVector += gravCompensation;
                            }
                            vectorToAlign = rangeUnit;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            Vector3D targetDirCamera = Vector3D.TransformNormal(estimatedTargetPos - _proxySensor.GetPosition(), MatrixD.Transpose(_proxySensor.WorldMatrix)).Normalized();
                            MyDetectedEntityInfo detection = _proxySensor.Raycast(_proxySensorRange, targetDirCamera);

                            if (!detection.IsEmpty() && detection.EntityId == _target.EntityID)
                            {
                                _payload.ForEach(w => w.Detonate());
                            }
                            break;

                        default:
                            vectorToAlign = referenceOrientation.Forward;
                            accelVector = Vector3D.Zero;
                            break;
                    }

                    Vector3D vectorToAlignLocal = Vector3D.TransformNormal(vectorToAlign, MatrixD.Transpose(referenceOrientation));
                    double dot = Vector3D.Dot(Vector3D.Forward, vectorToAlignLocal);
                    double epsilon = 1e-6;
                    Vector3D rotationVector;
                    if (dot <= -1 + epsilon)
                    {
                        rotationVector = Vector3D.Right;
                    }
                    else if (dot >= 1 - epsilon)
                    {
                        rotationVector = Vector3D.Zero;
                    }
                    else
                    {
                        rotationVector = Vector3D.Cross(Vector3D.Forward, vectorToAlignLocal);
                    }
                    double rotationAngle = Math.Acos(MathHelper.Clamp(dot, -1, 1));
                    Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, (float)rotationAngle);
                    MatrixD alignedMatrixLocal = MatrixD.CreateFromQuaternion(quaternion);

                    double yawError = Math.Atan2(-alignedMatrixLocal.M13, alignedMatrixLocal.M11);
                    double pitchError = Math.Atan2(-alignedMatrixLocal.M32, alignedMatrixLocal.M22);
                    float yawCorrection = _yawController.Run((float)yawError, (float)timeDelta);
                    float pitchCorrection = _pitchController.Run((float)pitchError, (float)timeDelta);

                    Vector3 momentLocal = new Vector3(pitchCorrection, yawCorrection, 0);
                    Vector3 momentWorld = Vector3D.TransformNormal(momentLocal, referenceOrientation);

                    foreach (Gyro gyro in _gyros)
                    {
                        Vector3 momentGyro = Vector3D.TransformNormal(momentWorld, MatrixD.Transpose(gyro.GyroBlock.WorldMatrix.GetOrientation()));
                        gyro.Pitch = momentGyro.X;
                        gyro.Yaw = momentGyro.Y;
                    }


                    double alignment = Vector3D.Dot(vectorToAlign, referenceOrientation.Forward);
                    Vector3D desiredThrustVector = accelVector * _missileMass;
                    foreach (var thrusterGroup in _thrusterGroups)
                    {
                        if (alignment > 0.9f)
                        {
                            double value = Vector3D.Dot(desiredThrustVector, thrusterGroup.Vector);
                            if (value < 0) value = 0;
                            thrusterGroup.ThrustOverride = (float)value;
                        }
                        else
                        {
                            thrusterGroup.ThrustOverride = 0;
                        }
                    }
                }
                _time = time;
            }

            private void ClampAndAlign(Vector3D currentVectorToAlign, ref Vector3D accelVector, out Vector3D newVectorToAlign)
            {
                double accelMag = accelVector.Length();
                if (accelMag > _maxAccel)
                {
                    accelVector = accelVector / accelMag * _maxAccel;
                    accelMag = _maxAccel;
                }                

                double minForwardAccel;
                double maxForwardAccel;

                if (accelMag <= _maxRadialAccel)
                {
                    minForwardAccel = 0;
                    maxForwardAccel = _maxForwardAccel;
                }
                else
                {
                    minForwardAccel = Math.Sqrt(accelMag * accelMag - _maxRadialAccel * _maxRadialAccel);
                    maxForwardAccel = _maxForwardAccel;
                }

                double currentForwardAccel = Vector3D.Dot(accelVector, currentVectorToAlign);

                if (currentForwardAccel >= minForwardAccel && currentForwardAccel <= maxForwardAccel)
                {
                    newVectorToAlign = currentVectorToAlign;
                    return;
                }

                Vector3D accelDir = accelMag == 0 ? Vector3D.Zero : accelVector / accelMag;
                double dot = Vector3D.Dot(accelDir, currentVectorToAlign);
                Vector3D rotationVector;
                double epsilon = 1e-6;

                if (dot <= -1 + epsilon)
                {
                    rotationVector = Vector3D.CalculatePerpendicularVector(currentVectorToAlign);
                }
                else if (dot >= 1 - epsilon)
                {
                    rotationVector = Vector3D.Zero;
                }
                else
                {
                    rotationVector = Vector3D.Cross(currentVectorToAlign, accelDir);
                }

                double targetForwardAccel = currentForwardAccel < minForwardAccel ? minForwardAccel : maxForwardAccel;

                double currentAccelAngle = accelMag == 0 ? 0 : Math.Acos(MathHelper.Clamp(currentForwardAccel / accelMag, -1, 1));
                double targetAccelAngle = accelMag == 0 ? 0 : Math.Acos(MathHelper.Clamp(targetForwardAccel / accelMag, -1, 1));
                double rotationAngle = -1 * (targetAccelAngle - currentAccelAngle);

                Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationVector, (float)rotationAngle);
                newVectorToAlign = Vector3D.Transform(currentVectorToAlign, quaternion);
            }

            public void Activate()
            {
                if (Stage != MissileStage.Idle)
                {
                    return;
                }
                Stage = MissileStage.Active;
                Config.Set("Config", "Stage", MissileEnumHelper.GetMissileStageStr(Stage));
                MePb.CustomData = Config.ToString();

                _antenna.Enabled = true;
            }

            public void Deactivate()
            {
                if (Stage >= MissileStage.Launching)
                {
                    return;
                }
                Stage = MissileStage.Idle;
                Config.Set("Config", "Stage", MissileEnumHelper.GetMissileStageStr(Stage));
                MePb.CustomData = Config.ToString();

                _antenna.Enabled = false;
            }

            public void Launch()
            {
                if (Stage != MissileStage.Active)
                {
                    return;
                }
                Stage = MissileStage.Launching;
                Config.Set("Config", "Stage", MissileEnumHelper.GetMissileStageStr(Stage));
                MePb.CustomData = Config.ToString();

                _h2Tanks.ForEach(t => t.TankBlock.Stockpile = false);
                _batteries.ForEach(b => b.BatteryBlock.ChargeMode = ChargeMode.Discharge);

                foreach (var thrusterGroup in _thrusterGroups)
                {
                    foreach (var thruster in thrusterGroup.Thrusters)
                    {
                        thruster.ThrusterBlock.Enabled = true;
                    }
                }
                _gyros.ForEach(g => g.GyroBlock.Enabled = true);

                _launchTime = _time;
            }

            public void UpdateTarget(EntityInfo target)
            {
                _target = target;
            }

            public void Abort()
            {
                if (Stage > MissileStage.Launching && (_time - _launchTime) > 10)
                {
                    foreach (IMyWarhead warhead in _payload)
                    {
                        warhead.IsArmed = true;
                        warhead.Detonate();
                    }
                }
            }
        }
    }
}
