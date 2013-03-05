using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using MNP.Core;
using MNP.Core.Classes;
using MNP.Core.Enums;
using MNP.Core.Messages;
using MNP.Core.Serialisers;
using MNP.Server.Observers;
using MNP.Server.Providers;

namespace MNP.Server
{
    public sealed class Node
    {
        public Node(ILogProvider logProvider = null)
        {
            // Setup core properties
            LogProvider = logProvider ?? new DefaultLogProvider();
            DependancyInjector = Injector.Instance;
            DependancyInjector.LogProvider = LogProvider;
            
            // Setup the IoC container
            DependancyInjector.Bind<ILogProvider>(LogProvider);
            DependancyInjector.Bind<ISocket, TCPSocket>();
            DependancyInjector.Bind<IBroadcastSocket, UDPSocket>();
            DependancyInjector.Bind<CacheProvider<String, ClientResultMessage>, DefaultCache<String, ClientResultMessage>>();
            DependancyInjector.Bind<IObservableQueue<ClientProcess>, ObservablePriorityQueue<ClientProcess>>();
            DependancyInjector.Bind<ISerialiser<AutoDiscoveryMessage, byte[]>, DefaultSerialiser<AutoDiscoveryMessage>>();
            DependancyInjector.Bind<ISerialiser<ClientMessage, byte[]>, DefaultSerialiser<ClientMessage>>();
            DependancyInjector.Bind<ISerialiser<ClientProcess, byte[]>, DefaultSerialiser<ClientProcess>>();
            DependancyInjector.Bind<ISerialiser<ClientResultMessage, byte[]>, DefaultSerialiser<ClientResultMessage>>();
            DependancyInjector.Bind<ISerialiser<InterNodeCommunicationMessage, byte[]>, DefaultSerialiser<InterNodeCommunicationMessage>>();
            DependancyInjector.Bind<ISerialiser<IObservableQueue<ClientProcess>, byte[]>, DefaultSerialiser<IObservableQueue<ClientProcess>>>();
            DependancyInjector.Bind<IObservableQueue<ClientProcess>, ObservablePriorityQueue<ClientProcess>>();
        }

        #region "Private Members"
        private readonly List<IPAddress> _autoDiscoveryList = new List<IPAddress>();
        private ISocket _internodeListeningSocket;
        private ISocket _internodeConnectionSocket;
        private ISocket _clientCommunicationsSocket;
        private IBroadcastSocket _autoDiscoverySocket;
        private PluginManager _pluginManager;
        private ILogProvider LogProvider { get; set; }
        internal INodeTask NodeTask;
        #endregion

        #region "Properties"
        public String AutoDiscoveryBindingAddress { get; set; }
        public Int32 AutoDiscoveryPort { get; set; }
        public String ClientBindingAddress { get; set; }
        public Int32 ClientPort { get; set; }
        public Injector DependancyInjector { get; set; }
        public String InternodeBindingAddress { get; set; }
        public Int32 InternodePort { get; set; }
        public bool UseAutoDiscovery { get; set; }

        internal ISerialiser<AutoDiscoveryMessage, byte[]> AutoDiscoveryMessageSerialiser { get; private set; }
        internal ISerialiser<ClientMessage, byte[]> ClientMessageSerialiser { get; private set; }
        internal ISerialiser<ClientProcess, byte[]> ClientProcessSerialiser { get; private set; }
        internal ISerialiser<ClientResultMessage, byte[]> ClientResultMessageSerialiser { get; private set; }
        internal ISerialiser<InterNodeCommunicationMessage, byte[]> InterNodeCommunicationMessageSerialiser { get; private set; }
        internal ISerialiser<CacheProvider<string, ClientResultMessage>, byte[]> CacheSerialiser { get; set; }
        internal ISerialiser<IObservableQueue<ClientProcess>, byte[]> PrioritisedQueueSerialiser { get; set; }
        internal List<IPAddress> KnownNodes
        {
            get
            {
                return UseAutoDiscovery ? _autoDiscoveryList : _internodeListeningSocket.GetKnownNodes();
            }
        }
        #endregion

        #region "Message Collections"
        private CacheProvider<String, ClientResultMessage> ResultCache { get; set; }
        private IObservableQueue<ClientProcess> ProcessQueue { get; set; }
        #endregion

