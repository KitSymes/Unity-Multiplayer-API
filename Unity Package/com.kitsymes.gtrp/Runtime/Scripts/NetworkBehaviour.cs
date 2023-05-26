using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkBehaviour : MonoBehaviour, INetworkMessageTarget
    {
        /// <summary>
        /// The <see cref="NetworkObject"/> this NetworkBehaviour is attached to.
        /// </summary>
        protected NetworkObject networkObject;
        /// <summary>
        /// The ID of this NetworkBehaviour, used to uniquely identify it from the others on the same <see cref="NetworkObject"/>.
        /// </summary>
        private uint _id;
        /// <summary>
        /// The last time this NetworkBehaviour received a <see cref="PacketNetworkBehaviourSync"/>.
        /// </summary>
        private DateTime _lastUpdate;

        /// <summary>
        /// Make sure to call <c>base.Awake()</c> when overriding!
        /// </summary>
        public void Awake()
        {
            networkObject = GetComponent<NetworkObject>();
            _id = networkObject.RegisterNetworkBehaviour(this);
            _lastUpdate = DateTime.MinValue;
            InitialiseSyncData();
            InitialiseClientRPCs();
        }

        #region API only intended Methods
        /// <summary>
        /// Tick this NetworkBehaviour.
        /// </summary>
        public virtual void Tick()
        {
            if (!networkObject.IsSpawned())
                return;
            if (HasChanged())
                networkObject.AddUDPPacket(CreateSyncPacket(true));
        }

        /// <summary>
        /// Overriden through the Source Generator.
        /// </summary>
        public virtual void InitialiseSyncData() { }
        /// <summary>
        /// Check if this NetworkBehaviour has changed and should be synchronised to the other side.
        /// Overriden through the Source Generator.
        /// </summary>
        /// <returns>True if this NetworkBehaviour has changed.</returns>
        public virtual bool HasChanged() { return false; }
        /// <summary>
        /// Get all data that needs to be synchronised, regardless of if it has changed since last tick.
        /// Overriden through the Source Generator.
        /// </summary>
        /// <returns>All data that should be synchronised.</returns>
        protected virtual List<byte> GetFullData() { return new List<byte>(); }
        /// <summary>
        /// Get data that needs to be synchronised because it has changed since last tick.
        /// Overriden through the Source Generator.
        /// </summary>
        /// <returns>Data that should be synchronised.</returns>
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
        /// <summary>
        /// Parse the given <see cref="PacketNetworkBehaviourSync"/>, and update <see cref="_lastUpdate"/>.
        /// Overriden through the Source Generator.
        /// </summary>
        /// <param name="packet">The <see cref="PacketNetworkBehaviourSync"/> to parse.</param>
        /// <returns>The bytes used by this class when parsing, for inheritance purposes.</returns>
        public virtual int ParseSyncPacket(PacketNetworkBehaviourSync packet) { _lastUpdate = packet.timestamp; return 0; }

        public virtual uint InitialiseClientRPCs() { return 0; }
        public PacketClientRPC CreateClientRPCPacket(uint methodID, byte[] data) { return new PacketClientRPC() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id, methodID = methodID, data = data }; }
        public virtual void OnPacketClientRPCReceive(PacketClientRPC packet) { }
        
        public virtual uint InitialiseServerRPCs() { return 0; }
        public PacketServerRPC CreateServerRPCPacket(uint methodID, byte[] data) { return new PacketServerRPC() { networkObjectID = networkObject.GetNetworkID(), networkBehaviourID = _id, methodID = methodID, data = data }; }
        public virtual void OnPacketServerRPCReceive(PacketServerRPC packet) { }
#endregion

        // Overridable Event Methods
        public virtual void OnServerStart() { }
        public virtual void OnClientStart() { }
        public virtual void OnPacketReceive(Packet packet) { }
        public virtual void OnOwnershipChange(uint oldClient, uint newClient) { }
        public virtual void OnAuthorityChange(bool oldValue, bool newValue) { }
    }
}
