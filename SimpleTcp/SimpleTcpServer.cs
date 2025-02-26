﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp server with SSL support.  
    /// Set the ClientConnected, ClientDisconnected, and DataReceived events.  
    /// Once set, use Start() to begin listening for connections.
    /// </summary>
    public class SimpleTcpServer : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Indicates if the server is listening for connections.
        /// </summary>
        public bool IsListening { get; private set; } = false;

        /// <summary>
        /// SimpleTcp server settings.
        /// </summary>
        public SimpleTcpServerSettings Settings
        {
            get => _Settings;
            set => _Settings = value ?? new SimpleTcpServerSettings();
        }

        /// <summary>
        /// SimpleTcp server events.
        /// </summary>
        public SimpleTcpServerEvents Events
        {
            get => _Events;
            set => _Events = value ?? new SimpleTcpServerEvents();
        }

        /// <summary>
        /// SimpleTcp statistics.
        /// </summary>
        public SimpleTcpStatistics Statistics { get; private set; } = new SimpleTcpStatistics();

        /// <summary>
        /// SimpleTcp keepalive settings.
        /// </summary>
        public SimpleTcpKeepaliveSettings Keepalive
        {
            get => _Keepalive;
            set => _Keepalive = value ?? new SimpleTcpKeepaliveSettings();
        }

        /// <summary>
        /// Method to invoke to send a log message.
        /// </summary>
        public Action<string> Logger = null;

        #endregion

        #region Private-Members

        private const string _Header = "[SimpleTcp.Server] ";
        private SimpleTcpServerSettings _Settings = new SimpleTcpServerSettings();
        private SimpleTcpServerEvents _Events = new SimpleTcpServerEvents();
        private SimpleTcpKeepaliveSettings _Keepalive = new SimpleTcpKeepaliveSettings();
        private readonly string _ListenerIp = null;
        private readonly IPAddress _IPAddress = null;
        private readonly int _Port = 0;
        private readonly bool _Ssl = false;

        private readonly X509Certificate2 _SslCertificate = null;
        private readonly X509Certificate2Collection _SslCertificateCollection = null;

        private readonly ConcurrentDictionary<string, ClientMetadata> _Clients = new ConcurrentDictionary<string, ClientMetadata>();
        private readonly ConcurrentDictionary<string, DateTime> _ClientsLastSeen = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, DateTime> _ClientsKicked = new ConcurrentDictionary<string, DateTime>();
        private readonly ConcurrentDictionary<string, DateTime> _ClientsTimedout = new ConcurrentDictionary<string, DateTime>();

        private TcpListener _Listener = null;
        private CancellationTokenSource _TokenSource = new CancellationTokenSource();
        private CancellationToken _Token;
        private Task _AcceptConnections = null;
        private Task _IdleClientMonitor = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiates the TCP server without SSL.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        public SimpleTcpServer(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _ListenerIp, out _Port);

            if (_Port < 0) throw new ArgumentException("Port must be zero or greater.");
            if (string.IsNullOrEmpty(_ListenerIp))
            {
                _IPAddress = IPAddress.Loopback;
                _ListenerIp = _IPAddress.ToString();
            }
            else if (_ListenerIp == "*" || _ListenerIp == "+")
            {
                _IPAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_ListenerIp, out _IPAddress))
                {
                    _IPAddress = Dns.GetHostEntry(_ListenerIp).AddressList[0];
                    _ListenerIp = _IPAddress.ToString();
                }
            }

            IsListening = false;
            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Instantiates the TCP server without SSL.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address or hostname.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        public SimpleTcpServer(string listenerIp, int port)
        {
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _ListenerIp = listenerIp;
            _Port = port;

            if (string.IsNullOrEmpty(_ListenerIp))
            {
                _IPAddress = IPAddress.Loopback;
                _ListenerIp = _IPAddress.ToString();
            }
            else if (_ListenerIp == "*" || _ListenerIp == "+")
            {
                _IPAddress = IPAddress.Any;
                _ListenerIp = listenerIp;
            }
            else
            {
                if (!IPAddress.TryParse(_ListenerIp, out _IPAddress))
                {
                    _IPAddress = Dns.GetHostEntry(listenerIp).AddressList[0];
                    _ListenerIp = _IPAddress.ToString();
                }
            }

            IsListening = false;
            _Token = _TokenSource.Token;
        }

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="ipPort">The IP:port of the server.</param> 
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpServer(string ipPort, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            Common.ParseIpPort(ipPort, out _ListenerIp, out _Port);
            if (_Port < 0) throw new ArgumentException("Port must be zero or greater.");

            if (string.IsNullOrEmpty(_ListenerIp))
            {
                _IPAddress = IPAddress.Loopback;
                _ListenerIp = _IPAddress.ToString();
            }
            else if (_ListenerIp == "*" || _ListenerIp == "+")
            {
                _IPAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_ListenerIp, out _IPAddress))
                {
                    _IPAddress = Dns.GetHostEntry(_ListenerIp).AddressList[0];
                    _ListenerIp = _IPAddress.ToString();
                }
            }

            _Ssl = ssl;
            IsListening = false;
            _Token = _TokenSource.Token;

            if (!_Ssl)
                return;

            _SslCertificate = string.IsNullOrEmpty(pfxPassword) ? new X509Certificate2(pfxCertFilename) : new X509Certificate2(pfxCertFilename, pfxPassword);

            _SslCertificateCollection = new X509Certificate2Collection
                {
                    _SslCertificate
                };
        }

        /// <summary>
        /// Instantiates the TCP server.  Set the ClientConnected, ClientDisconnected, and DataReceived callbacks.  Once set, use Start() to begin listening for connections.
        /// </summary>
        /// <param name="listenerIp">The listener IP address or hostname.</param>
        /// <param name="port">The TCP port on which to listen.</param>
        /// <param name="ssl">Enable or disable SSL.</param>
        /// <param name="pfxCertFilename">The filename of the PFX certificate file.</param>
        /// <param name="pfxPassword">The password to the PFX certificate file.</param>
        public SimpleTcpServer(string listenerIp, int port, bool ssl, string pfxCertFilename, string pfxPassword)
        {
            if (port < 0) throw new ArgumentException("Port must be zero or greater.");

            _ListenerIp = listenerIp;
            _Port = port;

            if (string.IsNullOrEmpty(_ListenerIp))
            {
                _IPAddress = IPAddress.Loopback;
                _ListenerIp = _IPAddress.ToString();
            }
            else if (_ListenerIp == "*" || _ListenerIp == "+")
            {
                _IPAddress = IPAddress.Any;
            }
            else
            {
                if (!IPAddress.TryParse(_ListenerIp, out _IPAddress))
                {
                    _IPAddress = Dns.GetHostEntry(listenerIp).AddressList[0];
                    _ListenerIp = _IPAddress.ToString();
                }
            }

            _Ssl = ssl;
            IsListening = false;
            _Token = _TokenSource.Token;

            if (!_Ssl)
                return;

            _SslCertificate = string.IsNullOrEmpty(pfxPassword) ? new X509Certificate2(pfxCertFilename) : new X509Certificate2(pfxCertFilename, pfxPassword);

            _SslCertificateCollection = new X509Certificate2Collection
                {
                    _SslCertificate
                };
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dispose of the TCP server.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Start accepting connections.
        /// </summary>
        public void Start()
        {
            if (IsListening) throw new InvalidOperationException("SimpleTcpServer is already running.");

            _Listener = new TcpListener(_IPAddress, _Port);

            _Listener.Start();
            IsListening = true;

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;
            Statistics = new SimpleTcpStatistics();
            _IdleClientMonitor = Task.Run(() => IdleClientMonitor(), _Token);
            _AcceptConnections = Task.Run(() => AcceptConnections(), _Token);
        }

        /// <summary>
        /// Start accepting connections.
        /// </summary>
        /// <returns>Task.</returns>
        public Task StartAsync()
        {
            if (IsListening) throw new InvalidOperationException("SimpleTcpServer is already running.");

            _Listener = new TcpListener(_IPAddress, _Port);

            if (_Keepalive.EnableTcpKeepAlives) EnableKeepalives();

            _Listener.Start();
            IsListening = true;

            _TokenSource = new CancellationTokenSource();
            _Token = _TokenSource.Token;
            Statistics = new SimpleTcpStatistics();
            _IdleClientMonitor = Task.Run(() => IdleClientMonitor(), _Token);
            _AcceptConnections = Task.Run(() => AcceptConnections(), _Token);
            return _AcceptConnections;
        }

        /// <summary>
        /// Stop accepting new connections.
        /// </summary>
        public void Stop()
        {
            if (!IsListening) throw new InvalidOperationException("SimpleTcpServer is not running.");

            IsListening = false;
            _Listener.Stop();
            _TokenSource.Cancel();

            Logger?.Invoke(_Header + "stopped");
        }

        /// <summary>
        /// Retrieve a list of client IP:port connected to the server.
        /// </summary>
        /// <returns>IEnumerable of strings, each containing client IP:port.</returns>
        public IEnumerable<string> GetClients()
        {
            List<string> clients = new List<string>(_Clients.Keys);
            return clients;
        }

        /// <summary>
        /// Determines if a client is connected by its IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <returns>True if connected.</returns>
        public bool IsConnected(string ipPort)
        {
            return string.IsNullOrEmpty(ipPort) ? throw new ArgumentNullException(nameof(ipPort)) : _Clients.TryGetValue(ipPort, out _);
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        public void Send(string ipPort, string data)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            ms.Write(bytes, 0, bytes.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(ipPort, bytes.Length, ms);
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        public void Send(string ipPort, byte[] data)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            MemoryStream ms = new MemoryStream();
            ms.Write(data, 0, data.Length);
            ms.Seek(0, SeekOrigin.Begin);
            SendInternal(ipPort, data.Length, ms);
        }

        /// <summary>
        /// Send data to the specified client by IP:port.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        public void Send(string ipPort, long contentLength, Stream stream)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            SendInternal(ipPort, contentLength, stream);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">String containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, string data, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (string.IsNullOrEmpty(data)) throw new ArgumentNullException(nameof(data));
            if (token == default) token = _Token;
            byte[] bytes = Encoding.UTF8.GetBytes(data);
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(ipPort, bytes.Length, ms, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="data">Byte array containing data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, byte[] data, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (data == null || data.Length < 1) throw new ArgumentNullException(nameof(data));
            if (token == default) token = _Token;
            MemoryStream ms = new MemoryStream();
            await ms.WriteAsync(data, 0, data.Length, token).ConfigureAwait(false);
            ms.Seek(0, SeekOrigin.Begin);
            await SendInternalAsync(ipPort, data.Length, ms, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send data to the specified client by IP:port asynchronously.
        /// </summary>
        /// <param name="ipPort">The client IP:port string.</param>
        /// <param name="contentLength">The number of bytes to read from the source stream to send.</param>
        /// <param name="stream">Stream containing the data to send.</param>
        /// <param name="token">Cancellation token for canceling the request.</param>
        public async Task SendAsync(string ipPort, long contentLength, Stream stream, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));
            if (contentLength < 1) return;
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead) throw new InvalidOperationException("Cannot read from supplied stream.");
            if (token == default) token = _Token;
            await SendInternalAsync(ipPort, contentLength, stream, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Disconnects the specified client.
        /// </summary>
        /// <param name="ipPort">IP:port of the client.</param>
        public void DisconnectClient(string ipPort)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            if (!_Clients.TryGetValue(ipPort, out ClientMetadata client))
            {
                Logger?.Invoke(_Header + "unable to find client: " + ipPort);
            }
            else
            {
                if (!_ClientsTimedout.ContainsKey(ipPort))
                {
                    Logger?.Invoke(_Header + "kicking: " + ipPort);
                    _ClientsKicked.TryAdd(ipPort, DateTime.Now);
                }

                _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
                client.Dispose();
                Logger?.Invoke(_Header + "disposed: " + ipPort);
            }
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Dispose of the TCP server.
        /// </summary>
        /// <param name="disposing">Dispose of resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            try
            {
                if (_Clients != null && _Clients.Count > 0)
                {
                    foreach (var curr in _Clients)
                    {
                        curr.Value.Dispose();
                        Logger?.Invoke(_Header + "disconnected client: " + curr.Key);
                    }
                }

                if (_TokenSource != null && !_TokenSource.IsCancellationRequested)
                {
                    _TokenSource.Cancel();
                    _TokenSource.Dispose();
                }

                _Listener?.Server.Close();
                _Listener?.Server.Dispose();
                _Listener?.Stop();
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "dispose exception:" +
                    Environment.NewLine +
                    e.ToString() +
                    Environment.NewLine);
            }

            IsListening = false;

            Logger?.Invoke(_Header + "disposed");
        }

        private bool IsClientConnected(TcpClient client)
        {
            if (!client.Connected)
                return false;

            if (!client.Client.Poll(0, SelectMode.SelectWrite) || client.Client.Poll(0, SelectMode.SelectError))
                return false;

            byte[] buffer = new byte[1];
            return client.Client.Receive(buffer, SocketFlags.Peek) != 0;
        }

        private async Task AcceptConnections()
        {
            while (!_Token.IsCancellationRequested)
            {
                ClientMetadata client = null;

                try
                {
                    TcpClient tcpClient = await _Listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    string clientIp = tcpClient.Client.RemoteEndPoint.ToString();

                    client = new ClientMetadata(tcpClient);

                    if (_Ssl)
                    {
                        client.SslStream = _Settings.AcceptInvalidCertificates
                            ? new SslStream(client.NetworkStream, false, new RemoteCertificateValidationCallback(AcceptCertificate))
                            : new SslStream(client.NetworkStream, false);

                        bool success = await StartTls(client).ConfigureAwait(false);
                        if (!success)
                        {
                            client.Dispose();
                            continue;
                        }
                    }

                    _Clients.TryAdd(clientIp, client);
                    _ClientsLastSeen.TryAdd(clientIp, DateTime.Now);
                    Logger?.Invoke(_Header + "starting data receiver for: " + clientIp);
                    _Events.HandleClientConnected(this, new ClientConnectedEventArgs(clientIp));

                    if (_Keepalive.EnableTcpKeepAlives) EnableKeepalives(tcpClient);

                    CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(client.Token, _Token);
                    Task unawaited = Task.Run(() => DataReceiver(client), linkedCts.Token);
                }
                catch (TaskCanceledException)
                {
                    IsListening = false;
                    if (client != null) client.Dispose();
                    return;
                }
                catch (OperationCanceledException)
                {
                    IsListening = false;
                    if (client != null) client.Dispose();
                    return;
                }
                catch (ObjectDisposedException)
                {
                    if (client != null) client.Dispose();
                    continue;
                }
                catch (Exception e)
                {
                    if (client != null) client.Dispose();
                    Logger?.Invoke(_Header + "exception while awaiting connections: " + e.ToString());
                    continue;
                }
            }

            IsListening = false;
        }

        private async Task<bool> StartTls(ClientMetadata client)
        {
            try
            {
                await client.SslStream.AuthenticateAsServerAsync(
                    _SslCertificate,
                    _Settings.MutuallyAuthenticate,
                    SslProtocols.Tls12,
                    !_Settings.AcceptInvalidCertificates).ConfigureAwait(false);

                if (!client.SslStream.IsEncrypted)
                {
                    Logger?.Invoke(_Header + "client " + client.IpPort + " not encrypted, disconnecting");
                    client.Dispose();
                    return false;
                }

                if (!client.SslStream.IsAuthenticated)
                {
                    Logger?.Invoke(_Header + "client " + client.IpPort + " not SSL/TLS authenticated, disconnecting");
                    client.Dispose();
                    return false;
                }

                if (_Settings.MutuallyAuthenticate && !client.SslStream.IsMutuallyAuthenticated)
                {
                    Logger?.Invoke(_Header + "client " + client.IpPort + " failed mutual authentication, disconnecting");
                    client.Dispose();
                    return false;
                }
            }
            catch (Exception e)
            {
                Logger?.Invoke(_Header + "client " + client.IpPort + " SSL/TLS exception: " + Environment.NewLine + e.ToString());
                client.Dispose();
                return false;
            }

            return true;
        }

        private bool AcceptCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // return true; // Allow untrusted certificates.
            return _Settings.AcceptInvalidCertificates;
        }

        private async Task DataReceiver(ClientMetadata client)
        {
            Logger?.Invoke(_Header + "data receiver started for client " + client.IpPort);

            CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_Token, client.Token);

            while (true)
            {
                try
                {
                    if (!IsClientConnected(client.Client))
                    {
                        Logger?.Invoke(_Header + "client " + client.IpPort + " disconnected");
                        break;
                    }

                    if (client.Token.IsCancellationRequested)
                    {
                        Logger?.Invoke(_Header + "cancellation requested (data receiver for client " + client.IpPort + ")");
                        break;
                    }

                    byte[] data = await Common.DataReadAsync(client.Stream, _Settings.StreamBufferSize, linkedCts.Token).ConfigureAwait(false);
                    if (data == null)
                    {
                        await Task.Delay(10).ConfigureAwait(false);
                        continue;
                    }

                    Task unawaited = Task.Run(() => _Events.HandleDataReceived(this, new DataReceivedEventArgs(client.IpPort, data)), linkedCts.Token);
                    Statistics.ReceivedBytes += data.Length;
                    UpdateClientLastSeen(client.IpPort);
                }
                catch (IOException)
                {
                    Logger?.Invoke(_Header + "data receiver canceled, peer disconnected [" + client.IpPort + "]");
                }
                catch (SocketException)
                {
                    Logger?.Invoke(_Header + "data receiver canceled, peer disconnected [" + client.IpPort + "]");
                }
                catch (TaskCanceledException)
                {
                    Logger?.Invoke(_Header + "data receiver task canceled [" + client.IpPort + "]");
                }
                catch (ObjectDisposedException)
                {
                    Logger?.Invoke(_Header + "data receiver canceled due to disposal [" + client.IpPort + "]");
                }
                catch (Exception e)
                {
                    Logger?.Invoke(_Header + "data receiver exception [" + client.IpPort + "]:" +
                        Environment.NewLine +
                        e.ToString() +
                        Environment.NewLine);

                    break;
                }
            }

            Logger?.Invoke(_Header + "data receiver terminated for client " + client.IpPort);

            if (_ClientsKicked.ContainsKey(client.IpPort))
            {
                _Events.HandleClientDisconnected(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Kicked));
            }
            else if (_ClientsTimedout.ContainsKey(client.IpPort))
            {
                _Events.HandleClientDisconnected(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Timeout));
            }
            else
            {
                _Events.HandleClientDisconnected(this, new ClientDisconnectedEventArgs(client.IpPort, DisconnectReason.Normal));
            }

            _Clients.TryRemove(client.IpPort, out ClientMetadata destroyed);
            _ClientsLastSeen.TryRemove(client.IpPort, out DateTime removedTs);
            _ClientsKicked.TryRemove(client.IpPort, out removedTs);
            _ClientsTimedout.TryRemove(client.IpPort, out removedTs);
            client.Dispose();
        }

        private async Task IdleClientMonitor()
        {
            while (!_Token.IsCancellationRequested)
            {
                await Task.Delay(_Settings.IdleClientEvaluationIntervalMs, _Token).ConfigureAwait(false);

                if (_Settings.IdleClientTimeoutMs == 0) continue;

                try
                {
                    DateTime idleTimestamp = DateTime.Now.AddMilliseconds(-1 * _Settings.IdleClientTimeoutMs);

                    foreach (KeyValuePair<string, DateTime> curr in _ClientsLastSeen)
                    {
                        if (curr.Value < idleTimestamp)
                        {
                            _ClientsTimedout.TryAdd(curr.Key, DateTime.Now);
                            Logger?.Invoke(_Header + "disconnecting " + curr.Key + " due to timeout");
                            DisconnectClient(curr.Key);
                        }
                    }
                }
                catch (Exception e)
                {
                    Logger?.Invoke(_Header + "monitor exception: " + e.ToString());
                }
            }
        }

        private void UpdateClientLastSeen(string ipPort)
        {
            if (_ClientsLastSeen.ContainsKey(ipPort))
            {
                _ClientsLastSeen.TryRemove(ipPort, out _);
            }

            _ClientsLastSeen.TryAdd(ipPort, DateTime.Now);
        }

        private void SendInternal(string ipPort, long contentLength, Stream stream)
        {
            if (!_Clients.TryGetValue(ipPort, out ClientMetadata client)) return;
            if (client == null) return;

            Common.SendInternal(client.SendLock, client.Stream, _Settings.StreamBufferSize, stream, contentLength, Statistics);
        }

        private async Task SendInternalAsync(string ipPort, long contentLength, Stream stream, CancellationToken token)
        {
            if (_Clients.TryGetValue(ipPort, out ClientMetadata client) && client != null)
                await Common.SendInternalAsync(client.SendLock, client.Stream, _Settings.StreamBufferSize, stream, contentLength, Statistics, token)
                    .ConfigureAwait(false);
        }

        private void EnableKeepalives()
        {
            try
            {
#if NETCOREAPP || NET5_0

                _Listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _Keepalive.TcpKeepAliveTime);
                _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _Keepalive.TcpKeepAliveInterval);
                _Listener.Server.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _Keepalive.TcpKeepAliveRetryCount);

