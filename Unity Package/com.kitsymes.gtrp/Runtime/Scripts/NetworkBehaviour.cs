using KitSymes.GTRP.Packets;
using System;
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
        private DateTime _lastUpdate;

        void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            _id = networkObject.RegisterNetworkBehaviour(this);
            _lastUpdate = DateTime.UtcNow;
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

        public bool ShouldParseSyncPacket(PacketNetworkBehaviourSync packet) { return _lastUpdate.CompareTo(packet.timestamp) < 0; }
        public virtual int ParseSyncPacket(PacketNetworkBehaviourSync packet) { _lastUpdate = packet.timestamp; return 0; }

        public virtual void OnServerStart() { }
        public virtual void OnPacketReceive(Packet packet) { }

    }
}
