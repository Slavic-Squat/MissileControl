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
        public class MissileGuidance
        {
            public float MaxAccel { get; set; }
            public float M { get; set; }
            public float N { get; set; }
            public float MaxSpeed { get; set; }

            public MissileGuidance(float maxAccel, float m, float n, float maxSpeed = 100)
            {
                MaxAccel = maxAccel;
                M = m;
                N = n;
                MaxSpeed = maxSpeed;
            }

            public Vector3D CalculateTotalAccel(Vector3D targetPos, Vector3D targetVel, Vector3D missilePos, Vector3D missileVel)
            {
                Vector3D range = targetPos - missilePos;
                double dist = range.Length();
                Vector3D rangeUnit = dist != 0 ? range / dist : Vector3D.Zero;
                Vector3D relVel = targetVel - missileVel;
                double relSpeed = relVel.Length();
                Vector3D relVelUnit = relSpeed != 0 ? relVel / relSpeed : Vector3D.Zero;
                double axialSpeed = Vector3D.Dot(relVel, rangeUnit);
                Vector3D axialVel = axialSpeed * rangeUnit;
                Vector3D lateralVel = relVel - axialVel;
                double lateralSpeed = lateralVel.Length();
                Vector3D lateralVelUnit = lateralSpeed != 0 ? lateralVel / lateralSpeed : Vector3D.Zero;

                //double axialSpeedFactor = Math.Max(0, 1 - axialSpeed / MaxSpeed);
                //double distFactor = Math.Min(dist / 1000, 1);
                
                Vector3D lateralAccel = N * lateralSpeed / dist * Math.Abs(axialSpeed) * lateralVelUnit;
                double lateralAccelMag = lateralAccel.Length();
                double freeAccel;
                if (lateralAccelMag > MaxAccel)
                {
                    lateralAccel = lateralAccel / lateralAccelMag * MaxAccel;
                    freeAccel = 0;
                }
                else
                {
                    freeAccel = Math.Sqrt(MaxAccel * MaxAccel - lateralAccelMag * lateralAccelMag);
                }

                Vector3D targetLateralVel = targetVel - Vector3D.Dot(targetVel, rangeUnit) * rangeUnit;
                double maxAxialSpeed = Math.Sqrt(MaxSpeed * MaxSpeed - targetLateralVel.LengthSquared());
                Vector3D axialAccel = -axialSpeed > maxAxialSpeed * 0.95 ? Vector3D.Zero : M * freeAccel * rangeUnit;
                //Vector3D axialAccel = M * MaxAccel * distFactor * axialSpeedFactor * rangeUnit;
                Vector3D totalAccel = axialAccel + lateralAccel;
                double totalAccelMag = totalAccel.Length();

                if (totalAccelMag > MaxAccel)
                {
                    totalAccel = totalAccel / totalAccelMag * MaxAccel;
                }

                return totalAccel;
            }
        }
    }
}
