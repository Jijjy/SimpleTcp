using System;
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
    internal static class Common
    {
        internal static void ParseIpPort(string ipPort, out string ip, out int port)
        {
            if (string.IsNullOrEmpty(ipPort)) throw new ArgumentNullException(nameof(ipPort));

            ip = null;
            port = -1;

            int colonIndex = ipPort.LastIndexOf(':');
            if (colonIndex != -1)
            {
                ip = ipPort.Substring(0, colonIndex);
                port = Convert.ToInt32(ipPort.Substring(colonIndex + 1));
            }
        }

        public static async Task<byte[]> DataReadAsync(Stream stream, int bufferSize, CancellationToken token)
        {
            byte[] buffer = new byte[bufferSize];
            int read;

            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);

                    if (read <= 0)
                        throw new SocketException();

                    await ms.WriteAsync(buffer, 0, read, token).ConfigureAwait(false);
                    return ms.ToArray();
                }
            }
        }

        public static void SendInternal(SemaphoreSlim sendLock, Stream writeStream, int bufferSize, Stream readStream, long contentLength, SimpleTcpStatistics statistics)
        {
            long bytesRemaining = contentLength;
            byte[] buffer = new byte[bufferSize];

            try
            {
                sendLock.Wait();

                while (bytesRemaining > 0)
                {
                    int bytesRead = readStream.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        writeStream.Write(buffer, 0, bytesRead);

                        bytesRemaining -= bytesRead;
                        statistics.SentBytes += bytesRead;
                    }
                }

                writeStream.Flush();
            }
            finally
            {
                sendLock.Release();
            }
        }

        public static async Task SendInternalAsync(SemaphoreSlim sendLock, Stream writeStream, int bufferSize, Stream readStream, long contentLength, SimpleTcpStatistics statistics, CancellationToken token)
        {
            try
            {
                long bytesRemaining = contentLength;
                int bytesRead = 0;
                byte[] buffer = new byte[bufferSize];

                await sendLock.WaitAsync(token).ConfigureAwait(false);

                while (bytesRemaining > 0)
                {
                    bytesRead = await readStream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false);
                    if (bytesRead > 0)
                    {
                        await writeStream.WriteAsync(buffer, 0, bytesRead, token).ConfigureAwait(false);

                        bytesRemaining -= bytesRead;
                        statistics.SentBytes += bytesRead;
                    }
                }

                await writeStream.FlushAsync(token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {

            }
            catch (OperationCanceledException)
            {

            }
            finally
            {
                sendLock?.Release();
            }
        }

    }
}
