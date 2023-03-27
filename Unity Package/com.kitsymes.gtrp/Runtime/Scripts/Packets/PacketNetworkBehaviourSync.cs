using System;
using System.Runtime.Serialization;

namespace KitSymes.GTRP.Packets
{
    public class PacketNetworkBehaviourSync : Packet, ISerializable
    {
        public uint networkObjectID;
        public uint networkBehaviourID;

        public DateTime timestamp;

        public byte[] data;

        public PacketNetworkBehaviourSync()
        {
            timestamp = DateTime.UtcNow;
        }

        protected PacketNetworkBehaviourSync(SerializationInfo info, StreamingContext context)
        {

        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            
        }
    }
}
