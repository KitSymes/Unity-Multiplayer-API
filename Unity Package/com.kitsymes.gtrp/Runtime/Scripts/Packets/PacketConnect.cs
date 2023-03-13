using System;
using System.Net;

namespace KitSymes.GTRP.Packets
{
    [Serializable]
    public class PacketConnect : Packet
    {
        public EndPoint udpEndPoint;

        public PacketConnect()
        {
        }
    }
}
