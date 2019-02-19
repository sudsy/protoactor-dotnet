using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointWriter: IActor
    {
        private readonly IClientStreamWriter<ClientMessageBatch> _requestStream;
        private static readonly ILogger Logger = Log.CreateLogger(typeof(Client).FullName);

        public ClientEndpointWriter(IClientStreamWriter<ClientMessageBatch> clientStreamsRequestStream)
        {
            _requestStream = clientStreamsRequestStream;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Stopping _:
                    Logger.LogDebug($"Sending end of stream signal to server");
                    await _requestStream.CompleteAsync();
                    break;
                case RemoteDeliver rd:
                    var batch = rd.getMessageBatch();
            
                    Logger.LogDebug($"Sending RemoteDeliver message {rd.Message} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                
                    var clientBatch = new ClientMessageBatch()
                    {
                        Address = rd.Target.Address,
                        Batch = batch
                    };
            
                    await _requestStream.WriteAsync(clientBatch);
                    break;
                    
            }
           

        }

      
    }
}