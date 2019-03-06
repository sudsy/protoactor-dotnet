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
                    if (str == "getclienthostpid")
                    {
                        context.Respond(await _clientEndpointReader.GetClientHostPID());
                    }
                    break;
                case AcquireClientEndpointReference _:
                    if (_clientEndpointReader == null)
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
                        var clientEndpointReader = new ClientEndpointReader(_clientStreams.ResponseStream,  _connectionTimeoutMs);
                        var hostPid = await clientEndpointReader.GetClientHostPID();
                        _clientEndpointReader = clientEndpointReader;
                        _endpointWriter =
                            RootContext.Empty.Spawn(Props.FromProducer(() =>
                                new ClientEndpointWriter(_clientStreams.RequestStream)));
                
                        _logger.LogDebug("Created Endpoint Writer");
                        
                        ProcessRegistry.Instance.Address = "client://" + hostPid.Address + "/" + hostPid.Id;
                           
                    
                        
                    }
                    
                    
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
                    break;
                
                case ReleaseClientEndpointReference _:
                    _endpointReferenceCount--;
                    if (_endpointReferenceCount <= 0)
                    {
                        _endpointWriter.PoisonAsync().Wait();
                        //Wait for the end of stream for the reader
                
                        _clientEndpointReader.Dispose();
                        _clientEndpointReader = null;
                        _clientStreams?.Dispose();
                    }

                    break;
                
                case RemoteDeliver rd:
                    context.Forward(_endpointWriter);
                    break;
                
            }

            return;
        }
    }
}