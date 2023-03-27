using UnityEngine;

namespace KitSymes.GTRP.Packets
{
    public class PacketSpawnObject : Packet
    {
        public uint prefabID;
        public uint objectNetworkID;
        public uint ownerNetworkID;
        public bool ownerHasAuthority;

        public Vector3 position;
        public Quaternion rotation;
        public Vector3 localScale;
    }
}
