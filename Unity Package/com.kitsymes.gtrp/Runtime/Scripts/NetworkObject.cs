using KitSymes.GTRP.Packets;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP
{
    public sealed class NetworkObject : MonoBehaviour
    {
        [SerializeField]
        private uint _prefabID;

        private uint _networkID;
        private uint _ownerNetworkID = 0;
        private bool _isOwner = false;
        [SerializeField]
        private bool _hasAuthoriy = false;
        private bool _spawned = false;

        private readonly List<Packet> _tcpPackets = new List<Packet>();
        private readonly List<Packet> _udpPackets = new List<Packet>();

        private readonly Dictionary<uint, NetworkBehaviour> _networkBehaviours = new Dictionary<uint, NetworkBehaviour>();
        private uint _networkBehavioursCount = 0;

        void Start()
        {
            if (!_spawned)
                gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_spawned && NetworkManager.IsServer())
                NetworkManager.Despawn(this);
        }

        public bool IsSpawned() { return _spawned; }
        public void Spawn(uint networkID, uint ownerNetworkID)
        {
            if (_spawned)
                return;
            _spawned = true;

            _networkID = networkID;
            ChangeOwnership(ownerNetworkID);

            gameObject.SetActive(true);
        }
        public PacketSpawnObject GetSpawnPacket()
        {
            return new PacketSpawnObject
            {
                prefabID = _prefabID,
                objectNetworkID = _networkID,
                ownerNetworkID = _ownerNetworkID,
                ownerHasAuthority = _hasAuthoriy,
                positionX = transform.position.x,
                positionY = transform.position.y,
                positionZ = transform.position.z,
                rotationX = transform.rotation.x,
                rotationY = transform.rotation.y,
                rotationZ = transform.rotation.z,
                rotationW = transform.rotation.w,
                localScaleX = transform.localScale.x,
                localScaleY = transform.localScale.y,
                localScaleZ = transform.localScale.z,
            };
        }

        public void AddTCPPacket(Packet packet) { _tcpPackets.Add(packet); }
        public List<Packet> GetTCPPackets() { return _tcpPackets; }
        public void AddUDPPacket(Packet packet) { _udpPackets.Add(packet); }
        public List<Packet> GetUDPPackets() { return _udpPackets; }
        public List<Packet> GetAllFullSyncPackets()
        {
            List<Packet> packets = new List<Packet>();
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                packets.Add(networkBehaviour.CreateSyncPacket(false));
            return packets;
        }
        public void ClearPackets()
        {
            _tcpPackets.Clear();
            _udpPackets.Clear();
        }

        public uint GetPrefabID() { return _prefabID; }
#if UNITY_EDITOR
        public void SetPrefabID(uint id) { _prefabID = id; }
#endif
        public uint GetNetworkID() { return _networkID; }

        public bool IsOwner() { return _isOwner; }
        public uint GetOwnerID() { return _ownerNetworkID; }
        public void ChangeOwnership(uint newOwner)
        {
            if (newOwner == _ownerNetworkID) return;

            _ownerNetworkID = newOwner;
            _isOwner = (NetworkManager.IsServer() && newOwner == 0) ||
                (NetworkManager.IsClient() && newOwner == NetworkManager.GetClientID());

            if (_spawned && NetworkManager.IsServer())
                _tcpPackets.Add(new PacketOwnershipChange()
                {
                    networkObjectID = _networkID,
                    clientID = newOwner
                });
        }

        public bool HasAuthority() { return _hasAuthoriy; }
        public void ChangeAuthority(bool hasAuthority) { _hasAuthoriy = hasAuthority; }

        public bool HasNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours.ContainsKey(networkBehaviourID); }
        public NetworkBehaviour GetNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours[networkBehaviourID]; }
        public uint RegisterNetworkBehaviour(NetworkBehaviour networkBehaviour)
        {
            uint key = _networkBehavioursCount;
            _networkBehaviours.Add(key, networkBehaviour);
            _networkBehavioursCount++;
            return key;
        }

        internal void HostSpawn(PacketSpawnObject packet)
        {
            _isOwner = (NetworkManager.IsServer() && packet.ownerNetworkID == 0) ||
                (NetworkManager.IsClient() && packet.ownerNetworkID == NetworkManager.GetClientID());
        }
    }
}
