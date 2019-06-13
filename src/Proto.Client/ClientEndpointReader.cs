using System;
using System.Data;
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
        private ClientHostPIDResponse _clientHostPIDResponse;
        private PID _pidRequester;



     

        public ClientEndpointReader(IAsyncStreamReader<MessageBatch> responseStream)
        {
            _responseStream = responseStream;
        }

        public async Task ReceiveAsync(IContext context)
        {

            switch (context.Message)
            {
                
                case ClientHostPIDRequest _:
                    _logger.LogDebug("Received PID Request");
                    if (_clientHostPIDResponse != null)
                    {
                        _logger.LogDebug("Returning PID Response");
                        context.Respond(_clientHostPIDResponse);
                    }
                    else
                    {
                        _pidRequester = context.Sender;
                    }
                    //start listening
                    context.Send(context.Self, "listen");

                    break;
                
                case String str:
                    if (str != "listen")
                    {
                        return;
                    }
                   
                    //We use a recursive message here to allow errors in this method to be handled by the supervision hierarchy and still receive system messages
                    
                    _logger.LogDebug("Awaiting Message Batch");
                    if (!await _responseStream.MoveNext(new CancellationToken()))
                    {
                        //This is when we are at the end of the stream - Means we received the end of stream signal from the server
                        return;
                    }

                    _logger.LogDebug("Received Message Batch");
                    var messageBatch = _responseStream.Current;
                    foreach (var envelope in messageBatch.Envelopes)
                    {
                        var target = new PID(ProcessRegistry.Instance.Address,
                            messageBatch.TargetNames[envelope.Target]);

                        var message = Serialization.Deserialize(messageBatch.TypeNames[envelope.TypeId],
                            envelope.MessageData, envelope.SerializerId);

                        if (message is ClientHostPIDResponse pidResponse)
                        {
                            _logger.LogDebug("Received PID Response");
                            if (_pidRequester != null)
                            {
                                _logger.LogDebug($"Sending PID Response to requester {_pidRequester}");
                                context.Send(_pidRequester, message);
                            }
                            else
                            {
                                _logger.LogDebug($"Storing PID Response for later");
                                _clientHostPIDResponse = pidResponse;
                            }
                            context.Send(context.Self, "listen");
                            return;

                        }


                        _logger.LogDebug(
                            $"Opened Envelope from {envelope.Sender} for {target} containing message {message.GetType()}");
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