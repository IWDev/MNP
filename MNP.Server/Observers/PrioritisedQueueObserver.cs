using MNP.Core;
using MNP.Core.Classes;
using MNP.Core.Enums;
using MNP.Core.Messages;
using MNP.Server.Providers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MNP.Server.Observers
{
    [Serializable]
    public class PrioritisedQueueObserver : IObserver<ClientProcess>
    {
        private Node ParentNode { get; set; }
        private ILogProvider LogProvider { get; set; }

        private PrioritisedQueueObserver() { }

        internal PrioritisedQueueObserver(Node parent, ILogProvider logger)
        {
            if (parent == null)
            {
                throw new ArgumentException("Parent node cannot be null", "parent");
            }

            ParentNode = parent;

            LogProvider = logger ?? new DefaultLogProvider(LogLevel.Verbose);
        }

        public void OnCompleted()
        {
            LogProvider.Log("Prioritised Queue Observer Completed", "PrioritisedQueueObserver", LogLevel.Verbose);
            ParentNode = null;
            LogProvider = null;
        }

        public void OnError(Exception error)
        {
            LogProvider.Log(error.Message, "PrioritisedQueueObserver", LogLevel.Verbose);
        }

        public async void OnNext(ClientProcess value)
        {
            await Task.Run(() =>
            {
                // Get the list of nodes
                List<IPAddress> nodes = ParentNode.KnownNodes;

                // if there are no nodes, there is no point serialising the message
                if (nodes != null && !value.LocalOnly)
                {
                    // setup the message before sending
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage { Data = ParentNode.ClientProcessSerialiser.Serialise(value), IsLocalOnly = true, MessageType = InterNodeMessageType.AddToQueue };

                    byte[] data = ParentNode.InterNodeCommunicationMessageSerialiser.Serialise(msg);

                    // need to send this to all the other known nodes
                    foreach (var node in nodes)
                    {
                        ParentNode.SendToNode(node, data);
                    }

                    // be a good boy and clean up after ourselves
                    nodes.Clear(); 
                }
            });

            if (!value.LocalOnly)
            {
                // start the task
                ParentNode.ProcessQueue.ChangeState(value.Tag, QueuedProcessState.Running, false);
                Task<byte[]> task = ParentNode.NodeTask.Execute(value.Data);
                ParentNode.AddToCache(false, value.Tag, await task);
            }
        }
    }
}
