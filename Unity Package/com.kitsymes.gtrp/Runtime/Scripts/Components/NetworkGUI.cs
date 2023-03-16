using UnityEngine;
using UnityEngine.UI;

namespace KitSymes.GTRP.Components
{
    public class NetworkGUI : MonoBehaviour
    {
        [SerializeField]
        private NetworkManagerComponent _server;
        [Header("UI Elements")]
        [SerializeField]
        private GameObject _serverButton;
        [SerializeField]
        private Text _serverButtonText;
        [SerializeField]
        private GameObject _ipField;
        [SerializeField]
        private GameObject _portField;
        [SerializeField]
        private GameObject _clientButton;
        [SerializeField]
        private Text _clientButtonText;

        void Awake()
        {
            _server.GetNetworkManager().OnServerStart += OnServerStart;
            _server.GetNetworkManager().OnServerStop += OnServerStop;
            _server.GetNetworkManager().OnClientStart += OnClientStart;
            _server.GetNetworkManager().OnClientStop += OnClientStop;
        }

        public void SetIP(string ip) { _server.ip = ip; }
        public void SetPort(string port)
        {
            int portInt;
            if (int.TryParse(port, out portInt))
                _server.port = portInt;
        }

        public void ToggleServer()
        {
            if (!_server.IsServer())
            {
                _server.ServerStart();
                _serverButtonText.text = "Stop Server";
            }
            else
            {
                _server.ServerStop();
                _serverButtonText.text = "Start Server";
            }
        }
        public void ToggleClient()
        {
            if (!_server.IsClient())
            {
                _server.ClientStart();
                _clientButtonText.text = "Stop Client";
            }
            else
            {
                _server.ClientStop();
                _clientButtonText.text = "Start Client";
            }
        }

        public void OnServerStart()
        {
            _ipField.SetActive(false);
            _portField.SetActive(false);
        }
        public void OnServerStop()
        {
            if (!_server.IsClient())
            {
                _ipField.SetActive(true);
                _portField.SetActive(true);
            }
        }

        public void OnClientStart()
        {
            if (!_server.IsServer())
                _serverButton.SetActive(false);
            _ipField.SetActive(false);
            _portField.SetActive(false);
        }
        public void OnClientStop()
        {
            if (!_server.IsServer())
            {
                _serverButton.SetActive(true);
                _ipField.SetActive(true);
                _portField.SetActive(true);
            }
        }
    }
}
