using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointReader : IDisposable
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientEndpointReader>();
        private static readonly string _clientId = Guid.NewGuid().ToString();
        
        private static Channel _channel;
        private static ClientRemoting.ClientRemotingClient _client;
        
        private PID _endpointWriter;
        private AsyncDuplexStreamingCall<ClientMessageBatch, MessageBatch> _clientStreams;
        private TaskCompletionSource<PID> _clientHostPIDTCS = new TaskCompletionSource<PID>();
        private bool _disposed;
        

        static ClientEndpointReader()
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientHostProcess( pid));
        }
        
        public ClientEndpointReader(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _disposed = false;
            if (_channel == null || _channel.State != ChannelState.Ready)
            {
                _logger.LogTrace("Creating channel for connection");
                //THis hangs around even when there are no client rpcs
                _channel = new Channel(hostname, port, config.ChannelCredentials, config.ChannelOptions);
                _client = new ClientRemoting.ClientRemotingClient(_channel);
               
            }
            
            var connectionHeaders = new Metadata() {{"clientid", _clientId}};
                
            _clientStreams = _client.ConnectClient(connectionHeaders, null);
                
                
            _logger.LogDebug("Got client streams");
               

            _endpointWriter =
                RootContext.Empty.Spawn(Props.FromProducer(() =>
                    new ClientEndpointWriter(_clientStreams.RequestStream)));
                
            _logger.LogDebug("Created Endpoint Writer");
            
            var listenerTask = Task.Run(IncomingStreamListener);

        }

        public Task<PID> GetClientHostPID()
        {
            return _clientHostPIDTCS.Task;
        }

        public PID GetEndpointWriter()
        {
            return _endpointWriter;
        }
        
        private async Task IncomingStreamListener()
        {
            try
            {
                var responseStream = _clientStreams.ResponseStream;
                while (await responseStream.MoveNext(new CancellationToken())) //Need to work out how this might be cancelled
                {
                    _logger.LogDebug("Received Message Batch");
                    var messageBatch = responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,
                            messageBatch.TargetNames[envelope.Target]);

                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        if (message is ClientConnectionStarted)
                        {
                            _clientHostPIDTCS.SetResult(envelope.Sender);
//                            _clientHostAddress = envelope.Sender.Address;
//                            _allAddresses.Add(_clientHostAddress);
//                            ProcessRegistry.Instance.Address =
//                                "client://" + envelope.Sender.Address + "/" + envelope.Sender.Id;
//                            _receivedClientAddressTCS.SetResult(_clientHostAddress);
                            continue;
                        }


                        _logger.LogDebug(
                            $"Opened Envelope from {envelope.Sender} for {target} containing message {message}");
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);

                        RootContext.Empty.Send(target, localEnvelope);
                    }

                }
            }
            catch (Exception ex)
            {
                
                if (ex is RpcException rpcEx)
                {
                    if (rpcEx.StatusCode.Equals(StatusCode.Cancelled) || _disposed)
                    {
                        return;
                    }
                }
                if (ex is InvalidOperationException)
                {
                    if (_disposed)
                    {
                        //Do nothing, this is an expected exception when we cancel the connection
                        
                        return;
                    }
                }

                _logger.LogCritical(ex, $"Exception Thrown from inside stream listener task");
                throw ex;
            }

        }

      public void Dispose()
      {
          _disposed = true;
          _endpointWriter.PoisonAsync().Wait();
          //Wait for the end of stream for the reader
                
          _clientStreams?.Dispose();
          
      }


    }
}