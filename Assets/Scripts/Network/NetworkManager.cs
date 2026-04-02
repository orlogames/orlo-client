using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Orlo.Network
{
    /// <summary>
    /// Manages TCP connection to the Orlo game server.
    /// Length-prefixed framing: 4 bytes big-endian length + protobuf payload.
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Server Connection")]
        [SerializeField] private string serverHost = "play.orlo.games";
        [SerializeField] private int serverPort = 7777;

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<byte[]> OnPacketReceived;

        public bool IsConnected => _client?.Connected ?? false;
        public string ServerHost => $"{serverHost}:{serverPort}";

        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<byte[]> _incomingQueue = new();
        private readonly ConcurrentQueue<byte[]> _outgoingQueue = new();

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Drain incoming packets on main thread
            while (_incomingQueue.TryDequeue(out var packet))
            {
                OnPacketReceived?.Invoke(packet);
            }
        }

        public void SetServer(string host, int port)
        {
            serverHost = host;
            serverPort = port;
        }

        public async void Connect()
        {
            try
            {
                _cts = new CancellationTokenSource();
                _client = new TcpClient();
                await _client.ConnectAsync(serverHost, serverPort);
                _stream = _client.GetStream();

                Debug.Log($"[Network] Connected to {serverHost}:{serverPort}");
                OnConnected?.Invoke();

                _ = Task.Run(() => ReadLoop(_cts.Token));
                _ = Task.Run(() => WriteLoop(_cts.Token));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Network] Connection failed: {ex.Message}");
            }
        }

        public void Send(byte[] data)
        {
            _outgoingQueue.Enqueue(data);
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            _stream?.Close();
            _client?.Close();
            Debug.Log("[Network] Disconnected");
            OnDisconnected?.Invoke();
        }

        private async Task ReadLoop(CancellationToken ct)
        {
            var headerBuf = new byte[4];
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    // Read 4-byte length prefix
                    await ReadExact(headerBuf, 4, ct);
                    int length = (headerBuf[0] << 24) | (headerBuf[1] << 16)
                               | (headerBuf[2] << 8) | headerBuf[3];

                    if (length > 1024 * 1024)
                    {
                        Debug.LogError($"[Network] Oversized packet: {length} bytes");
                        break;
                    }

                    // Read payload
                    var body = new byte[length];
                    await ReadExact(body, length, ct);
                    _incomingQueue.Enqueue(body);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Network] Read error: {ex.Message}");
            }
        }

        private async Task WriteLoop(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    if (_outgoingQueue.TryDequeue(out var data))
                    {
                        // Length-prefix frame
                        var frame = new byte[4 + data.Length];
                        int len = data.Length;
                        frame[0] = (byte)((len >> 24) & 0xFF);
                        frame[1] = (byte)((len >> 16) & 0xFF);
                        frame[2] = (byte)((len >> 8) & 0xFF);
                        frame[3] = (byte)(len & 0xFF);
                        Buffer.BlockCopy(data, 0, frame, 4, data.Length);

                        await _stream.WriteAsync(frame, 0, frame.Length, ct);
                    }
                    else
                    {
                        await Task.Delay(1, ct);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Network] Write error: {ex.Message}");
            }
        }

        private async Task ReadExact(byte[] buffer, int count, CancellationToken ct)
        {
            int offset = 0;
            while (offset < count)
            {
                int read = await _stream.ReadAsync(buffer, offset, count - offset, ct);
                if (read == 0) throw new Exception("Connection closed");
                offset += read;
            }
        }

        private void OnDestroy()
        {
            Disconnect();
        }
    }
}
