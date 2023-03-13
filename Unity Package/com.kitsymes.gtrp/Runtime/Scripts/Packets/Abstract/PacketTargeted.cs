using System;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public abstract class PacketTargeted : Packet
    {
        public uint target;
    }
}
