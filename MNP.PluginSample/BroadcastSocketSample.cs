using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(IBroadcastSocket))]
    public sealed class BroadcastSocketSample : IBroadcastSocket
    {
        public System.Net.IPAddress BroadcastSourceAddress
        {
            get { throw new NotImplementedException(); }
        }

        public void SendBroadcastMessage(BroadcastMessageType msgType, ISerialiser<Core.Messages.AutoDiscoveryMessage, byte[]> serialiser, int multicastPort)
        {
            throw new NotImplementedException();
        }

        public bool UseBroadcasting
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public void Start()
        {
            throw new NotImplementedException();
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        public bool AllowAddressReuse
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public int Port
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public System.Net.IPAddress BindingAddress
        {
            get
            {
                throw new NotImplementedException();
            }
            set
            {
                throw new NotImplementedException();
            }
        }

        public bool SendTo(string address, byte[] data)
        {
            throw new NotImplementedException();
        }

        public void ConnectTo(System.Net.IPEndPoint ipe)
        {
            throw new NotImplementedException();
        }

        public List<System.Net.IPAddress> GetKnownNodes()
        {
            throw new NotImplementedException();
        }

        public event EventHandler<SocketDataEventArgs> OnDataReceived;

        public event EventHandler<System.Net.IPEndPoint> OnClientConnectCompleted;
    }
}
