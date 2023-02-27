using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace KitSymes.GTRP
{
    public class NetworkMonoBehaviour : MonoBehaviour
    {
        private NetworkManager _networkManager;

        public string ip = "127.0.0.1";
        public int port = 25565;

        void Awake()
        {
            _networkManager = new NetworkManager();
        }

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
