using System;
using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientProxyActor: IActor
    {
        private PID _proxyPID;
        private IServerStreamWriter<ClientMessageBatch> _responseStream;

        public ClientProxyActor(PID proxyPID, IServerStreamWriter<ClientMessageBatch> responseStream)
        {
            //TODO - fix this reference to another actor that manages the stream
            _proxyPID = proxyPID;
            _responseStream = responseStream;
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
                     try
                     {
                         _responseStream.WriteAsync(ClientContext.getClientMessageBatch(_proxyPID, context.Message, Serialization.DefaultSerializerId, context.Sender, context.Headers));    
                     }catch(Exception ex)
                     {
                         Console.WriteLine(ex.Message);
                     }
            
                     return Actor.Done;
            }
            

        }
    }
}