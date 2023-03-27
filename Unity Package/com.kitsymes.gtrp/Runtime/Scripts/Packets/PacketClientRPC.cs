using System.Net;

namespace KitSymes.GTRP.Packets
{
    public class PacketClientRPC : Packet
    {
        public uint networkObjectID;
        public uint networkBehaviourID;
        public uint methodID;
        public byte[] data;
    }
}
