using System;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketDespawnObject : Packet
    {
        public uint objectNetworkID;
    }
}
