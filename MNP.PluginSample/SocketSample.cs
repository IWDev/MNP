using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using MNP.Core;

namespace MNP.PluginSample
{
    [Export(typeof(ISocket))]
    public sealed class SocketSample : ISocket
    {
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
