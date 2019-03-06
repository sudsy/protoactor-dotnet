using System;
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
                case String str:
                    if (str == "getclienthostpid")
                    {
                        context.Respond(await _clientEndpointReader.GetClientHostPID());
                    }
                    break;
                case AcquireClientEndpointReference _:
                    if (_clientEndpointReader == null)
                    {
                        
                      
                        var clientEndpointReader = new ClientEndpointReader(_hostName, _port, _config, _connectionTimeoutMs);
                        var hostPid = await clientEndpointReader.GetClientHostPID();
                        _clientEndpointReader = clientEndpointReader;
                        ProcessRegistry.Instance.Address = "client://" + hostPid.Address + "/" + hostPid.Id;
                           
                    
                        
                    }
                    
                    
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
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