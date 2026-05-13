using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Emit;
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
        public class CameraArray
        {
            private List<IMyCameraBlock> _cameras = new List<IMyCameraBlock>();
            private PriorityQueue<IMyCameraBlock, double> _cameraQueue;
            private MovingAverage _avgRaycastDistance = new MovingAverage(100);
            private double _timeLastRaycast;
            private bool _enabled = true;
            private bool _raycastEnabled = true;
            public string ID { get; private set; }
            public float MaxRaycastDistance { get; set; }
            public bool Recharging => SystemTime - _timeLastRaycast < Period;
            public int CameraCount => _cameras.Count;
            public double Period => _avgRaycastDistance.Average / (_cameras[0].RaycastTimeMultiplier * 1000 * CameraCount);
            public double Frequency => 1 / Period;
            public bool Enabled
            {
                get
                {
                    return _enabled;
                }
                set
                {
                    foreach (var camera in _cameras)
                    {
                        camera.Enabled = value;
                    }
                    _enabled = value;
                }
            }
            public bool RaycastEnabled
            {
                get
                {
                    return _raycastEnabled;
                }
                set
                {
                    foreach (var camera in _cameras)
                    {
                        camera.EnableRaycast = value;
                    }
                    _raycastEnabled = value;
                }
            }
            public bool IsFunctional => _cameras.All(c => c.IsFunctional);
            public CameraArray(string id, float maxRaycastDistance)
            {
                ID = id.ToUpper();
                MaxRaycastDistance = maxRaycastDistance;

                Init();
            }

            private void Init()
            {
                _cameras = AllBlocks.Where(b => b is IMyCameraBlock && b.CustomName.ToUpper().Contains($"CAMERA ARRAY {ID} CAMERA")).Cast<IMyCameraBlock>().ToList();
                if (_cameras.Count == 0)
                {
                    throw new Exception($"Camera Array {ID} has no cameras!");
                }

                foreach (var camera in _cameras)
                {
                    camera.Enabled = true;
                    camera.EnableRaycast = true;
                }

                Func<IMyCameraBlock, double> prioritySelector = c => -c.AvailableScanRange;
                _cameraQueue = new PriorityQueue<IMyCameraBlock, double>(prioritySelector, _cameras);
            }

            public MyDetectedEntityInfo Raycast(Vector3D raycastTarget)
            {
                if (CanScan(raycastTarget))
                {
                    IMyCameraBlock nextCamera = _cameraQueue.Dequeue();
                    var result = nextCamera.Raycast(raycastTarget);
                    double raycastDistance = Vector3D.Distance(raycastTarget, nextCamera.GetPosition());
                    _avgRaycastDistance.Add(raycastDistance);
                    _cameraQueue.Enqueue(nextCamera);
                    _timeLastRaycast = SystemTime;
                    return result;
                }
                else
                {
                    return default(MyDetectedEntityInfo);
                }
            }

            public MyDetectedEntityInfo Raycast(Vector3D raycastTarget, float overshoot)
            {
                Vector3D raycastOvershoot = (raycastTarget - GetCameraPosition()).Normalized() * overshoot;
                raycastTarget += raycastOvershoot;

                return Raycast(raycastTarget);
            }

            public bool CanScan(Vector3D raycastTarget)
            {
                IMyCameraBlock nextCamera = _cameraQueue.Peek();
                double raycastDistance = Vector3D.Distance(raycastTarget, nextCamera.GetPosition());

                if (nextCamera.CanScan(raycastTarget) && raycastDistance < MaxRaycastDistance && !Recharging)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public bool CanScan(Vector3D raycastTarget, float overshoot)
            {
                Vector3D raycastOvershoot = (raycastTarget - GetCameraPosition()).Normalized() * overshoot;
                raycastTarget += raycastOvershoot;

                return CanScan(raycastTarget);
            }

            public Vector3D GetCameraPosition() => _cameraQueue.Peek().GetPosition();

            public void AddCamera(IMyCameraBlock camera)
            {
                if (!_cameras.Contains(camera))
                {
                    _cameras.Add(camera);
                    camera.EnableRaycast = true;
                    _cameraQueue.Enqueue(camera);
                }
            }

            public void RemoveCamera(IMyCameraBlock camera)
            {
                if (_cameras.Contains(camera))
                {
                    _cameras.Remove(camera);
                    _cameraQueue = new PriorityQueue<IMyCameraBlock, double>(c => -c.AvailableScanRange, _cameras);
                }
            }
        }
    }
}
