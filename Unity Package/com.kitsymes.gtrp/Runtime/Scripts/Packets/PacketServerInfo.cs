using System.Security.Cryptography;

namespace KitSymes.GTRP.Packets
{
    public class PacketServerInfo : Packet
    {
        public uint yourClientID;
        public RSAParameters publicKey;
    }
}
