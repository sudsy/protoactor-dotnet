using System;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientHostEndpointWriter :IActor
    {
        private readonly IServerStreamWriter<MessageBatch> _responseStream;

        public ClientHostEndpointWriter(IServerStreamWriter<MessageBatch> responseStream)
        {
            _responseStream = responseStream;
        }
        public Task ReceiveAsync(IContext context)
        {
            Console.WriteLine($"Endpoint Writer {context.Self} received message {context.Message}");
            if (!(context.Message is RemoteDeliver rd)) return Actor.Done;

          
            var batch = rd.getMessageBatch();
            Console.WriteLine("Sending Batch to stream");
            return _responseStream.WriteAsync(batch);

        }
    }
}