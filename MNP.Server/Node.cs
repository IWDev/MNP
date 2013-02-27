using MNP.Core;
using MNP.Core.Classes;
using MNP.Core.Enums;
using MNP.Core.Messages;
using MNP.Core.Serialisers;
using MNP.Server.Observers;
using MNP.Server.Providers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading.Tasks;

namespace MNP.Server
{
    public sealed class Node
    {
        #region "Properties"
        /// <summary>
        /// The port that clients will connect to.
        /// </summary>
        private Int32 ClientPort { get; set; }
        /// <summary>
        /// The port that other nodes will connect to.
        /// </summary>
        private Int32 ServerPort { get; set; }
        /// <summary>
        /// The port used for auto discovery
        /// </summary>
        private Int32 AutoDiscoveryPort { get; set; }
        /// <summary>
        /// The local address that the socket used to talk to clients will bind on.
        /// </summary>
        private String ClientBindingAddress { get; set; }
        /// <summary>
        /// The local address that the socket used to talk to other nodes will bind on.
        /// </summary>
        private String ServerBindingAddress { get; set; }
        /// <summary>
        /// Determines whether to use AutoDiscovery functionality or not. Must be set before Start() is called.
        /// </summary>
        public bool UseAutoDiscovery { get; set; }
        /// <summary>
        /// Determines whether or not this node instance is a master node
        /// </summary>
        public bool IsMasterNode { get; private set; }

        private ILogProvider LogProvider { get; set; }
        internal ISerialiser<AutoDiscoveryMessage, byte[]> AutoDiscoveryMessageSerialiser { get; private set; }
        internal ISerialiser<ClientMessage, byte[]> ClientMessageSerialiser { get; private set; }
        internal ISerialiser<ClientProcess, byte[]> ClientProcessSerialiser { get; private set; }
        internal ISerialiser<ClientResultMessage, byte[]> ClientResultMessageSerialiser { get; private set; }
        internal ISerialiser<InterNodeCommunicationMessage, byte[]> InterNodeCommunicationMessageSerialiser { get; private set; }
        internal ISerialiser<CacheProvider<string, ClientResultMessage>, byte[]> CacheSerialiser { get; set; }
        internal ISerialiser<ObservablePriorityQueue<ClientProcess>, byte[]> PrioritisedQueueSerialiser { get; set; }
        internal List<IPAddress> KnownNodes
        {
            get
            {
                if (this.UseAutoDiscovery)
                {
                    return _autoDiscoveryList;
                }
                else
                {
                    return _internodeListeningSocket.GetKnownNodes();
                }
            }
        }
        #endregion

        #region "Private Members"
        private List<IDisposable> _disposableResources = new List<IDisposable>();
        private List<IPAddress> _autoDiscoveryList = new List<IPAddress>();
        private ISocket _internodeListeningSocket;
        private ISocket _internodeConnectionSocket;
        private ISocket _clientCommunicationsSocket;
        private IBroadcastSocket _autoDiscoverySocket;
        private PluginManager _pluginManager;
        #endregion

        #region "Message Collections"
        // not needed
        public readonly ObservableCollection<byte[]> DataReceived = new ObservableCollection<byte[]>();
        // end of not needed

        /// <summary>
        /// Stores all of the results from the cache entry
        /// </summary>
        private CacheProvider<String, ClientResultMessage> ResultCache { get; set; }

        private ObservablePriorityQueue<ClientProcess> ProcessQueue { get; set; }
        #endregion

        #region "Constructors"
        private Node() { }

