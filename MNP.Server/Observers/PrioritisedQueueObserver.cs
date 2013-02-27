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

            this.ParentNode = parent;

            this.LogProvider = (logger == null) ? new DefaultLogProvider(LogLevel.Verbose) : logger;
        }

        public void OnCompleted()
        {
            this.LogProvider.Log("Prioritised Queue Observer Completed", "PrioritisedQueueObserver", LogLevel.Verbose);
            this.ParentNode = null;
            this.LogProvider = null;
        }

        public void OnError(Exception error)
        {
            this.LogProvider.Log(error.Message, "PrioritisedQueueObserver", LogLevel.Verbose);
        }

        public void OnNext(ClientProcess value)
        {
            Task.Run(() =>
            {
                // Get the list of nodes
                List<IPAddress> nodes = this.ParentNode.KnownNodes;

                // if there are no nodes, there is no point serialising the message
                if (nodes != null)
                {
                    // setup the message before sending
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage { Data = this.ParentNode.ClientProcessSerialiser.Serialise(value), IsLocalOnly = true, MessageType = InterNodeMessageType.AddToQueue };

                    byte[] data = this.ParentNode.InterNodeCommunicationMessageSerialiser.Serialise(msg);

                    // need to send this to all the other known nodes
                    foreach (var node in nodes)
                    {
                        this.ParentNode.SendToNode(node, data);
                    }

                    // be a good boy and clean up after ourselves
                    nodes.Clear();
                }
            });
        }
    }
}
