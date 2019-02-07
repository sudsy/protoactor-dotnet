using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class ProxyActivator: IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(ClientHost).FullName);
        private readonly PID _endpointWriter;

        public ProxyActivator(PID endpointWriter)
        {
            _endpointWriter = endpointWriter; 
        }
        public Task ReceiveAsync(IContext context)
        {
            Logger.LogDebug($"Proxy Activator Received Message {context.Message}");
           
            if (context.Message is ProxyPidRequest request)
            {
                var props =
                    Props.FromProducer(() => new ClientProxyActor(request.ClientPID, _endpointWriter)); 
                
                var clientProxyActorPid = RootContext.Empty.Spawn(props);
                //Send a return message with the proxy id contained within
           

                var proxyResponse = new ProxyPidResponse()
                {
                    ProxyPID = clientProxyActorPid
                };
                
                var env = new RemoteDeliver(null, proxyResponse, context.Sender, context.Self, Serialization.DefaultSerializerId);
                context.Send(_endpointWriter, env);
            }

            return Actor.Done;
        }
    }
}