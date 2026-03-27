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
            private double _lastRunTime;
            private List<Gyro> _gyros = new List<Gyro>();
            private List<IMyWarhead> _payload = new List<IMyWarhead>();
            private List<ThrusterGroup> _thrusterGroups = new List<ThrusterGroup>();
            private Dictionary<Direction, float> _maxThrust = new Dictionary<Direction, float>();
            private IMyRadioAntenna _antenna;
            private List<GasTank> _h2Tanks = new List<GasTank>();
            private List<Battery> _batteries = new List<Battery>();
            private IMyRemoteControl _remoteCtrl;
            private IMyCameraBlock _proxySensor;
            private IMyShipConnector _connector;
            private IMyShipMergeBlock _mergeBlock;

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

            private MissilePayload _payloadType;
            private MissileStage _stage = MissileStage.Building;
            private List<Vector4D> _launchBurns = new List<Vector4D>();
            private int _burnIndex = 0;
            private double _burnStartTime = -1;
            private IEnumerator<Vector3D> _launchBurnEnumerator;
            private float _proxySensorRange = 5;
            private float _interceptionThreshold = 3;

            private EntityInfo _target;
            private EntityInfo _lastTarget;
            private double _launchTime;
            private Vector3D _velAtLaunch;
            public MissileStage Stage => GetStage();
            public MissilePayload PayloadType => _payloadType;
            public EntityInfo Target => _target;

            public MissileControl()
            {
                Init();
            }

            private void GetBlocks()
            {
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 0")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 1")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 2")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 3")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 4")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                _thrusterGroups.Add(new ThrusterGroup(AllBlocks.Where(b => b is IMyThrust && b.CustomName.ToUpper().Contains("THRUSTER GROUP 5")).Select(b => new Thruster(b as IMyThrust)).ToArray()));
                if (_thrusterGroups.Count(tg => tg.Thrusters.Count > 0) == 0)
                {
                    throw new Exception("No thrusters found!");
                }
                _gyros = AllBlocks.Where(b => b is IMyGyro).Select(b => new Gyro(b as IMyGyro)).ToList();
                if (_gyros.Count == 0)
                {
                    throw new Exception("No gyros found!");
                }

                _payload = AllBlocks.Where(b => b is IMyWarhead).Cast<IMyWarhead>().ToList();
                if (_payload.Count == 0)
                {
                    throw new Exception("No warheads found!");
                }

                _antenna = AllBlocks.FirstOrDefault(b => b is IMyRadioAntenna) as IMyRadioAntenna;
                if (_antenna == null)
                {
                    throw new Exception("No antenna found!");
                }

                _h2Tanks = AllBlocks.Where(b => b is IMyGasTank).Select(b => new GasTank(b as IMyGasTank)).ToList();
                if (_h2Tanks.Count == 0)
                {
                    throw new Exception("No hydrogen tanks found!");
                }

                _batteries = AllBlocks.Where(b => b is IMyBatteryBlock).Select(b => new Battery(b as IMyBatteryBlock)).ToList();
                if (_batteries.Count == 0)
                {
                    throw new Exception("No batteries found!");
                }

                _remoteCtrl = AllBlocks.FirstOrDefault(b => b is IMyRemoteControl) as IMyRemoteControl;
                if (_remoteCtrl == null)
                {
                    throw new Exception("No remote control found!");
                }

                _proxySensor = AllBlocks.FirstOrDefault(b => b is IMyCameraBlock) as IMyCameraBlock;
                if (_proxySensor == null)
                {
                    throw new Exception("No proxy sensor found!");
                }

                _mergeBlock = AllBlocks.FirstOrDefault(b => b is IMyShipMergeBlock) as IMyShipMergeBlock;
                if (_mergeBlock == null)
                {
                    throw new Exception("No merge block found!");
                }

                _connector = AllBlocks.FirstOrDefault(b => b is IMyShipConnector) as IMyShipConnector;
                if (_connector == null)
                {
                    throw new Exception("No connector found!");
                }
            }

            private void Init()
            {
                GetBlocks();

                _payloadType = MissileEnumHelper.GetMissilePayload(Config.Get("Config", "Payload").ToString(MissileEnumHelper.GetMissilePayloadStr(MissilePayload.HE)));
                Config.Set("Config", "Payload", MissileEnumHelper.GetMissilePayloadStr(_payloadType));

                _missileMass = Config.Get("Config", "Mass").ToSingle(10000);
                Config.Set("Config", "Mass", _missileMass);

                _maxSpeed = Config.Get("Config", "MaxSpeed").ToSingle(200);
                Config.Set("Config", "MaxSpeed", _maxSpeed);

                _m = Config.Get("Config", "M").ToSingle(2);
                Config.Set("Config", "M", _m);

                _n = Config.Get("Config", "N").ToSingle(3.5f);
                Config.Set("Config", "N", _n);

                _kp = Config.Get("Config", "Kp").ToSingle(2.5f);
                Config.Set("Config", "Kp", _kp);

                _ki = Config.Get("Config", "Ki").ToSingle(0f);
                Config.Set("Config", "Ki", _ki);

                _kd = Config.Get("Config", "Kd").ToSingle(0f);
                Config.Set("Config", "Kd", _kd);

                int launchBurnCount = Config.Get("Config", "LaunchBurnCount").ToInt32(1);
                Config.Set("Config", "LaunchBurnCount", launchBurnCount);

                for (int i = 0; i < launchBurnCount; i++)
                {
                    _launchBurns.Add(VectorFromStr(Config.Get("Config", "LaunchBurn" + i).ToString("<0, 0, 0, 0>")));
                    Config.Set("Config", "LaunchBurn" + i, VectorToStr(_launchBurns[i]));
                }

                _proxySensorRange = Config.Get("Config", "ProxySensorRange").ToSingle(5);
                Config.Set("Config", "ProxySensorRange", _proxySensorRange);

                _interceptionThreshold = Config.Get("Config", "InterceptionThreshold").ToSingle(3);
                Config.Set("Config", "InterceptionThreshold", _interceptionThreshold);

                MePb.CustomData = Config.ToString();

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
                if (_lastRunTime == 0)
                {
                    _lastRunTime = time;
                    return;
                }
                double globalTime = SystemCoordinator.GlobalTime;

                double timeDelta = time - _lastRunTime;
                _lastRunTime = time;

                if (Stage < MissileStage.Launching)
                {
                    return;
                }

                _missileMass = _remoteCtrl.CalculateShipMass().TotalMass;
                _maxForwardAccel = _maxThrust[Direction.Forward] / _missileMass;
                _maxRadialAccel = _maxThrust[Direction.Right] / _missileMass;
                _maxAccel = (float)Math.Sqrt(_maxForwardAccel * _maxForwardAccel + _maxRadialAccel * _maxRadialAccel);
                _missileGuidance.MaxAccel = _maxAccel;

                Vector3D missilePos = SystemCoordinator.ReferencePosition;
                Vector3D missileVel = SystemCoordinator.ReferenceVelocity;

                EntityInfo estimatedTarget = EstimateTargetKinematics(_target, _lastTarget);
                Vector3D range = estimatedTarget.Position - missilePos;
                double dist = range.Length();
                Vector3D rangeUnit = dist == 0 ? Vector3D.Zero : range / dist;
                Vector3D relVel = estimatedTarget.Velocity - missileVel;
                double closingSpeed = -Vector3D.Dot(rangeUnit, relVel);
                double timeToTarget = dist / closingSpeed;

                Vector3D gravVector = SystemCoordinator.ReferenceGravity;
                MatrixD referenceOrientation = SystemCoordinator.ReferenceWorldMatrix.GetOrientation();

                Vector3D vectorToAlign;
                Vector3D accelVector;
                switch (Stage)
                {
                    case MissileStage.Launching:
                        {
                            if (_launchBurnEnumerator == null)
                            {
                                _launchBurnEnumerator = GetLaunchBurns();
                            }

                            if (_launchBurnEnumerator.MoveNext())
                            {
                                accelVector = Vector3D.TransformNormal(_launchBurnEnumerator.Current, referenceOrientation.GetOrientation());
                            }
                            else
                            {
                                accelVector = Vector3D.Zero;
                                _stage = MissileStage.Flying;
                                MePb.CubeGrid.CustomName = "MISSILE";
                            }

                            double accelMag = accelVector.Length();
                            Vector3D accelDir = accelMag == 0 ? Vector3D.Zero : accelVector / accelMag;

                            Vector3D velToMaintain = _velAtLaunch - Vector3D.Dot(_velAtLaunch, accelDir) * accelDir;
                            double freeSpeed = Math.Sqrt(_maxSpeed * _maxSpeed - velToMaintain.LengthSquared());

                            if (freeSpeed < 0)
                            {
                                accelVector = Vector3D.Zero;
                            }

                            if (gravVector.LengthSquared() > 0)
                            {
                                //Vector3D gravComp = -gravVector - Vector3D.Dot(-gravVector, accelDir) * accelDir;
                                Vector3D gravComp = -gravVector;
                                accelVector += gravComp;
                            }
                            vectorToAlign = referenceOrientation.Forward;

                            break;
                        }

                    case MissileStage.Flying:
                        {
                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTarget.Position, estimatedTarget.Velocity, missilePos, missileVel);
                            if (gravVector.LengthSquared() > 0)
                            {
                                //double accelMag = accelVector.Length();
                                //Vector3D accelDir = accelMag != 0 ? accelVector / accelMag : Vector3D.Zero;
                                //Vector3D gravComp = -gravVector - Vector3D.Dot(-gravVector, accelDir) * accelDir;
                                Vector3D gravComp = -gravVector;
                                accelVector += gravComp;
                            }
                            vectorToAlign = rangeUnit;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            if (timeToTarget > 0 && timeToTarget < _interceptionThreshold)
                            {
                                _stage = MissileStage.Interception;
                                _payload.ForEach(w => w.IsArmed = true);
                                _proxySensor.Enabled = true;
                            }

                            break;
                        }

                    case MissileStage.Interception:
                        {
                            accelVector = _missileGuidance.CalculateTotalAccel(estimatedTarget.Position, estimatedTarget.Velocity, missilePos, missileVel);
                            if (gravVector.LengthSquared() > 0)
                            {
                                //double accelMag = accelVector.Length();
                                //Vector3D accelDir = accelMag != 0 ? accelVector / accelMag : Vector3D.Zero;
                                //Vector3D gravComp = -gravVector - Vector3D.Dot(-gravVector, accelDir) * accelDir;
                                Vector3D gravComp = -gravVector;
                                accelVector += gravComp;
                            }
                            vectorToAlign = rangeUnit;
                            ClampAndAlign(vectorToAlign, ref accelVector, out vectorToAlign);

                            MyDetectedEntityInfo detection = _proxySensor.Raycast(_proxySensorRange);

                            if (!detection.IsEmpty() && detection.EntityId == _target.EntityID)
                            {
                                _payload.ForEach(w => w.Detonate());
                            }
                            break;
                        }

                    default:
                        {
                            vectorToAlign = referenceOrientation.Forward;
                            accelVector = Vector3D.Zero;
                        }
                        break;
                }

                Vector3D vectorToAlignLocal = Vector3D.TransformNormal(vectorToAlign, MatrixD.Transpose(referenceOrientation));
                double dot = Vector3D.Dot(Vector3D.Forward, vectorToAlignLocal);
                double epsilon = 1e-6;
                Vector3D rotationAxis;
                if (dot <= -1 + epsilon)
                {
                    rotationAxis = Vector3D.Up;
                }
                else if (dot >= 1 - epsilon)
                {
                    rotationAxis = Vector3D.Zero;
                }
                else
                {
                    rotationAxis = Vector3D.Cross(Vector3D.Forward, vectorToAlignLocal).Normalized();
                }
                double rotationAngle = Math.Acos(MathHelper.Clamp(dot, -1, 1));
                MatrixD alignedLocal = MatrixD.CreateFromAxisAngle(rotationAxis, rotationAngle);

                double yawError = Math.Atan2(-alignedLocal.M13, alignedLocal.M11);
                double pitchError = Math.Atan2(-alignedLocal.M32, alignedLocal.M22);
                float yawCorrection = _yawController.Run((float)yawError, (float)timeDelta);
                float pitchCorrection = _pitchController.Run((float)pitchError, (float)timeDelta);

                Vector3 momentLocal = new Vector3(pitchCorrection, yawCorrection, 0);
                Vector3 momentWorld = Vector3D.TransformNormal(momentLocal, referenceOrientation);

                foreach (Gyro gyro in _gyros)
                {
                    Vector3 momentGyro = Vector3D.TransformNormal(momentWorld, MatrixD.Transpose(gyro.GyroBlock.WorldMatrix.GetOrientation()));
                    gyro.Pitch = momentGyro.X;
                    gyro.Yaw = momentGyro.Y;
                    gyro.Roll = momentGyro.Z;
                }

                Vector3D desiredThrustVector = accelVector * _missileMass;
                foreach (var thrusterGroup in _thrusterGroups)
                {
                    double value = Vector3D.Dot(desiredThrustVector, thrusterGroup.Vector);
                    if (value < 0) value = 0;
                    thrusterGroup.ThrustOverride = (float)value;
                }
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
                Vector3D rotationAxis;
                double epsilon = 1e-6;

                if (dot <= -1 + epsilon)
                {
                    rotationAxis = Vector3D.CalculatePerpendicularVector(currentVectorToAlign);
                }
                else if (dot >= 1 - epsilon)
                {
                    rotationAxis = Vector3D.Zero;
                }
                else
                {
                    rotationAxis = Vector3D.Cross(currentVectorToAlign, accelDir).Normalized();
                }

                double targetForwardAccel = currentForwardAccel < minForwardAccel ? minForwardAccel : maxForwardAccel;

                double currentAccelAngle = accelMag == 0 ? 0 : Math.Acos(MathHelper.Clamp(currentForwardAccel / accelMag, -1, 1));
                double targetAccelAngle = accelMag == 0 ? 0 : Math.Acos(MathHelper.Clamp(targetForwardAccel / accelMag, -1, 1));
                double rotationAngle = -1 * (targetAccelAngle - currentAccelAngle);

                Quaternion quaternion = Quaternion.CreateFromAxisAngle(rotationAxis, (float)rotationAngle);
                newVectorToAlign = Vector3D.Transform(currentVectorToAlign, quaternion);
            }

            private Vector3D DirectionToVector(Direction direction, MatrixD referenceOrientation)
            {
                switch (direction)
                {
                    case Direction.Up:
                        return referenceOrientation.Up;
                    case Direction.Down:
                        return referenceOrientation.Down;
                    case Direction.Left:
                        return referenceOrientation.Left;
                    case Direction.Right:
                        return referenceOrientation.Right;
                    case Direction.Forward:
                        return referenceOrientation.Forward;
                    case Direction.Backward:
                        return referenceOrientation.Backward;
                    default:
                        return referenceOrientation.Forward;
                }
            }

            private Vector4D VectorFromStr(string str)
            {
                string[] parts = str.Trim(' ', '<', '>').Split(',');
                if (parts.Length != 4) return Vector4D.Zero;
                double x, y, z, w;
                if (!double.TryParse(parts[0], out x) || !double.TryParse(parts[1], out y) || !double.TryParse(parts[2], out z) || !double.TryParse(parts[3], out w))
                {
                    return Vector4D.Zero;
                }
                return new Vector4D(x, y, z, w);
            }

            private string VectorToStr(Vector4D vec)
            {
                return string.Format("<{0}, {1}, {2}, {3}>", vec.X, vec.Y, vec.Z, vec.W);
            }

            private IEnumerator<Vector3D> GetLaunchBurns()
            {
                _burnStartTime = SystemTime;
                _burnIndex = 0;
                while (_burnIndex < _launchBurns.Count)
                {
                    Vector4D currentBurn = _launchBurns[_burnIndex];
                    if (SystemTime - _burnStartTime > currentBurn.W)
                    {
                        _burnIndex++;
                        if (_burnIndex >= _launchBurns.Count)
                        {
                            yield break;
                        }
                        _burnStartTime = SystemTime;
                        continue;
                    }
                    yield return new Vector3D(currentBurn.X, currentBurn.Y, currentBurn.Z);
                }
            }

            public void Launch()
            {
                if (Stage != MissileStage.Idle)
                {
                    return;
                }
                _stage = MissileStage.Launching;

                _antenna.Enabled = true;
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
                _connector.Disconnect();
                _mergeBlock.Enabled = false;

                _launchTime = SystemTime;
                _velAtLaunch = SystemCoordinator.ReferenceVelocity;
            }

            public void UpdateTarget(EntityInfo target)
            {
                if (!target.IsValid)
                {
                    return;
                }
                if (_target.EntityID != target.EntityID)
                {
                    _lastTarget = default(EntityInfo);
                    _target = target;
                }
                else if (target.TimeRecorded > _target.TimeRecorded)
                {
                    _lastTarget = _target;
                    _target = target;
                }
            }

            public void ForgetTarget()
            {
                _target = default(EntityInfo);
                _lastTarget = default(EntityInfo);
            }

            private EntityInfo EstimateTargetKinematics(EntityInfo currentTarget, EntityInfo lastTarget)
            {
                Vector3D accel = Vector3D.Zero;
                if (lastTarget.IsValid && currentTarget.EntityID == lastTarget.EntityID)
                {
                    Vector3D velDelta = currentTarget.Velocity - lastTarget.Velocity;
                    double recordedTimeDelta = currentTarget.TimeRecorded - lastTarget.TimeRecorded;
                    if (recordedTimeDelta > 0)
                    {
                        accel = velDelta / recordedTimeDelta;
                    }
                }

                double timeSinceLastRecord = SystemCoordinator.GlobalTime - currentTarget.TimeRecorded;
                Vector3D estimatedVel = currentTarget.Velocity + accel * timeSinceLastRecord;
                Vector3D estimatedPos = currentTarget.Position + currentTarget.Velocity * timeSinceLastRecord + 0.5 * accel * timeSinceLastRecord * timeSinceLastRecord;

                return new EntityInfo(currentTarget.EntityID, estimatedPos, estimatedVel, SystemCoordinator.GlobalTime);
            }

            public void Abort()
            {
                ForgetTarget();
                if (Stage > MissileStage.Launching && (SystemTime - _launchTime) > 10)
                {
                    foreach (IMyWarhead warhead in _payload)
                    {
                        warhead.IsArmed = true;
                        warhead.Detonate();
                    }
                }
            }

            public MissileStage GetStage()
            {
                if (_stage < MissileStage.Idle)
                {
                    switch (_stage)
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
                            if (!_connector.IsFunctional) break;
                            if (!_mergeBlock.IsFunctional) break;
                            _stage = MissileStage.Fueling;
                            break;
                        case MissileStage.Fueling:
                            if (_h2Tanks.Average(t => t.FillPercentage) < 75f) break;
                            if (_batteries.Average(t => t.ChargePercentage) < 30f) break;
                            _stage = MissileStage.Idle;
                            break;
                    }
                }
                return _stage;
            }
        }
    }
}
