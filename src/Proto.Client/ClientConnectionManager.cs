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
        private string _hostName;
        private int _port;
        private RemoteConfig _config;
        private int _connectionTimeoutMs;
        private AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;
        private PID _endpointWriter;
        private PID _endpointReader;
        private Behavior _behaviour;
        
        private ClientHostPIDResponse _clientHostPidResponse;


        public ClientConnectionManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
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
                case Stopping _:
                    _logger.LogDebug($"Sending end of stream signal to server");
                    await _clientStreams.RequestStream.CompleteAsync();
                    break;
                case Started _:
                    _channel = new Channel(_hostName, _port, _config.ChannelCredentials, _config.ChannelOptions);
                    _client = new ClientRemoting.ClientRemotingClient(_channel);
                    var connectionHeaders = new Metadata() {{"clientid", _clientId}};

                    _clientStreams = _client.ConnectClient(connectionHeaders, null);


                    _logger.LogDebug("Got client streams");

                    
                    //This will let the parent decide whether to restart the connection or not
                    var escalateFailureStrategy = new OneForOneStrategy(((pid, reason) => SupervisorDirective.Escalate), 0,
                        TimeSpan.MinValue);

                    _endpointReader =
                        context.SpawnPrefix(Props.FromProducer(() =>
                                new ClientEndpointReader(_clientStreams.ResponseStream))
                            .WithChildSupervisorStrategy(escalateFailureStrategy), "reader");
                    
                    //It's better to await the clientHostPID Response here makes everything much simpler - that way we are sure that everything is right before trying to deliver
                    _clientHostPidResponse =
                        await context.RequestAsync<ClientHostPIDResponse>(_endpointReader, new ClientHostPIDRequest(), TimeSpan.FromMilliseconds(_connectionTimeoutMs));
                    
                    break;
                case ClientHostPIDRequest _:
                    context.Respond(_clientHostPidResponse);
                    break;
                
                case RemoteDeliver rd:
                    var batch = rd.getMessageBatch();
            
                    _logger.LogDebug($"Sending RemoteDeliver message {rd.Message} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                
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
                    
                    
                    _logger.LogDebug($"Sent RemoteDeliver message {rd.Message} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                    break;
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