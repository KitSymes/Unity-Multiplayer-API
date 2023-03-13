using System;
using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketSpawnObject : Packet
    {
        public uint prefabID;
        public uint objectNetworkID;
        public uint ownerNetworkID;

        public float positionX, positionY, positionZ;
        public float rotationX, rotationY, rotationZ, rotationW;
        public float localScaleX, localScaleY, localScaleZ;

        public Vector3 GetPosition() { return new Vector3(positionX, positionY, positionZ); }
        public Quaternion GetRotation() { return new Quaternion(rotationX, rotationY, rotationZ, rotationW); }
        public Vector3 GetScale() { return new Vector3(localScaleX, localScaleY, localScaleZ); }
    }
}
