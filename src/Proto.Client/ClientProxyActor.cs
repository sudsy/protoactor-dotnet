using System.Threading.Tasks;
using Grpc.Core;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientProxyActor: IActor
    {
        private PID _proxyPID;

        public ClientProxyActor(PID proxyPID, IServerStreamWriter<MessageBatch> responseStream)
        {
            //TODO - fix this reference to another actor that manages the stream
            _proxyPID = proxyPID;
        }
        
        public Task ReceiveAsync(IContext context)
        {
            throw new System.NotImplementedException();
        }
    }
}