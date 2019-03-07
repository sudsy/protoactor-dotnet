using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
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
            
                    await WriteWithTimeout(clientBatch, TimeSpan.FromSeconds(30));
                    
                    Logger.LogDebug($"Sent RemoteDeliver message {rd.Message} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");
                    break;
                    
            }
           

        }
        
        private async Task WriteWithTimeout(ClientMessageBatch batch, TimeSpan timeout)
        {
            var timeoutPolicy = Policy.TimeoutAsync(timeout, TimeoutStrategy.Pessimistic);

            try
            {
                await timeoutPolicy.ExecuteAsync(() => _requestStream.WriteAsync(batch));
            }
            catch
            {
                Logger.LogError($"DeadLetter - could not send client message batch {batch} to server");
            }
            
            
        }

      
    }
}