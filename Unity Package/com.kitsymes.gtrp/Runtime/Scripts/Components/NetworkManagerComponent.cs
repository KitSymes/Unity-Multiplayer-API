using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KitSymes.GTRP.Components
{
    public class NetworkManagerComponent : MonoBehaviour
    {
        [SerializeField]
        private NetworkManager _networkManager = new NetworkManager();

        [Tooltip("The IP a Client will attempt to connect to.")]
        public string ip = "127.0.0.1";
        [Tooltip("The Port a Server will listen on and a Client will attempt to connect to.")]
        public int port = 25565;

        [Tooltip("The File Path to the Offline Scene.")]
        public string offlineScene;
        [Tooltip("The File Path to the Online Scene.")]
        public string onlineScene;

        [Tooltip("The List of Spawnable Prefabs.\nPrefabs must have a NetworkObject component.")]
        public List<NetworkObject> spawnablePrefabs;

        [Tooltip("The Player Prefabs to spawn (and give ownership to) for each client.\nMust have a NetworkObject component.")]
        public NetworkObject playerPrefab;

        void Awake()
        {
            _networkManager.SetSpawnableObjects(spawnablePrefabs);
            _networkManager.SetPlayerPrefab(playerPrefab);
            _networkManager.OnServerStart += OnStartServer;
            _networkManager.OnServerStop += OnServerStop;
            _networkManager.OnClientStart += OnClientStart;
            _networkManager.OnClientStop += OnClientStop;

#if UNITY_EDITOR
            for (uint i = 0; i < spawnablePrefabs.Count; i++)
                spawnablePrefabs[(int)i].SetPrefabID(i);
#endif

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void LateUpdate()
        {
            _networkManager.LateUpdate();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            for (uint i = 0; i < spawnablePrefabs.Count; i++)
                spawnablePrefabs[(int)i].SetPrefabID(i);
        }
#endif

        void OnDestroy()
        {
            if (IsClient())
                ClientStop();
            if (IsServer())
                ServerStop();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        public NetworkManager GetNetworkManager() { return _networkManager; }

        #region Server Methods
        public void ServerStart()
        {
            DontDestroyOnLoad(gameObject);
            bool started = _networkManager.ServerStart(port);
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

        #region Events
        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && scene.path == "Assets/" + onlineScene + ".unity")
                _networkManager.BeginProcessingPackets();
        }

        void SharedStart()
        {
            if (!IsServer() || !IsClient())
            {
                SceneManager.LoadScene(onlineScene);
            }
        }
        void SharedStop()
        {
            if (!IsServer() && !IsClient())
            {
                Destroy(gameObject);
                SceneManager.LoadScene(offlineScene);
            }
        }

        void OnStartServer()
        {
            SharedStart();
        }
        void OnServerStop()
        {
            SharedStop();
        }

        void OnClientStart()
        {
            SharedStart();

            if (IsServer())
                _networkManager.BeginProcessingPackets();
        }
        void OnClientStop()
        {
            SharedStop();
        }
        #endregion
    }
}
