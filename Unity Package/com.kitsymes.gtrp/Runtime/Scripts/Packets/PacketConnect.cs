using System.Net;
using System.Security.Cryptography;

namespace KitSymes.GTRP.Packets
{
    public class PacketConnect : Packet
    {
        public IPEndPoint udpEndPoint;
        public RSAParameters publicKey;
    }
}