        public Node(Int32 clientPort, Int32 nodePort) : this(clientPort, nodePort, "", "", "", "", null, "") { }
        public Node(Int32 clientPort, Int32 nodePort, String clientBindingAddress, String nodeBindingAddress) : this(clientPort, nodePort, clientBindingAddress, nodeBindingAddress, "", "", null, "") { }
        public Node(Int32 clientPort, Int32 nodePort, String clientBindingAddress, String nodeBindingAddress, String clientSocketType, String nodeSocketType) : this(clientPort, nodePort, clientBindingAddress, nodeBindingAddress, "", "", null, "") { }
        public Node(Int32 clientPort, Int32 nodePort, String clientBindingAddress, String nodeBindingAddress, String clientSocketType, String nodeSocketType, ILogProvider logger, String configurationPath)
        {
            if (String.IsNullOrEmpty(configurationPath))
            {
                InitWithDefaults(clientPort, nodePort, clientBindingAddress, nodeBindingAddress, logger);
            }
            else
            {
                if (!LoadConfiguration(configurationPath))
                {
                    InitWithDefaults(clientPort, nodePort, clientBindingAddress, nodeBindingAddress, logger);
                }
            }
        }

        private void InitWithDefaults(Int32 clientPort, Int32 nodePort, String clientBindingAddress, String nodeBindingAddress, ILogProvider logger)
        {
            this.ClientPort = (clientPort == 0) ? 270 : clientPort;
            this.ServerPort = (nodePort == 0) ? 280 : nodePort;
            this.ClientBindingAddress = String.IsNullOrEmpty(clientBindingAddress) ? "0.0.0.0" : clientBindingAddress;
            this.ServerBindingAddress = String.IsNullOrEmpty(nodeBindingAddress) ? "0.0.0.0" : nodeBindingAddress;
            this.LogProvider = (logger == null) ? new DefaultLogProvider(LogLevel.None) : logger;

            _clientCommunicationsSocket = new TCPSocket(this.ClientBindingAddress, this.ClientPort);
            _internodeListeningSocket = new TCPSocket(this.ServerBindingAddress, this.ServerPort);
            _internodeConnectionSocket = new TCPSocket(285);

            AutoDiscoveryMessageSerialiser = new DefaultSerialiser<AutoDiscoveryMessage>();
            CacheSerialiser = new DefaultSerialiser<CacheProvider<string, ClientResultMessage>>();
            ClientMessageSerialiser = new DefaultSerialiser<ClientMessage>();
            ClientProcessSerialiser = new DefaultSerialiser<ClientProcess>();
            ClientResultMessageSerialiser = new DefaultSerialiser<ClientResultMessage>();
            InterNodeCommunicationMessageSerialiser = new DefaultSerialiser<InterNodeCommunicationMessage>();
            PrioritisedQueueSerialiser = new DefaultSerialiser<ObservablePriorityQueue<ClientProcess>>();
        }
        #endregion

        #region "Start/Stop"
        public void Start()
        {
            // if not set, load the defaults
            if (this.ResultCache == null)
            {
                this.ResultCache = new DefaultCache<String, ClientResultMessage>();
            }

            if (this.ProcessQueue == null)
            {
                this.ProcessQueue = new ObservablePriorityQueue<ClientProcess>();
            }

            // Setup the auto discovery socket (if applicable)
            if (this.UseAutoDiscovery)
            {
                if (this.AutoDiscoveryPort == 0)
                {
                    this.AutoDiscoveryPort = 275;
                }

                _autoDiscoverySocket = new UDPSocket("192.168.63.1", this.AutoDiscoveryPort);
                _autoDiscoverySocket.AllowAddressReuse = true;
                _autoDiscoverySocket.UseBroadcasting = true;
            }

            // hook up the events ready
            _disposableResources.Add(this.ResultCache.Subscribe(new ResultCacheObserver(this, this.LogProvider)));
            _disposableResources.Add(this.ProcessQueue.Subscribe(new PrioritisedQueueObserver(this, this.LogProvider)));
            _internodeListeningSocket.OnDataReceived += _internodeCommunicationsSocket_OnDataReceived;
            _internodeListeningSocket.OnClientConnectCompleted += _internodeConnectionSocket_OnClientConnectCompleted;
            _internodeConnectionSocket.OnDataReceived += _internodeCommunicationsSocket_OnDataReceived;
            _internodeConnectionSocket.OnClientConnectCompleted += _internodeConnectionSocket_OnClientConnectCompleted;
            _clientCommunicationsSocket.OnDataReceived += _clientCommunicationsSocket_OnDataReceived;

            _pluginManager = new PluginManager();
            _internodeListeningSocket.Start();
            _clientCommunicationsSocket.Start();

            if (this.UseAutoDiscovery)
            {
                _autoDiscoverySocket.OnDataReceived += _autoDiscoverySocket_OnDataReceived;
                _autoDiscoverySocket.Start();
                _autoDiscoverySocket.SendBroadcastMessage(BroadcastMessageType.Startup, this.AutoDiscoveryMessageSerialiser, this.AutoDiscoveryPort);
            }
        }

