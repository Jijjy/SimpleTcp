using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleTcp
{
    /// <summary>
    /// SimpleTcp statistics.
    /// </summary>
    public class SimpleTcpStatistics
    {
        #region Public-Members

        /// <summary>
        /// The time at which the client or server was started.
        /// </summary>
        public DateTime StartTime { get; } = DateTime.Now.ToUniversalTime();

        /// <summary>
        /// The amount of time which the client or server has been up.
        /// </summary>
        public TimeSpan UpTime => DateTime.Now.ToUniversalTime() - StartTime;

        /// <summary>
        /// The number of bytes received.
        /// </summary>
        public long ReceivedBytes { get; internal set; } = 0;

        /// <summary>
        /// The number of bytes sent.
        /// </summary>
        public long SentBytes
        {
            get => _SentBytes;
            internal set => _SentBytes = value;
        }

        #endregion

        #region Private-Members

        private long _SentBytes = 0; 

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initialize the statistics object.
        /// </summary>
        public SimpleTcpStatistics()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Return human-readable version of the object.
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string ret =
                "--- Statistics ---" + Environment.NewLine +
                "    Started        : " + StartTime.ToString() + Environment.NewLine +
                "    Uptime         : " + UpTime.ToString() + Environment.NewLine +
                "    Received bytes : " + ReceivedBytes + Environment.NewLine +
                "    Sent bytes     : " + SentBytes + Environment.NewLine;
            return ret;
        }

        /// <summary>
        /// Reset statistics other than StartTime and UpTime.
        /// </summary>
        public void Reset()
        {
            ReceivedBytes = 0; 
            _SentBytes = 0; 
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