        #region "Start/Stop"
        public void Start()
        {
            // Resolve all the types
            _clientCommunicationsSocket = DependancyInjector.Resolve<ISocket>();
            _internodeConnectionSocket = DependancyInjector.Resolve<ISocket>();
            _internodeListeningSocket = DependancyInjector.Resolve<ISocket>();
            AutoDiscoveryMessageSerialiser = DependancyInjector.Resolve<ISerialiser<AutoDiscoveryMessage, byte[]>>();
            CacheSerialiser = DependancyInjector.Resolve<ISerialiser<CacheProvider<String, ClientResultMessage>, byte[]>>();
            ClientMessageSerialiser = DependancyInjector.Resolve<ISerialiser<ClientMessage, byte[]>>();
            ClientProcessSerialiser = DependancyInjector.Resolve<ISerialiser<ClientProcess, byte[]>>();
            ClientResultMessageSerialiser = DependancyInjector.Resolve<ISerialiser<ClientResultMessage, byte[]>>();
            InterNodeCommunicationMessageSerialiser = DependancyInjector.Resolve<ISerialiser<InterNodeCommunicationMessage, byte[]>>();
            PrioritisedQueueSerialiser = DependancyInjector.Resolve<ISerialiser<IObservableQueue<ClientProcess>, byte[]>>();
            ProcessQueue = DependancyInjector.Resolve<IObservableQueue<ClientProcess>>();
            ResultCache = DependancyInjector.Resolve<CacheProvider<String, ClientResultMessage>>();
            NodeTask = DependancyInjector.Resolve<INodeTask>();


            // Setup the auto discovery socket (if applicable)
            if (UseAutoDiscovery)
            {
                if (AutoDiscoveryPort <= 0)
                {
                    AutoDiscoveryPort = 275;
                }
                _autoDiscoverySocket = DependancyInjector.Resolve<IBroadcastSocket>();
                _autoDiscoverySocket.AllowAddressReuse = true;
                _autoDiscoverySocket.UseBroadcasting = true;
                _autoDiscoverySocket.Port = AutoDiscoveryPort;
                _autoDiscoverySocket.BindingAddress = IPAddress.Parse(AutoDiscoveryBindingAddress);
            }

            // hook up the events ready
            ResultCache.Subscribe(new ResultCacheObserver(this, LogProvider));
            ProcessQueue.Subscribe(new PrioritisedQueueObserver(this, LogProvider));
            _internodeConnectionSocket.OnClientConnectCompleted += internode_OnClientConnectCompleted;
            _internodeConnectionSocket.OnDataReceived += internode_OnDataReceived;
            _internodeListeningSocket.OnClientConnectCompleted += internode_OnClientConnectCompleted;
            _internodeListeningSocket.OnDataReceived += internode_OnDataReceived;
            _clientCommunicationsSocket.OnDataReceived += client_OnDataReceived;


            // setup the arguments ready
            if (ClientPort <= 0)
            {
                ClientPort = 270;
            }
            if (InternodePort <= 0)
            {
                InternodePort = 280;
            }

            if (String.IsNullOrEmpty(ClientBindingAddress))
            {
                ClientBindingAddress = "0.0.0.0";
            }
            if (String.IsNullOrEmpty(InternodeBindingAddress))
            {
                InternodeBindingAddress = "0.0.0.0";
            }

            _clientCommunicationsSocket.Port = ClientPort;
            _clientCommunicationsSocket.BindingAddress = IPAddress.Parse(ClientBindingAddress);

            _internodeConnectionSocket.Port = InternodePort;
            _internodeConnectionSocket.BindingAddress = IPAddress.Parse(InternodeBindingAddress);

            _internodeListeningSocket.Port = InternodePort;
            _internodeListeningSocket.BindingAddress = IPAddress.Parse(InternodeBindingAddress);

            //_pluginManager = new PluginManager();
            _internodeListeningSocket.Start();
            _clientCommunicationsSocket.Start();

            if (UseAutoDiscovery)
            {
                _autoDiscoverySocket.OnDataReceived += autodiscovery_OnDataReceived;
                _autoDiscoverySocket.Start();
                _autoDiscoverySocket.SendBroadcastMessage(BroadcastMessageType.Startup, AutoDiscoveryMessageSerialiser, AutoDiscoveryPort);
            }
        }

