using System;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientProxyActor: IActor
    {
        private readonly PID _proxyPid;
        private readonly PID _endpointWriter;

        public ClientProxyActor(PID proxyPid, PID endpointWriter)
        {
            _proxyPid = proxyPid;
            _endpointWriter = endpointWriter;
        }
        
        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started started:
                case Restarting restarting:
                     //Ignore lifecycle messages
                     return Actor.Done;
                 default:
                     Console.WriteLine($"Forwarding Message to stream - {context.Message}");
                     
            
                     var env = new RemoteDeliver(context.Headers, context.Message, _proxyPid, context.Sender, Serialization.DefaultSerializerId);
                     
                     context.Send(_endpointWriter, env);
            
                     return Actor.Done;
            }
            

        }
    }
}