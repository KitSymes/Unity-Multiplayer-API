using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
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
                networkObject.AddUDPPacket(CreateSyncPacket(true));
        }

        public virtual void Initialise() { }
        public virtual bool HasChanged() { return false; }
        public virtual List<byte> GetFullData() { return new List<byte>(); }
        public virtual List<byte> GetDynamicData() { return new List<byte>(); }
        public PacketNetworkBehaviourSync CreateSyncPacket(bool dynamic)
        {
            PacketNetworkBehaviourSync packet = new PacketNetworkBehaviourSync()
            {
                networkObjectID = networkObject.GetNetworkID(),
                networkBehaviourID = _id
            };
            packet.data = dynamic ? GetDynamicData().ToArray() : GetFullData().ToArray();
            return packet;
        }

        public bool ShouldParseSyncPacket(PacketNetworkBehaviourSync packet) { return _lastUpdate.CompareTo(packet.timestamp) < 0; }
        public virtual int ParseSyncPacket(PacketNetworkBehaviourSync packet) { _lastUpdate = packet.timestamp; return 0; }

        public virtual void OnServerStart() { }
        public virtual void OnClientStart() { }
        public virtual void OnPacketReceive(Packet packet) { }
        public virtual void OnOwnershipChange(uint oldClient, uint newClient) { }
        public virtual void OnAuthorityChange(bool oldValue, bool newValue) { }
    }
}