        public void Stop()
        {
            _internodeListeningSocket.Stop();
            _clientCommunicationsSocket.Stop();

            if (this.UseAutoDiscovery)
            {
                _autoDiscoverySocket.SendBroadcastMessage(BroadcastMessageType.Shutdown, this.AutoDiscoveryMessageSerialiser, this.AutoDiscoveryPort);
                _autoDiscoverySocket.Stop();
            }
        }
        #endregion

        #region "Data handlers"
        #region "Socket Handlers"
        private void _clientCommunicationsSocket_OnDataReceived(object sender, SocketDataEventArgs e)
        {
            ClientMessage msg = ClientMessageSerialiser.Deserialise(e.Data);

            switch (msg.MessageType)
            {
                case ClientMessageType.FetchResult:
                    GetResultFromCache(msg.Tag, msg.Data);
                    break;
                case ClientMessageType.NewTask:
                    StartNewTask(msg.Data);
                    break;
                case ClientMessageType.TimeoutPrevention:
                    // we are preventing a timeout from taking place here, no action is neccessary
                    break;
                default:
                    Console.WriteLine("Client message received. Default.");
                    break;
            }
        }

        private void GetResultFromCache(string p1, byte[] p2)
        {
            throw new NotImplementedException();
        }

        private void StartNewTask(byte[] p)
        {
            throw new NotImplementedException();
        }

        private void _internodeCommunicationsSocket_OnDataReceived(object sender, SocketDataEventArgs e)
        {
            InterNodeCommunicationMessage msg = InterNodeCommunicationMessageSerialiser.Deserialise(e.Data);
            Console.WriteLine("INCM :: " + msg.MessageType.ToString());
            switch (msg.MessageType)
            {
                case InterNodeMessageType.AddToCache:
                    // typeof ClientResultMessage
                    AddToCache(msg.IsLocalOnly, msg.Tag, msg.Data);
                    break;
                case InterNodeMessageType.AddToQueue:
                    // typeof ClientMessage
                    AddToQueue(msg.IsLocalOnly, msg.Data);
                    break;
                case InterNodeMessageType.RemoveFromCache:
                    RemoveFromCache(msg.IsLocalOnly, msg.Tag);
                    break;
                case InterNodeMessageType.RemoveFromQueue:
                    RemoveFromQueue(msg.IsLocalOnly, msg.Tag);
                    break;
                case InterNodeMessageType.NewNodeDiscovered:
                    NewNodeDiscoveredOnNetwork(e.Source);
                    break;
                case InterNodeMessageType.ChangeMessageStateInQueue:
                    // typeof QueuedProcessState
                    QueueMessageStateChanged(msg.Tag, msg.Data);
                    break;
                case InterNodeMessageType.FullCacheUpdateSent:
                    SendFullCacheUpdate(e.Source);
                    break;
                case InterNodeMessageType.FullQueueUpdateSent:
                    SendFullQueueUpdate(e.Source);
                    break;
                case InterNodeMessageType.FullCacheUpdateReceived:
                    UpdateCacheWithFullUpdate(msg.Data);
                    break;
                case InterNodeMessageType.FullQueueUpdateReceived:
                    UpdateQueueWithFullUpdate(msg.Data);
                    break;
                case InterNodeMessageType.StartUp:
                    GetDataFromNode(e.Source, InterNodeMessageType.FullCacheUpdateSent);
                    GetDataFromNode(e.Source, InterNodeMessageType.FullQueueUpdateSent);
                    break;
                default:
                    Console.WriteLine("Internode message received. Default.");
                    break;
            }

        }

