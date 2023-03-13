using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketNetworkTransformSync : PacketTargeted, ISerializable
    {
        private bool _containsPosition;
        private bool _containsRotation;
        private bool _containsScale;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;

        public DateTime timestamp;

        public PacketNetworkTransformSync(uint target, bool containsPosition, bool containsRotation, bool containsScale)
        {
            this.target = target;
            _containsPosition = containsPosition;
            _containsRotation = containsRotation;
            _containsScale = containsScale;
            timestamp = DateTime.UtcNow;
        }
        
        protected PacketNetworkTransformSync(SerializationInfo info, StreamingContext context)
        {
            byte config = info.GetByte("config");
            _containsPosition = ((config >> 0) & 1) != 0;
            _containsRotation = ((config >> 1) & 1) != 0;
            _containsScale = ((config >> 2) & 1) != 0;

            target = info.GetUInt32("target");
            timestamp = info.GetDateTime("timestamp");

            // Check to see if the bits are set indicating this information was sent
            if (_containsPosition)
            {
                position.x = info.GetSingle("position.x");
                position.y = info.GetSingle("position.y");
                position.z = info.GetSingle("position.z");
            }

            if (_containsRotation)
            {
                rotation.x = info.GetSingle("rotation.x");
                rotation.y = info.GetSingle("rotation.y");
                rotation.z = info.GetSingle("rotation.z");
                rotation.w = info.GetSingle("rotation.w");
            }

            if (_containsScale)
            {
                localScale.x = info.GetSingle("localScale.x");
                localScale.y = info.GetSingle("localScale.y");
                localScale.z = info.GetSingle("localScale.z");
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            byte config = 0;
            // If this information needs to be sent, set a bit flag
            if (_containsPosition)
                config |= 1 << 0;
            if (_containsRotation)
                config |= 1 << 1;
            if (_containsScale)
                config |= 1 << 2;
            info.AddValue("config", config);

            info.AddValue("target", target);
            info.AddValue("timestamp", timestamp);

            if (_containsPosition)
            {
                info.AddValue("position.x", position.x);
                info.AddValue("position.y", position.y);
                info.AddValue("position.z", position.z);
            }

            if (_containsRotation)
            {
                info.AddValue("rotation.x", rotation.x);
                info.AddValue("rotation.y", rotation.y);
                info.AddValue("rotation.z", rotation.z);
                info.AddValue("rotation.w", rotation.w);
            }

            if (_containsScale)
            {
                info.AddValue("localScale.x", localScale.x);
                info.AddValue("localScale.y", localScale.y);
                info.AddValue("localScale.z", localScale.z);
            }
        }

        public bool HasPosition() { return _containsPosition; }
        public bool HasRotation() { return _containsRotation; }
        public bool HasScale() { return _containsScale; }
    }
}
