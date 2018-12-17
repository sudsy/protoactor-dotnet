using System;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ProxyActivator: IActor
    {
        private IServerStreamWriter<ClientMessageBatch> _responseStream;

        public ProxyActivator(IServerStreamWriter<ClientMessageBatch> responseStream)
        {
            _responseStream = responseStream; //TODO: fix this to take an actor as a refence to ensure we write to stream in a thread safe way
        }
        public async Task ReceiveAsync(IContext context)
        {
            Console.WriteLine($"Proxy Activator {context.Self} Received Message - {context.Message.GetType()}");
            if (context.Message is ProxyPidRequest request)
            {
                var props =
                    Props.FromProducer(() => new ClientProxyActor(request.ClientPID, _responseStream)); //TODO: fix this to take an actor as a refence to ensure we write to stream in a thread safe way
                
                var clientProxyActorPID = RootContext.Empty.Spawn(props);
                //Send a return message with the proxy id contained within
                Console.WriteLine("Sending created message");
                //todo: move this inside the actor started hook - might be able to use a better abstraction here too
                await _responseStream.WriteAsync(Client.getClientMessageBatch(context.Sender,
                    new ProxyPidResponse()
                    {
                        ProxyPID = clientProxyActorPID
                    }, Serialization.DefaultSerializerId));
            }

            
        }
    }
}