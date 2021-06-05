using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleTcp
{
    internal class ClientMetadata : IDisposable
    {
        #region Public-Members

        internal TcpClient Client { get; private set; } = null;

        internal NetworkStream NetworkStream { get; private set; } = null;

        internal SslStream SslStream { get; set; } = null;

        internal Stream Stream => (Stream)SslStream ?? NetworkStream;

        internal string IpPort => _IpPort;

        internal SemaphoreSlim SendLock = new SemaphoreSlim(1, 1);
        internal SemaphoreSlim ReceiveLock = new SemaphoreSlim(1, 1);

        internal CancellationTokenSource TokenSource { get; set; }

        internal CancellationToken Token { get; set; }

        #endregion

        #region Private-Members

        private readonly string _IpPort = null;

        #endregion

        #region Constructors-and-Factories

        internal ClientMetadata(TcpClient tcp)
        {
            if (tcp == null) throw new ArgumentNullException(nameof(tcp));

            Client = tcp;
            NetworkStream = tcp.GetStream();
            _IpPort = tcp.Client.RemoteEndPoint.ToString();
            TokenSource = new CancellationTokenSource();
            Token = TokenSource.Token;
        }

        #endregion

        #region Public-Methods

        public void Dispose()
        {
            if (TokenSource != null)
            {
                if (!TokenSource.IsCancellationRequested)
                {
                    TokenSource.Cancel();
                    TokenSource.Dispose();
                }
            }

            SslStream?.Close();
            NetworkStream?.Close();
            Client?.Close();
            Client?.Dispose();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