#elif NETFRAMEWORK

            byte[] keepAlive = new byte[12];

            // Turn keepalive on
            Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

            // Set TCP keepalive time
            Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveTime), 0, keepAlive, 4, 4); 

            // Set TCP keepalive interval
            Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveInterval), 0, keepAlive, 8, 4); 

            // Set keepalive settings on the underlying Socket
            _Listener.Server.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            {
                Logger?.Invoke(_Header + "keepalives not supported on this platform, disabled");
            }
        }

        private void EnableKeepalives(TcpClient client)
        {
            try
            {
#if NETCOREAPP || NET5_0

                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _Keepalive.TcpKeepAliveTime);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _Keepalive.TcpKeepAliveInterval);
                client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, _Keepalive.TcpKeepAliveRetryCount);

#elif NETFRAMEWORK

                byte[] keepAlive = new byte[12];

                // Turn keepalive on
                Buffer.BlockCopy(BitConverter.GetBytes((uint)1), 0, keepAlive, 0, 4);

                // Set TCP keepalive time
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveTime), 0, keepAlive, 4, 4); 

                // Set TCP keepalive interval
                Buffer.BlockCopy(BitConverter.GetBytes((uint)_Keepalive.TcpKeepAliveInterval), 0, keepAlive, 8, 4); 

                // Set keepalive settings on the underlying Socket
                client.Client.IOControl(IOControlCode.KeepAliveValues, keepAlive, null);

#elif NETSTANDARD

#endif
            }
            catch (Exception)
            {
                Logger?.Invoke(_Header + "keepalives not supported on this platform, disabled");
                _Keepalive.EnableTcpKeepAlives = false;
            }
        }

        #endregion
    }
}
