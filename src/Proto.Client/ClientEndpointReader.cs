using System;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointReader : IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientEndpointReader>();
        private readonly IAsyncStreamReader<MessageBatch> _responseStream;
        private Object _clientHostPIDResponse;
        private PID _pidRequester;




        static ClientEndpointReader()
        {
            ProcessRegistry.Instance.RegisterHostResolver(pid => new ClientHostProcess(pid));
        }

        public ClientEndpointReader(IAsyncStreamReader<MessageBatch> responseStream)
        {
            _responseStream = responseStream;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    context.Send(context.Self, "listen");
                    break;
                case ClientHostPIDRequest _:
                    if (_clientHostPIDResponse != null)
                    {
                        context.Respond(_clientHostPIDResponse);
                    }
                    else
                    {
                        _pidRequester = context.Sender;
                    }
                    break;
                case String str:
                    if (str != "listen")
                    {
                        return;
                    }

                    await _responseStream.MoveNext(new CancellationToken());
                    _logger.LogDebug("Received Message Batch");
                    var messageBatch = _responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,
                            messageBatch.TargetNames[envelope.Target]);

                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        if (message is ClientHostPIDResponse)
                        {
                            if (_pidRequester != null)
                            {
                                context.Send(_pidRequester, message);
                            }
                            else
                            {
                                _clientHostPIDResponse = message; 
                            }
                           
                            context.Send(context.Self, "listen");
                            return;

                        }


                        _logger.LogDebug(
                            $"Opened Envelope from {envelope.Sender} for {target} containing message {message}");
                        //todo: Need to convert the headers here
                        var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, null);

                        context.Send(target, localEnvelope);
                        context.Send(context.Self, "listen");
                    }

                        
                    
                    break;
            }
        
        }
    }
}