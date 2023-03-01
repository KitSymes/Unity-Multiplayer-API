using UnityEngine;
using UnityEngine.UI;

namespace KitSymes.GTRP.MonoBehaviours
{
    public class NetworkGUI : MonoBehaviour
    {
        [SerializeField]
        private NetworkMonoBehaviour _server;
        [SerializeField]
        private Text _serverButtonText;
        [SerializeField]
        private Text _clientButtonText;

        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

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

        public void SetIP(string ip) { _server.ip = ip; }

        public void SetPort(string port)
        {
            int portInt;
            if (int.TryParse(port, out portInt))
                _server.port = portInt;
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
    }
}
