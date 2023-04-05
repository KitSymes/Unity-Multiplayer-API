using KitSymes.GTRP.Packets;
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
        [SerializeField]
        private bool _spawnOnStart = false;
        private bool _isServer = false;

        private readonly List<Packet> _tcpPackets = new List<Packet>();
        private readonly List<Packet> _udpPackets = new List<Packet>();

        private readonly Dictionary<uint, NetworkBehaviour> _networkBehaviours = new Dictionary<uint, NetworkBehaviour>();
        private uint _networkBehavioursCount = 0;

        void Start()
        {
            _isServer = NetworkManager.IsServer();

            if (!_spawned)
            {
                gameObject.SetActive(false);
                if (_spawnOnStart)
                    if (_isServer)
                        NetworkManager.Spawn(gameObject);
                    else
                        Destroy(gameObject);
            }
        }

        public void Tick()
        {
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                networkBehaviour.Tick();
        }

        void OnDestroy()
        {
            if (_spawned && NetworkManager.IsServer())
                NetworkManager.Despawn(this);
        }

        public bool IsSpawned() { return _spawned; }
        public void Spawn(uint networkID, uint ownerNetworkID, bool hasAuthority)
        {
            if (_spawned)
                return;

            _networkID = networkID;
            ChangeOwnership(ownerNetworkID);
            ChangeAuthority(hasAuthority);

            _spawned = true;
            gameObject.SetActive(true);
        }
        public PacketSpawnObject GetSpawnPacket()
        {
            return new PacketSpawnObject
            {
                prefabID          = _prefabID,
                objectNetworkID   = _networkID,
                ownerNetworkID    = _ownerNetworkID,
                ownerHasAuthority = _hasAuthoriy,
                position          = transform.position,
                rotation          = transform.rotation,
                localScale        = transform.localScale,
            };
        }

        public void AddTCPPacket(Packet packet) { _tcpPackets.Add(packet); }
        public List<Packet> GetTCPPackets() { return _tcpPackets; }
        public void AddUDPPacket(Packet packet) { _udpPackets.Add(packet); }
        public List<Packet> GetUDPPackets() { return _udpPackets; }
        public void ClearPackets()
        {
            _tcpPackets.Clear();
            _udpPackets.Clear();
        }

        public List<Packet> GetAllFullSyncPackets()
        {
            List<Packet> packets = new List<Packet>();
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                packets.Add(networkBehaviour.CreateSyncPacket(false));
            return packets;
        }
        public List<Packet> GetAllDynamicSyncPackets()
        {
            List<Packet> packets = new List<Packet>();
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                if (networkBehaviour.HasChanged())
                    packets.Add(networkBehaviour.CreateSyncPacket(true));
            return packets;
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
        public void ChangeAuthority(bool hasAuthority)
        {
            if (hasAuthority == _hasAuthoriy)
                return;

            _hasAuthoriy = hasAuthority;
            if (_spawned && NetworkManager.IsServer())
                _tcpPackets.Add(new PacketAuthorityChange()
                {
                    networkObjectID = _networkID,
                    hasAuthority = hasAuthority
                });
        }

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

        public bool IsServer() { return _isServer; }
    }
}
