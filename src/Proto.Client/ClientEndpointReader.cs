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
        
        private TaskCompletionSource<PID> _clientHostPIDTCS = new TaskCompletionSource<PID>();
        private bool _disposed;
        private CancellationTokenSource _cancelWaitingForPID;
        private IAsyncStreamReader<MessageBatch> _responseStream;
        private PID _endpointManager;


        static ClientEndpointReader()
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientHostProcess( pid));
        }
        
        public ClientEndpointReader(PID endpointManager, IAsyncStreamReader<MessageBatch> responseStream, int connectionTimeoutMs = 10000)
        {
            _cancelWaitingForPID = new CancellationTokenSource(connectionTimeoutMs);
            _cancelWaitingForPID.Token.Register(this.Dispose);
            
            _disposed = false;

            _endpointManager = endpointManager;
            _responseStream = responseStream;

            
            var listenerTask = Task.Run(IncomingStreamListener);

        }

        public Task<PID> GetClientHostPID()
        {
            return _clientHostPIDTCS.Task;
        }

    
        
        private async Task IncomingStreamListener()
        {
            try
            {
                
                while (await _responseStream.MoveNext(new CancellationToken())) //Need to work out how this might be cancelled
                {
                    _logger.LogDebug("Received Message Batch");
                    var messageBatch = _responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,
                            messageBatch.TargetNames[envelope.Target]);

                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        if (message is ClientConnectionStarted)
                        {
                            _cancelWaitingForPID?.Dispose();
                            _cancelWaitingForPID = null;
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
                
//                if (ex is RpcException rpcEx)
//                {
//                    if (rpcEx.StatusCode.Equals(StatusCode.Cancelled) || _disposed)
//                    {
//                        return;
//                    }
//                }
//                if (ex is InvalidOperationException)
//                {
//                    if (_disposed)
//                    {
//                        //Do nothing, this is an expected exception when we cancel the connection
//                        
//                        return;
//                    }
//                }

                _logger.LogCritical(ex, $"Exception Thrown from inside stream listener task");
                RootContext.Empty.Send(_endpointManager, "connectionfailure");
                throw ex;
            }

        }

      public void Dispose()
      {
          _disposed = true;
          
          
          
      }


    }
}