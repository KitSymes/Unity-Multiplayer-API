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

        private bool _spawned = false;

        private List<Packet> _tcpPackets = new List<Packet>();
        private List<Packet> _udpPackets = new List<Packet>();

        private Dictionary<uint, NetworkBehaviour> _networkBehaviours = new Dictionary<uint, NetworkBehaviour>();
        private uint _networkBehavioursCount = 0;

        void Awake()
        {

        }

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

        public void Spawn(uint networkID, uint ownerNetworkID)
        {
            if (_spawned)
                return;
            _spawned = true;

            _networkID = networkID;
            _ownerNetworkID = ownerNetworkID;

            gameObject.SetActive(true);
        }

        public uint RegisterNetworkBehaviour(NetworkBehaviour networkBehaviour)
        {
            uint key = _networkBehavioursCount;
            _networkBehaviours.Add(key, networkBehaviour);
            _networkBehavioursCount++;
            return key;
        }

        public void AddTCPPacket(Packet packet) { _tcpPackets.Add(packet); }
        public List<Packet> GetTCPPackets() { return _tcpPackets; }
        public void AddUDPPacket(Packet packet) { _udpPackets.Add(packet); }
        public List<Packet> GetUDPPackets() { return _udpPackets; }

        public List<Packet> GetAllFullSyncPackets()
        {
            List<Packet> packets = new List<Packet>();
            foreach (NetworkBehaviour networkBehaviour in _networkBehaviours.Values)
                packets.Add(networkBehaviour.CreateFullSyncPacket());
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
        public uint GetOwnerID() { return _ownerNetworkID; }

        public bool IsSpawned() { return _spawned; }

        public bool HasNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours.ContainsKey(networkBehaviourID); }
        public NetworkBehaviour GetNetworkBehaviour(uint networkBehaviourID) { return _networkBehaviours[networkBehaviourID]; }
    }
}
