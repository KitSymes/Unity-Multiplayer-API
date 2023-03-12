using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketNetworkBehaviourSync : Packet, ISerializable
    {
        public uint networkObjectID;
        public uint networkBehaviourID;

        public List<byte> dataList = new List<byte>();
        public byte[] data;

        public PacketNetworkBehaviourSync() { }

        protected PacketNetworkBehaviourSync(SerializationInfo info, StreamingContext context)
        {

        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            
        }
    }
}
