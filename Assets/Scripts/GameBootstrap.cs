using UnityEngine;
using Orlo.Network;

namespace Orlo
{
    /// <summary>
    /// Entry point — creates core managers and initiates server connection.
    /// Attach to an empty GameObject in the boot scene.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Server")]
        [SerializeField] private string serverHost = "127.0.0.1";
        [SerializeField] private int serverPort = 7777;

        private void Start()
        {
            Debug.Log("[Orlo] Bootstrapping...");

            // Connect to game server
            NetworkManager.Instance.OnConnected += OnConnected;
            NetworkManager.Instance.OnDisconnected += OnDisconnected;
            NetworkManager.Instance.Connect();
        }

        private void OnConnected()
        {
            Debug.Log("[Orlo] Connected to server — sending login...");
            // TODO: Send LoginRequest protobuf
        }

        private void OnDisconnected()
        {
            Debug.Log("[Orlo] Disconnected from server");
        }
    }
}
