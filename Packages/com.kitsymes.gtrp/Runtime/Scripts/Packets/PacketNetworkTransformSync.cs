using System;
using System.Runtime.Serialization;
using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketNetworkTransformSync : Packet, ISerializable
    {
        private bool _containsPosition;
        private bool _containsRotation;
        private bool _containsScale;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;

        public PacketNetworkTransformSync(bool containsPosition, bool containsRotation, bool containsScale)
        {
            _containsPosition = containsPosition;
            _containsRotation = containsRotation;
            _containsScale = containsScale;
        }
        
        protected PacketNetworkTransformSync(SerializationInfo info, StreamingContext context)
        {
            byte config = info.GetByte("config");

            // Check to see if the bits are set indicating this information was sent
            if (((config >> 0) & 1) != 0)
            {
                position.x = info.GetSingle("position.x");
                position.y = info.GetSingle("position.y");
                position.z = info.GetSingle("position.z");
            }

            if (((config >> 1) & 1) != 0)
            {
                rotation.x = info.GetSingle("rotation.x");
                rotation.y = info.GetSingle("rotation.y");
                rotation.z = info.GetSingle("rotation.z");
                rotation.w = info.GetSingle("rotation.w");
            }

            if (((config >> 2) & 1) != 0)
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
    }
}
