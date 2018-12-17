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
            if (!(context.Message is RemoteDeliver rd)) return Actor.Done;
            
            var batch = Client.getMessageBatch(rd);
            return _responseStream.WriteAsync(batch);

        }
    }
}