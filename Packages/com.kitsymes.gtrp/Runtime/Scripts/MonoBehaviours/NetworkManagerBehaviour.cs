using System.Collections.Generic;
using UnityEngine;

namespace KitSymes.GTRP.MonoBehaviours
{
    public class NetworkManagerBehaviour : MonoBehaviour
    {
        private NetworkManager _networkManager;

        [Tooltip("The IP a Client will attempt to connect to.")]
        public string ip = "127.0.0.1";
        [Tooltip("The Port a Server will listen on and a Client will attempt to connect to.")]
        public int port = 25565;

        [Tooltip("The File Path to the Offline Scene.\nHas no effect if changed during Play Mode.")]
        public string offlineScene;
        [Tooltip("The File Path to the Online Scene.\nHas no effect if changed during Play Mode.")]
        public string onlineScene;

        [Tooltip("The List of Spawnable Prefabs.\nPrefabs must have a NetworkObject component.")]
        public List<NetworkObject> spawnablePrefabs;

        void Awake()
        {
            _networkManager = new NetworkManager();
            _networkManager.SetOfflineScene(offlineScene);
            _networkManager.SetOnlineScene(onlineScene);
            _networkManager.SetSpawnableObjects(spawnablePrefabs);

#if UNITY_EDITOR
            for (uint i = 0; i < spawnablePrefabs.Count; i++)
                spawnablePrefabs[(int)i].SetPrefabID(i);
#endif
        }

        void Update()
        {
            _networkManager.Update();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            for (uint i = 0; i < spawnablePrefabs.Count; i++)
                spawnablePrefabs[(int)i].SetPrefabID(i);
        }
#endif

        #region Server Methods
        public void ServerStart()
        {
            DontDestroyOnLoad(gameObject);
            _networkManager.ServerStart(ip, port);
        }

        public void ServerStop()
        {
            _networkManager.ServerStop();
        }

        public bool IsServer()
        {
            return _networkManager.IsServerRunning();
        }
        #endregion

        #region Client Methods
        public void ClientStart()
        {
            DontDestroyOnLoad(gameObject);
            _ = _networkManager.ClientStart(ip, port);
        }

        public void ClientStop()
        {
            _networkManager.ClientStop();
        }

        public bool IsClient()
        {
            return _networkManager.IsClientRunning();
        }
        #endregion

    }
}
