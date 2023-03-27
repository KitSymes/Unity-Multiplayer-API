namespace KitSymes.GTRP.Packets
{
    public class PacketServerRPC : Packet
    {
        public uint networkObjectID;
        public uint networkBehaviourID;
        public uint methodID;
        public byte[] data;
    }
}
