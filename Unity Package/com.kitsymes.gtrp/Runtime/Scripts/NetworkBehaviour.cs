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
            InitialiseSyncData();
            InitialiseClientRPCs();
        }

        void Update()
        {
            if (!networkObject.IsSpawned())
                return;
            if (HasChanged())
                networkObject.AddUDPPacket(CreateSyncPacket(true));
        }

        public virtual void Tick() { }

        public virtual void InitialiseSyncData() { }
        public virtual bool HasChanged() { return false; }
        protected virtual List<byte> GetFullData() { return new List<byte>(); }
        protected virtual List<byte> GetDynamicData() { return new List<byte>(); }
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

        public virtual uint InitialiseClientRPCs() { return 0; }
        public PacketClientRPC CreateClientRPCPacket(uint methodID, byte[] data) { return new PacketClientRPC() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id, methodID = methodID, data = data }; }
        public virtual void OnPacketClientRPCReceive(PacketClientRPC packet) { }
        
        public virtual uint InitialiseServerRPCs() { return 0; }
        public PacketServerRPC CreateServerRPCPacket(uint methodID, byte[] data) { return new PacketServerRPC() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id, methodID = methodID, data = data }; }
        public virtual void OnPacketServerRPCReceive(PacketServerRPC packet) { }

        public virtual void OnServerStart() { }
        public virtual void OnClientStart() { }
        public virtual void OnPacketReceive(Packet packet) { }
        public virtual void OnOwnershipChange(uint oldClient, uint newClient) { }
        public virtual void OnAuthorityChange(bool oldValue, bool newValue) { }
    }
}