        public void Stop()
        {
            _internodeListeningSocket.Stop();
            _clientCommunicationsSocket.Stop();
            _autoDiscoverySocket.SendBroadcastMessage(BroadcastMessageType.Shutdown, AutoDiscoveryMessageSerialiser, AutoDiscoveryPort);
            _autoDiscoverySocket.Stop();
        }
        #endregion

        #region "Socket Data handlers"
        #region"ClientSocket"
        private void client_OnDataReceived(object sender, SocketDataEventArgs e)
        {
            ClientMessage msg = ClientMessageSerialiser.Deserialise(e.Data);

            switch (msg.MessageType)
            {
                case ClientMessageType.FetchResult:
                    GetResultFromCache(e.Source, msg.Tag);
                    break;
                case ClientMessageType.NewTask:
                    StartNewTask(e.Source, msg.Data, msg.Tag);
                    break;
                case ClientMessageType.TimeoutPrevention:
                    // we are preventing a timeout from taking place here, no action is neccessary
                    break;
                default:
                    Console.WriteLine("Client message received. Default.");
                    break;
            }
        }
        #endregion

        #region "InternodeSockets"
        private void internode_OnDataReceived(object sender, SocketDataEventArgs e)
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
                    GetDataFromNode(e.Source, InterNodeMessageType.FullQueueUpdateSent);
                    break;
                case InterNodeMessageType.FullQueueUpdateReceived:
                    UpdateQueueWithFullUpdate(msg.Data);
                    break;
                case InterNodeMessageType.StartUp:
                    GetDataFromNode(e.Source, InterNodeMessageType.FullCacheUpdateSent);
                    break;
                default:
                    Console.WriteLine("Internode message received. Default.");
                    break;
            }

        }
        private void internode_OnClientConnectCompleted(object sender, IPEndPoint e)
        {
            if (e != null)
            {
                Console.WriteLine("Client connected...");
                InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { IsLocalOnly = true, MessageType = InterNodeMessageType.StartUp, Tag = e.Address.ToString() };
                SendToNode(e.Address, InterNodeCommunicationMessageSerialiser.Serialise(msg));
            }
        }
        #endregion

        #region "AutoDiscoverySocket"
        private void autodiscovery_OnDataReceived(object sender, SocketDataEventArgs e)
        {
            // check that we actually want this message type on this interface
            if (e.Type == PacketType.BroadcastUDP)
            {
                // TODO :: add logging
                // deserialise the data
                AutoDiscoveryMessage msg = AutoDiscoveryMessageSerialiser.Deserialise(e.Data);
                if (_autoDiscoverySocket.BroadcastSourceAddress.Equals(msg.IP.Address))
                {
                    return;
                }
                switch (msg.MessageType)
                {
                    case BroadcastMessageType.Startup:
                        Console.WriteLine("Autodiscovery: Client start up -> {0}", msg.IP.Address);
                        _autoDiscoveryList.Add(msg.IP.Address);
                        NodeStartup(new IPEndPoint(msg.IP.Address, InternodePort));
                        break;
                    case BroadcastMessageType.Shutdown:
                        Console.WriteLine("Autodiscovery: Client shutdown -> {0}", msg.IP.Address);
                        _autoDiscoveryList.Remove(msg.IP.Address);
                        break;
                }
            }
        }
        #endregion
        #endregion

        #region "Methods"
        #region "Process"
        private void GetResultFromCache(IPAddress source, string tag)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (String.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException("tag");
            }

            if (ResultCache.Contains(tag))
            {
                SendToNode(source, ClientResultMessageSerialiser.Serialise(ResultCache[tag]));
            }
        }

        private void StartNewTask(IPAddress source, byte[] args, string tag)
        {
            // Check to see if the task exists
            if (NodeTask == null)
            {
                throw new Exception("There must be a valid task for this action to take place.");
            }

            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            if (args == null || args.Length == 0)
            {
                throw new ArgumentNullException("args");
            }

            if (String.IsNullOrEmpty(tag))
            {
                throw new ArgumentNullException("tag");
            }

            ProcessQueue.Enqueue(new ClientProcess()
            {
                State = QueuedProcessState.Runnable,
                Source = source,
                Data = args,
                Tag = tag
            }, true);
        }
        #endregion

        #region "Cache"
        private void SendFullCacheUpdate(IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentException("The specified IP address is not valid", "ip");
            }

            if (!KnownNodes.Contains(ip))
            {
                throw new ArgumentException("The ip address specified is not known.", "ip");
            }

            // grab a copy of the cache
            CacheProvider<string, ClientResultMessage> temp = ResultCache;
            temp.ClearSubscribersNoNotify();
            InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Data = CacheSerialiser.Serialise(temp), IsLocalOnly = true, MessageType = InterNodeMessageType.FullCacheUpdateReceived };

            SendToNode(ip, InterNodeCommunicationMessageSerialiser.Serialise(msg));
        }

        private void UpdateCacheWithFullUpdate(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The specified data was not valid", "data");
            }

            lock (ResultCache)
            {
                ResultCache = CacheSerialiser.Deserialise(data);
            }
        }

        private void RemoveFromCache(bool localOnly, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The key cannot be null or empty", "id");
            }

            ResultCache.Remove(id, localOnly);

            if (!localOnly)
            {
                Task.Run(() =>
                {
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Tag = id, IsLocalOnly = true, MessageType = InterNodeMessageType.RemoveFromCache };
                    // not sure this is valid as its non deterministic
                    lock (KnownNodes)
                    {
                        foreach (var el in KnownNodes)
                        {
                            SendToNode(el, InterNodeCommunicationMessageSerialiser.Serialise(msg));
                        }
                    }
                });
            }
        }

        internal void AddToCache(bool localOnly, string key, byte[] value)
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

            IObservableQueue<ClientProcess> temp = ProcessQueue;

            InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Data = PrioritisedQueueSerialiser.Serialise(temp), IsLocalOnly = true, MessageType = InterNodeMessageType.FullQueueUpdateReceived };

            SendToNode(ip, InterNodeCommunicationMessageSerialiser.Serialise(msg));
        }

        private void UpdateQueueWithFullUpdate(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("The specified data cannot be null or empty", "data");
            }

            lock (ProcessQueue)
            {
                ProcessQueue = PrioritisedQueueSerialiser.Deserialise(data);
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

            ProcessQueue.ChangeState(id, (QueuedProcessState)BitConverter.ToInt32(dataState, 0));
        }

        private void RemoveFromQueue(bool localOnly, string id)
        {
            if (String.IsNullOrEmpty(id))
            {
                throw new ArgumentException("The specified entry is not valid", "id");
            }

            ProcessQueue.Remove(x => x.Tag == id);

            if (!localOnly)
            {
                Task.Run(() =>
                {
                    InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { Tag = id, IsLocalOnly = true, MessageType = InterNodeMessageType.RemoveFromQueue };
                    // not sure this is valid as its non deterministic
                    lock (KnownNodes)
                    {
                        foreach (var el in KnownNodes)
                        {
                            SendToNode(el, InterNodeCommunicationMessageSerialiser.Serialise(msg));
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

        #region "Node"
        private void GetDataFromNode(IPAddress ip, InterNodeMessageType msgType)
        {
            if (ip == null)
            {
                throw new ArgumentException("ip cannot be null");
            }

            if (msgType == InterNodeMessageType.FullQueueUpdateSent || msgType == InterNodeMessageType.FullCacheUpdateSent)
            {
                InterNodeCommunicationMessage msg = new InterNodeCommunicationMessage() { MessageType = msgType, IsLocalOnly = true };
                SendToNode(ip, InterNodeCommunicationMessageSerialiser.Serialise(msg));
            }
        }

        private void NewNodeDiscoveredOnNetwork(IPAddress ip)
        {
            if (ip == null)
            {
                throw new ArgumentException("The given ip address is invalid", "ip");
            }

            _autoDiscoveryList.Add(ip);

            NodeStartup(new IPEndPoint(ip, InternodePort));
        }

        private void NodeStartup(IPEndPoint ipe)
        {
            _internodeConnectionSocket.ConnectTo(ipe);
        }

        internal bool SendToNode(IPAddress nodeAddress, byte[] data)
        {
            // prepend the length as we send the data
            byte[] _data = BitConverter.GetBytes(data.Length).Merge(data);
            if (_data == null) throw new ArgumentNullException("_data");

            // test to see if the listening socket has the connection open already
            if (!_internodeListeningSocket.SendTo(nodeAddress.ToString(), _data))
            {
                // test to see if the connection socket has the connection open already
                if (!_internodeConnectionSocket.SendTo(nodeAddress.ToString(), _data))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
        #endregion
    }
}