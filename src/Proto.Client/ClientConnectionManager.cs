using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientConnectionManager : IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientConnectionManager>();
        private static readonly string _clientId = Guid.NewGuid().ToString();
        private static Channel _channel;
        private ClientRemoting.ClientRemotingClient _client;
        private readonly string _hostName;
        private readonly int _port;
        private readonly RemoteConfig _config;
        private readonly int _connectionTimeoutMs;
        private AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;
        private PID _endpointReader;
        private readonly Behavior _behaviour;
        


        public ClientConnectionManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _hostName = hostname;
            _port = port;
            _config = config;
            _connectionTimeoutMs = connectionTimeoutMs;
            _behaviour = new Behavior();
            _behaviour.Become(Starting);
            
           
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Stopping _:
                    _logger.LogDebug($"Sending end of stream signal to server");
                    await _clientStreams.RequestStream.CompleteAsync();
                    break;
            }

            await _behaviour.ReceiveAsync(context);
        }

        private Task Starting(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Creating Channel");
                    
                    if (_channel == null || _channel.State != ChannelState.Ready)
                    {
                        _channel = new Channel(_hostName, _port, _config.ChannelCredentials, _config.ChannelOptions);
                    }
                   
                    _logger.LogDebug("Creating Remoting Client");
                    _client = new ClientRemoting.ClientRemotingClient(_channel);
                    var connectionHeaders = new Metadata() {{"clientid", _clientId}};
                    _logger.LogDebug("Connectiing Streams");
                    _clientStreams = _client.ConnectClient(connectionHeaders, null);


                    _logger.LogDebug("Got client streams - creating endpoint reader");

                    
//                    var endpointRunner = Task.Run(async () =>
//                    {
                        //Start this in a new process so the loop is not affected by parent processes shuttting down (eg. Orleans)
                        _endpointReader =
                            context.SpawnPrefix(Props.FromProducer(() =>
                                    new ClientEndpointReader(_clientStreams.ResponseStream))
                                , "reader");
                        _behaviour.Become(WaitingForPID);
                        context.Request(_endpointReader, new ClientHostPIDRequest());
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(_connectionTimeoutMs));
                        
                        
//                    });

                  
                   
                    
                    
                    _logger.LogDebug("Endpoint reader created");
                    break;
               
               
            }

            return Actor.Done;
        }

        private Task WaitingForPID(IContext context)
        {
            switch (context.Message)
            {
                case ClientHostPIDResponse pidResponse:
                    context.CancelReceiveTimeout();
                    context.Send(context.Parent, pidResponse);
                    _behaviour.Become(Started);
                    _logger.LogDebug("PID Response Received");
                    break;
                case ReceiveTimeout _:
                    throw new ApplicationException("Failed to receive client PID within the timeout");
                    
            }

            return Actor.Done;
        }

        private async Task Started(IContext context)
        {
            switch (context.Message)
            {
                
                
                case RemoteDeliver rd:
                    var batch = rd.getMessageBatch();
            
                    _logger.LogDebug($"Sending RemoteDeliver message {rd.Message.GetType()} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                
                    var clientBatch = new ClientMessageBatch()
                    {
                        Address = rd.Target.Address,
                        Batch = batch
                    };
                    try
                    {

                        await _clientStreams.RequestStream.WriteAsync(clientBatch);
                    }
                    catch (Exception ex)
                    {
                        context.Stash();
                        throw ex;
                    }
                    
                    
                    _logger.LogDebug($"Sent RemoteDeliver message {rd.Message.GetType()} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                    break;
               
               
            }

            
        }

       
    }
}