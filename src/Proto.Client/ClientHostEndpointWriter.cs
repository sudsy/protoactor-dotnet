using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientHostEndpointWriter :IActor
    {
        private readonly IServerStreamWriter<MessageBatch> _responseStream;
        private static readonly ILogger Logger = Log.CreateLogger<ClientHostEndpointWriter>();

        public ClientHostEndpointWriter(IServerStreamWriter<MessageBatch> responseStream)
        {
            _responseStream = responseStream;
        }
        public async Task ReceiveAsync(IContext context)
        {
            Logger.LogDebug($"ClientHostEndpointwriter received {context.Message}");
            
            switch (context.Message)
            {
                case Started started:
                    
                    //Send a connection started message to be delivered over this response stream
                    
                    
                    context.Send(context.Self, new  RemoteDeliver(null, new ClientHostPIDResponse(){HostProcess = context.Self}, context.Self, context.Self, Serialization.DefaultSerializerId));
                    break;

                case ClientMessageBatch cmb:
                    await WriteWithTimeout(cmb.Batch, TimeSpan.FromSeconds(30));
                    break;

                case RemoteDeliver rd:
                    
                    Logger.LogDebug($"Sending RemoteDeliver message {rd.Message} to {rd.Target.Id} address {rd.Target.Address} from {rd.Sender}");

                    var batch = rd.getMessageBatch();
           
                    await WriteWithTimeout(batch, TimeSpan.FromSeconds(30));
            
                    Logger.LogDebug($"Sent RemoteDeliver message {rd.Message} to {rd.Target.Id}");
                    
                    break;
            }

            
           

        }

        private async Task WriteWithTimeout(MessageBatch batch, TimeSpan timeout)
        {
            var timeoutPolicy = Policy.TimeoutAsync(timeout, TimeoutStrategy.Pessimistic);

            try
            {
                await timeoutPolicy.ExecuteAsync(() => _responseStream.WriteAsync(batch));
            }
            catch
            {
                Logger.LogError($"DeadLetter - could not send message batch {batch} to client");
            }
            
            
        }
    }
}