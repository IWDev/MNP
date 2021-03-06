﻿using MNP.Core;
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
    internal class ResultCacheObserver : IObserver<ClientResultMessage>
    {
        private Node ParentNode { get; set; }
        private ILogProvider LogProvider { get; set; }

        internal ResultCacheObserver(Node parent, ILogProvider logger)
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
            LogProvider.Log("Result Cache Observer Completed", "Result Cache Observer", LogLevel.Verbose);
            ParentNode = null;
            LogProvider = null;
        }

        public void OnError(Exception error)
        {
            LogProvider.Log(error.Message, "ResultCacheObserver", LogLevel.Verbose);
        }

        public void OnNext(ClientResultMessage value)
        {
            Task.Run(() =>
            {
                // Get the list of nodes
                List<IPAddress> nodes = ParentNode.KnownNodes;

                // if there are no nodes, there is no point serialising the message
                if (nodes != null)
                {
                    // setup the message before sending
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage { Data = ParentNode.ClientResultMessageSerialiser.Serialise(value), IsLocalOnly = true, MessageType = InterNodeMessageType.AddToCache };

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
        }
    }
}
