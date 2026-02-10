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
        public struct EntityInfo
        {
            public long EntityID { get; private set; }
            public EntityType Type => MissileInfo.IsValid ? EntityType.Missile : EntityType.Target;
            public Vector3D Position { get; private set;  }
            public Vector3D Velocity { get; private set; }
            public double TimeRecorded { get; private set; }
            public MissileInfo MissileInfo { get; private set; }
            public bool IsValid { get; private set; }

            public EntityInfo(long entityID, Vector3D position, Vector3D velocity, double timeRecorded)
            {
                EntityID = entityID;
                Position = position;
                Velocity = velocity;
                TimeRecorded = timeRecorded;
                MissileInfo = default(MissileInfo);
                IsValid = true;
            }

            public EntityInfo(MyDetectedEntityInfo entityInfo, double timeRecorded)
            {
                EntityID = entityInfo.EntityId;
                Position = entityInfo.Position;
                Velocity = entityInfo.Velocity;
                TimeRecorded = timeRecorded;
                MissileInfo = default(MissileInfo);
                IsValid = true;
            }

            public EntityInfo(long entityID, Vector3D position, Vector3D velocity, double timeRecorded, MissileInfo missileInfo)
            {
                EntityID = entityID;
                Position = position;
                Velocity = velocity;
                TimeRecorded = timeRecorded;
                MissileInfo = missileInfo;
                IsValid = true;
            }

            public EntityInfo Merge(EntityInfo other)
            {
                if (EntityID != other.EntityID)
                {
                    return this;
                }
                MergeKinematics(other);

                if (Type == EntityType.Target && other.Type == EntityType.Missile)
                {
                    if (other.MissileInfo.IsValid)
                    {
                        MissileInfo = other.MissileInfo;
                    }
                }
                return this;
            }

            public EntityInfo MergeKinematics(EntityInfo other)
            {
                if (EntityID != other.EntityID)
                {
                    return this;
                }
                if (TimeRecorded < other.TimeRecorded)
                {
                    Position = other.Position;
                    Velocity = other.Velocity;
                    TimeRecorded = other.TimeRecorded;
                }
                return this;
            }

            public int Serialize(byte[] bytes, int offset)
            {
                int index = offset;
                bytes[index++] = (byte)Type;
                MiscUtilities.WriteInt64(bytes, index, EntityID);
                index += 8;
                MyFixedPoint PosX = (MyFixedPoint)Position.X;
                MiscUtilities.WriteInt64(bytes, index, PosX.RawValue);
                index += 8;
                MyFixedPoint PosY = (MyFixedPoint)Position.Y;
                MiscUtilities.WriteInt64(bytes, index, PosY.RawValue);
                index += 8;
                MyFixedPoint PosZ = (MyFixedPoint)Position.Z;
                MiscUtilities.WriteInt64(bytes, index, PosZ.RawValue);
                index += 8;

                MyFixedPoint VelX = (MyFixedPoint)Velocity.X;
                MiscUtilities.WriteInt64(bytes, index, VelX.RawValue);
                index += 8;
                MyFixedPoint VelY = (MyFixedPoint)Velocity.Y;
                MiscUtilities.WriteInt64(bytes, index, VelY.RawValue);
                index += 8;
                MyFixedPoint VelZ = (MyFixedPoint)Velocity.Z;
                MiscUtilities.WriteInt64(bytes, index, VelZ.RawValue);
                index += 8;
                TimeSpan timeSpan = TimeSpan.FromSeconds(TimeRecorded);
                MiscUtilities.WriteInt64(bytes, index, timeSpan.Ticks);
                index += 8;

                if (Type == EntityType.Missile)
                {
                    index += MissileInfo.Serialize(bytes, index);
                }
                return index - offset;
            }

            public static EntityInfo Deserialize(ImmutableArray<byte> bytes, int offset, out int bytesRead)
            {
                bytesRead = 0;
                int index = offset;

                if (bytes.Length - index < 65)
                {
                    return new EntityInfo();
                }
                EntityType type = (EntityType)bytes[index];
                index += 1;

                long entityID = MiscUtilities.ReadInt64(bytes, index);
                index += 8;

                MyFixedPoint temp = new MyFixedPoint();

                long posXRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = posXRaw;
                double xPos = (double)temp;
                index += 8;

                long posYRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = posYRaw;
                double yPos = (double)temp;
                index += 8;

                long posZRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = posZRaw;
                double zPos = (double)temp;
                index += 8;

                Vector3D pos = new Vector3D(xPos, yPos, zPos);

                long velXRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = velXRaw;
                double xVel = (double)temp;
                index += 8;

                long velYRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = velYRaw;
                double yVel = (double)temp;
                index += 8;

                long velZRaw = MiscUtilities.ReadInt64(bytes, index);
                temp.RawValue = velZRaw;
                double zVel = (double)temp;
                index += 8;

                Vector3D vel = new Vector3D(xVel, yVel, zVel);

                long timeTicks = MiscUtilities.ReadInt64(bytes, index);
                double timeRecorded = TimeSpan.FromTicks(timeTicks).TotalSeconds;
                index += 8;

                if (type == EntityType.Missile)
                {
                    int missileBytesRead;
                    MissileInfo missileInfo = MissileInfo.Deserialize(bytes, index, out missileBytesRead);
                    index += missileBytesRead;
                    bytesRead = index - offset;
                    if (!missileInfo.IsValid)
                    {
                        return new EntityInfo();
                    }
                    return new EntityInfo(entityID, pos, vel, timeRecorded, missileInfo);
                }
                else
                {
                    bytesRead = index - offset;
                    return new EntityInfo(entityID, pos, vel, timeRecorded);
                }
            }
        }
    }
}