        private void GetDataFromNode(IPAddress ip, InterNodeMessageType msgType)
        {
            if (ip == null)
            {
                throw new ArgumentException("ip cannot be null");
            }

            if (msgType == InterNodeMessageType.FullQueueUpdateSent || msgType == InterNodeMessageType.FullCacheUpdateSent)
            {
                InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { MessageType = msgType, IsLocalOnly = true };
                SendToNode(ip, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
            }
        }

        private void _internodeConnectionSocket_OnClientConnectCompleted(object sender, IPEndPoint e)
        {
            if (e != null)
            {
                InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { IsLocalOnly = true, MessageType = InterNodeMessageType.StartUp, Tag = e.Address.ToString() };
                SendToNode(e.Address, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
            }
        }

        private void _autoDiscoverySocket_OnDataReceived(object sender, SocketDataEventArgs e)
        {
            // check that we actually want this message type on this interface
            if (e.Type == PacketType.BroadcastUDP)
            {
                // TODO :: add logging
                // deserialise the data
                AutoDiscoveryMessage msg = this.AutoDiscoveryMessageSerialiser.Deserialise(e.Data);
                if (_autoDiscoverySocket.BroadcastSourceAddress.Equals(msg.IP.Address))
                {
                    return;
                }
                switch (msg.MessageType)
                {
                    case BroadcastMessageType.Startup:
                        Console.WriteLine("Autodiscovery: Client start up -> {0}", msg.IP.Address.ToString());
                        _autoDiscoveryList.Add(msg.IP.Address);
                        NodeStartup(new IPEndPoint(msg.IP.Address, this.ServerPort));
                        break;
                    case BroadcastMessageType.Shutdown:
                        Console.WriteLine("Autodiscovery: Client shutdown -> {0}", msg.IP.Address.ToString());
                        _autoDiscoveryList.Remove(msg.IP.Address);
                        break;
                }
            }
        }
        #endregion

        #region "Cache"
        private void SendFullCacheUpdate(IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentException("The specified IP address is not valid", "ip");
            }

            if (!this.KnownNodes.Contains(ip))
            {
                throw new ArgumentException("The ip address specified is not known.", "ip");
            }

            // grab a copy of the cache
            CacheProvider<string, ClientResultMessage> temp = this.ResultCache;

            InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Data = CacheSerialiser.Serialise(temp), IsLocalOnly = true, MessageType = InterNodeMessageType.FullCacheUpdateReceived };

            this.SendToNode(ip, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
        }

        private void UpdateCacheWithFullUpdate(byte[] data)
        {

            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The specified data was not valid", "data");
            }

            // not sure if this is valid or not
            lock (this.ResultCache)
            {
                this.ResultCache = this.CacheSerialiser.Deserialise(data);
            }

        }

        private void RemoveFromCache(bool localOnly, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The key cannot be null or empty", "id");
            }

            this.ResultCache.Remove(id, localOnly);

            if (!localOnly)
            {
                Task.Run(() =>
                {
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Tag = id, IsLocalOnly = true, MessageType = InterNodeMessageType.RemoveFromCache };
                    // not sure this is valid as its non deterministic
                    lock (this.KnownNodes)
                    {
                        foreach (var el in this.KnownNodes)
                        {
                            SendToNode(el, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
                        }
                    }
                });
            }
        }

