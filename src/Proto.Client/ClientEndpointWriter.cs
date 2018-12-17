using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointWriter: IActor
    {
        private readonly IClientStreamWriter<ClientMessageBatch> _requestStream;

        public ClientEndpointWriter(IClientStreamWriter<ClientMessageBatch> clientStreamsRequestStream)
        {
            _requestStream = clientStreamsRequestStream;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (!(context.Message is RemoteDeliver rd)) return Actor.Done;
            
            var batch = Client.getMessageBatch(rd);
                
            var clientBatch = new ClientMessageBatch()
            {
                Address = rd.Target.Address,
                Batch = batch
            };

            return _requestStream.WriteAsync(clientBatch);


        }

      
    }
}