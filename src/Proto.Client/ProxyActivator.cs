using System;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ProxyActivator: IActor
    {
        private readonly PID _endpointWriter;

        public ProxyActivator(PID endpointWriter)
        {
            _endpointWriter = endpointWriter; 
        }
        public Task ReceiveAsync(IContext context)
        {
            Console.WriteLine($"Proxy Activator {context.Self} Received Message - {context.Message.GetType()}");
            if (context.Message is ProxyPidRequest request)
            {
                var props =
                    Props.FromProducer(() => new ClientProxyActor(request.ClientPID, _endpointWriter)); 
                
                var clientProxyActorPid = RootContext.Empty.Spawn(props);
                //Send a return message with the proxy id contained within
                Console.WriteLine($"Sending created message to {context.Sender} using endpoint {_endpointWriter}");

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