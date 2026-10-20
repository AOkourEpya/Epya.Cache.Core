using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Epya.Cache.Server
{
    internal class ClientContext
    {
        public TcpClient Client { get; }
        public ILogger Logger { get; }

        public ClientContext(TcpClient client, ILogger logger)
        {
            Client = client;
            Logger = logger;
        }
    }
}
