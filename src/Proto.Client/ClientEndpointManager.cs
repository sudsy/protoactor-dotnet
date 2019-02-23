using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: IActor
    {
        private static ClientEndpointReader _clientEndpointReader;
        private int _endpointReferenceCount = 0;
        private string _hostName;
        private int _port;
        private RemoteConfig _config;
        private int _connectionTimeoutMs;

        public ClientEndpointManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _hostName = hostname;
            _port = port;
            _config = config;
            _connectionTimeoutMs = connectionTimeoutMs;
        }
        
        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case AcquireClientEndpointReference _:
                    if (_clientEndpointReader == null)
                    {
                        _clientEndpointReader = new ClientEndpointReader(_hostName, _port, _config, _connectionTimeoutMs);
                    
                        //TODO, catch a connection error here and reset the reference to nulll
                        var hostPID = await _clientEndpointReader.GetClientHostPID();
                        ProcessRegistry.Instance.Address = "client://" + hostPID.Address + "/" + hostPID.Id;
                        
                        
                    }
                    
                    
                    context.Respond(_clientEndpointReader);
                    _endpointReferenceCount++;
                    break;
                
                case ReleaseClientEndpointReference _:
                    _endpointReferenceCount--;
                    if (_endpointReferenceCount <= 0)
                    {
                        _clientEndpointReader.Dispose();
                        _clientEndpointReader = null;
                    }

                    break;
                
                case RemoteDeliver rd:
                    context.Forward(_clientEndpointReader.GetEndpointWriter());
                    break;
                
            }

            return;
        }
    }
}