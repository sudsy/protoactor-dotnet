using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientConnectionManager : IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientConnectionManager>();
        private static readonly string _clientId = Guid.NewGuid().ToString();
        private Channel _channel;
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

        private async Task Starting(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Creating Channel");
                    _channel = new Channel(_hostName, _port, _config.ChannelCredentials, _config.ChannelOptions);
                    _logger.LogDebug("Creating Remoting Client");
                    _client = new ClientRemoting.ClientRemotingClient(_channel);
                    var connectionHeaders = new Metadata() {{"clientid", _clientId}};
                    _logger.LogDebug("Connectiing Streams");
                    _clientStreams = _client.ConnectClient(connectionHeaders, null);


                    _logger.LogDebug("Got client streams - creating endpoint reader");

                    Task.Run(() =>
                    {
                        //Start this in a new process so the loop is not affected by parent processes shuttting down (eg. Orleans)
                        _endpointReader =
                            context.SpawnPrefix(Props.FromProducer(() =>
                                    new ClientEndpointReader(_clientStreams.ResponseStream))
                                , "reader");
                    });
                    
                   
                    
                    _logger.LogDebug("Endpoint reader created");
                    break;
               
                case ClientHostPIDResponse clientHostPidResponse:
                    _behaviour.Become(Started);
                    context.Forward(context.Parent);
                    break;
                case RemoteDeliver rd:
                    _logger.LogWarning("Unexpected Remote deliver message while starting");
                    break;
            }
        }

        public async Task Started(IContext context)
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
                        await WriteWithTimeout(clientBatch, TimeSpan.FromSeconds(10));
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

        
        private async Task WriteWithTimeout(ClientMessageBatch batch, TimeSpan timeout)
        {
            var timeoutPolicy = Policy.TimeoutAsync(timeout, TimeoutStrategy.Pessimistic);

            try
            {
                await timeoutPolicy.ExecuteAsync(() => _clientStreams.RequestStream.WriteAsync(batch));
            }
            catch
            {
                _logger.LogError($"DeadLetter - could not send client message batch {batch} to server");
            }
            
            
        }
    }
}