using KitSymes.GTRP.Internal;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    public class PacketNetworkTransformSync : PacketTargeted
    {
        public bool containsPosition;
        public bool containsRotation;
        public bool containsScale;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;

        public DateTime timestamp;

        public PacketNetworkTransformSync()
        {
            timestamp = DateTime.UtcNow;
        }

        public override void Deserialise(byte[] bytes, int pointer)
        {
            int used;

            byte config = bytes[pointer];
            pointer++;
            containsPosition = ((config >> 0) & 1) != 0;
            containsRotation = ((config >> 1) & 1) != 0;
            containsScale = ((config >> 2) & 1) != 0;

            target = BitConverter.ToUInt32(bytes, pointer);
            pointer += sizeof(int);
            timestamp = ByteConverter.ToDateTime(bytes, out used, pointer);
            pointer += used;

            // Check to see if the bits are set indicating this information was sent
            if (containsPosition)
            {
                position = ByteConverter.ToVector3(bytes, out used, pointer);
                pointer += used;
            }

            if (containsRotation)
            {
                rotation = ByteConverter.ToQuaternion(bytes, out used, pointer);
                pointer += used;
            }

            if (containsScale)
            {
                localScale = ByteConverter.ToVector3(bytes, out used, pointer);
                pointer += used;
            }
        }

        public override List<byte> Serialise()
        {
            List<byte> bytes = new List<byte>();
            byte config = 0;
            // If this information needs to be sent, set a bit flag
            if (containsPosition)
                config |= 1 << 0;
            if (containsRotation)
                config |= 1 << 1;
            if (containsScale)
                config |= 1 << 2;
            bytes.Add(config);

            bytes.AddRange(BitConverter.GetBytes(target));
            bytes.AddRange(ByteConverter.GetBytes(timestamp));

            if (containsPosition)
                bytes.AddRange(ByteConverter.GetBytes(position));
            if (containsRotation)
                bytes.AddRange(ByteConverter.GetBytes(rotation));
            if (containsScale)
                bytes.AddRange(ByteConverter.GetBytes(localScale));

            return bytes;
        }
    }
}
