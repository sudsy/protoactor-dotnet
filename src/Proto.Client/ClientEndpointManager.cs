using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientEndpointManager>();
        
        private readonly string _clientId = Guid.NewGuid().ToString();
        private Channel _channel;
        private ClientRemoting.ClientRemotingClient _client;
        private ClientEndpointReader _clientEndpointReader;
        private AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;
        
        private int _endpointReferenceCount = 0;
        private string _hostName;
        private int _port;
        private RemoteConfig _config;
        private int _connectionTimeoutMs;
        
        private PID _endpointWriter;
        private bool _hostPIDSet;

        public ClientEndpointManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _hostName = hostname;
            _port = port;
            _config = config;
            _connectionTimeoutMs = connectionTimeoutMs;
        }
        
        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case String str:
                    switch (str)
                    {
                        case "getclienthostpid":
                            context.Respond(await _clientEndpointReader.GetClientHostPID());
                            break;
                        case "connectionfailure":
                            _logger.LogInformation("Connection Failed Retrying connection");
                           
//                            disposeConnection();
                            connectToServer(context.Self);
                            await connectReaderAndWriter(context);

                            break;
                    }
                    
                    break;
                case AcquireClientEndpointReference _:
                    if (_clientEndpointReader == null || _channel.State != ChannelState.Ready)
                    {
                        connectToServer(context.Self);

                        await connectReaderAndWriter(context);
                    }
                    
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
                    break;
                
                case ReleaseClientEndpointReference _:
                    _endpointReferenceCount--;
                    if (_endpointReferenceCount <= 0)
                    {
                        disposeConnection();
                    }

                    break;
                
                case RemoteDeliver rd:
                    if (_channel.State != ChannelState.Ready)
                    {
                        
                        _logger.LogInformation("Connection Failed Trying to send Retrying connection");
                           
                        connectToServer(context.Self);
                        await connectReaderAndWriter(context);
                    }
                    _logger.LogDebug($"Forwarding Remote Deliver Message to endpoint Writer");
                    context.Forward(_endpointWriter);
                    break;
                
            }

            return;
        }

        private async Task connectReaderAndWriter(IContext context)
        {
            var clientEndpointReader = new ClientEndpointReader(context.Self, _clientStreams.ResponseStream, _connectionTimeoutMs);
            if (!_hostPIDSet)
            {
                var hostPid = await clientEndpointReader.GetClientHostPID();
                ProcessRegistry.Instance.Address = "client://" + hostPid.Address + "/" + hostPid.Id;
                _hostPIDSet = true;
            }

            _clientEndpointReader = clientEndpointReader;
            _endpointWriter =
                RootContext.Empty.Spawn(Props.FromProducer(() =>
                    new ClientEndpointWriter(_clientStreams.RequestStream)));
            if (_hostPIDSet)
            {
                context.Spawn(Props.FromFunc(async ctx =>
                {
                    switch (ctx.Message)
                    {
                        case Started _:
                            try
                            {
                                var hostPid = await _clientEndpointReader.GetClientHostPID();
                                ProcessRegistry.Instance.Address = "client://" + hostPid.Address + "/" + hostPid.Id;
                            }
                            catch
                            {
                                //Ignore if this times out
                                _logger.LogInformation("Failed to retrieve host id on reconnect");
                            }

                            break;
                    }
                }));
            }
           
        }

        private void disposeConnection()
        {
            _endpointWriter.PoisonAsync().Wait();
            //Wait for the end of stream for the reader

            _clientEndpointReader.Dispose();
            _clientEndpointReader = null;
            _clientStreams?.Dispose();
        }

        private void connectToServer(PID endpointManager)
        {
            if (_channel == null || _channel.State != ChannelState.Ready)
            {
                _logger.LogTrace("Creating channel for connection");
                //THis hangs around even when there are no client rpcs

                _channel = new Channel(_hostName, _port, _config.ChannelCredentials, _config.ChannelOptions);
                _client = new ClientRemoting.ClientRemotingClient(_channel);
            }

            var connectionHeaders = new Metadata() {{"clientid", _clientId}};

            _clientStreams = _client.ConnectClient(connectionHeaders, null);


            _logger.LogDebug("Got client streams");
            
        }

//        private async Task retryConnection(PID endpointManager, int retryInterval)
//        {
//            while (_channel.State != ChannelState.Ready)
//            {
//                disposeConnection();
//                await connectToServer(endpointManager, retryInterval);
//            }
//        }
    }
}