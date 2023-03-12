using KitSymes.GTRP.Packets;
using UnityEngine;

namespace KitSymes.GTRP
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkBehaviour : MonoBehaviour, INetworkMessageTarget
    {
        protected NetworkObject networkObject;
        /// <summary>
        /// The ID of this NetworkBehaviour, used to uniquely identify it from the others on the same NetworkObject
        /// </summary>
        private uint _id;

        void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            _id = networkObject.RegisterNetworkBehaviour(this);
            Initialise();
        }

        void Update()
        {
            if (!networkObject.IsSpawned())
                return;
            if (HasChanged())
                networkObject.AddUDPPacket(CreateDynamicSyncPacket());
        }

        public bool IsOwner()
        {
            return false;
        }

        public virtual void Initialise() { }
        public virtual bool HasChanged() { return false; }
        public virtual PacketNetworkBehaviourSync CreateDynamicSyncPacket() { return new PacketNetworkBehaviourSync() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id }; }
        public virtual PacketNetworkBehaviourSync CreateFullSyncPacket() { return new PacketNetworkBehaviourSync() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id }; }

        public virtual int ParseSyncPacket(PacketNetworkBehaviourSync packet) { return 0; }
        public virtual void OnServerStart() { }
        public virtual void OnPacketReceive(Packet packet) { }

    }
}
