﻿using MLRat.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServerPlugin.InterfaceHandle;
using MLManagementServer.Handlers;
using SharedCode.Network;

namespace MLManagementServer
{
    public class ServerPlugin : IServerPlugin
    {
        public MLPluginInfomation PluginInfomation
        {
            get
            {
                MLPluginInfomation info = new MLPluginInfomation("MLManagement")
                {
                    Developer = "BahNahNah",
                    Description = "Core Plugin"
                };
                return info;
            }
        }

        public void OnClientConnect(IClient client)
        {
            PingHandler.StartPing(client);
        }

        public void OnClientDisconnect(IClient client)
        {
            PingHandler.Disconnect(client);
        }

        public void OnDataRetrieved(IClient client, object[] data)
        {
            NetworkCommand command = (NetworkCommand)data[0];

            if (command == NetworkCommand.Pong) PingHandler.EndPing(client);
        }

        public void OnPluginLoad(IServerUIHandler UIHost)
        {
            PingHandler.Column = UIHost.AddColumn("Ping", "-");
            MLRatContextEntry network = new MLRatContextEntry()
            {
                Text = "Network"
            };

            network.SubMenus = new MLRatContextEntry[]
            {
                new MLRatContextEntry(){Text = "Ping",OnClick = PingHandler.ContextCallback}
            };

            UIHost.AddContext(network);
        }
    }
}