        private void AddToCache(bool localOnly, string key, byte[] value)
        {
            // Sanity check everything first.
            if (String.IsNullOrEmpty(key))
            {
                throw new ArgumentException("The key cannot be null", "key");
            }

            if (value == null || value.Length == 0)
            {
                throw new ArgumentException("The entry to the cache cannot be null", "value");
            }

            // Update 
            ResultCache.Write(key, ClientResultMessageSerialiser.Deserialise(value), localOnly);
        }
        #endregion

        #region "Queue"
        private void SendFullQueueUpdate(IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentException("The specified ip is not valid", "ip");
            }

            ObservablePriorityQueue<ClientProcess> temp = this.ProcessQueue;

            InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Data = this.PrioritisedQueueSerialiser.Serialise(temp), IsLocalOnly = true, MessageType = InterNodeMessageType.FullQueueUpdateReceived };

            SendToNode(ip, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
        }

        private void UpdateQueueWithFullUpdate(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The specified data cannot be null or empty", "data");
            }

            lock (this.ProcessQueue)
            {
                this.ProcessQueue = this.PrioritisedQueueSerialiser.Deserialise(data);
            }
        }

        private void QueueMessageStateChanged(string id, byte[] dataState)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The specified id is not valid", "id");
            }

            if (dataState == null || dataState.Length == 0)
            {
                throw new ArgumentException("The specified data state is not valid", "dataState");
            }

            this.ProcessQueue.ChangeState(id, (QueuedProcessState)BitConverter.ToInt32(dataState, 0));
        }

        private void RemoveFromQueue(bool localOnly, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The specified entry is not valid", "id");
            }

            this.ProcessQueue.Remove(x => x.Tag == id);

            if (!localOnly)
            {
                Task.Run(() =>
                {
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Tag = id, IsLocalOnly = true, MessageType = InterNodeMessageType.RemoveFromQueue };
                    // not sure this is valid as its non deterministic
                    lock (this.KnownNodes)
                    {
                        foreach (var el in this.KnownNodes)
                        {
                            SendToNode(el, this.InterNodeCommunicationMessageSerialiser.Serialise(msg));
                        }
                    }
                });
            }
        }

        private void AddToQueue(bool localOnly, byte[] value)
        {
            // Sanity check everything first.
            if (value == null || value.Length == 0)
            {
                throw new ArgumentException("The entry to the cache cannot be null", "value");
            }

            ProcessQueue.Enqueue(ClientProcessSerialiser.Deserialise(value), !localOnly);
        }
        #endregion

        private void NewNodeDiscoveredOnNetwork(IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentException("The given ip address is invalid", "ip");
            }

            _autoDiscoveryList.Add(ip);

            NodeStartup(new IPEndPoint(ip, this.ServerPort));
        }

        private void NodeStartup(IPEndPoint ipe)
        {
            _internodeConnectionSocket.ConnectTo(ipe);
        }

        internal void SendToNode(IPAddress nodeAddress, byte[] data)
        {
            // prepend the length as we send the data
            byte[] _data = BitConverter.GetBytes(data.Length).Merge(data);

            // test to see if the listening socket has the connection open already
            if (!_internodeListeningSocket.SendTo(nodeAddress.ToString(), _data))
            {
                // test to see if the connection socket has the connection open already
                if (!_internodeConnectionSocket.SendTo(nodeAddress.ToString(), _data))
                {
                    // Should we open a new connection to the specified address? Just throw a exception now
                    //throw new Exception("The requested node is not known by the existing sockets.");
                }
            }
        }

        #endregion

        #region "Configuration"
        private bool LoadConfiguration(String pathToConfig = "")
        {
            return false;
        }

        /// <summary>
        /// Loads the socket that will be used to communicate with other nodes
        /// </summary>
        /// <param name="type"></param>
        private void LoadServerSocket(String type)
        { }

        /// <summary>
        /// Loads the socket that will be used for communication with the clients
        /// </summary>
        /// <param name="type"></param>
        private void LoadClientSocket(String type)
        { }
        #endregion
    }
}

