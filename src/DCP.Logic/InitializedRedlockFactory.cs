using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using System;
using System.Collections.Generic;
using System.Net;

namespace DCP.Logic
{
    public sealed class InitializedRedlockFactory
    {
        public RedLockFactory Factory { get; }
        private static readonly Lazy<InitializedRedlockFactory> _lazy = new Lazy<InitializedRedlockFactory>(() => new InitializedRedlockFactory());

        public static InitializedRedlockFactory Instance { get { return _lazy.Value; } }

        private InitializedRedlockFactory()
        {
            var localEndpoint = new DnsEndPoint("localhost", 6379);
            Factory = RedLockFactory.Create(new List<RedLockEndPoint> { localEndpoint });
        }
    }
}
