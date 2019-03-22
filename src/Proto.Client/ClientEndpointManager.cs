using System;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientEndpointManager>();
        
        private int _endpointReferenceCount = 0;
        private string _hostName;
        private int _port;
        private RemoteConfig _config;
        private int _connectionTimeoutMs;
        
        private PID _clientConnectionManager;
        private ClientHostPIDResponse _clientHostPidResponse;


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
                        context.Respond(_clientHostPidResponse.HostProcess);
                    }
                    break;
                case AcquireClientEndpointReference _:
                    _logger.LogDebug($"Acquiring EndpointReference  - reference count prior to grant is {_endpointReferenceCount}");
                       
                    //Standard Supervisor strategy should work, we want a restart in case of failure - we will stop it when finished with it

                    if (_endpointReferenceCount <= 0)
                    {
                        //TODO: Maybe we need exponential backoff here
                        _clientConnectionManager = context.SpawnPrefix(Props.FromProducer(() =>
                                new ClientConnectionManager(_hostName, _port, _config, _connectionTimeoutMs)),
                            "connmgr");

                        //Can't hand out a reference until the Process Address is set otherwise sender PIDs will all be wrong
                        _clientHostPidResponse = await context.RequestAsync<ClientHostPIDResponse>(_clientConnectionManager,
                            new ClientHostPIDRequest());
                        
                        ProcessRegistry.Instance.Address = "client://" + _clientHostPidResponse.HostProcess.Address + "/" +
                                                           _clientHostPidResponse.HostProcess.Id;
                    }
                    
                   
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
                    break;
                
                case ReleaseClientEndpointReference _:
                    _endpointReferenceCount--;
                    if (_endpointReferenceCount <= 0)
                    {
                        _clientConnectionManager.Stop();
                        _clientConnectionManager = null;
                        _endpointReferenceCount = 0; //Just to be sure it's never less than zero
                    }

                    break;
                
                case RemoteDeliver rd:
                    
                    _logger.LogDebug($"Forwarding Remote Deliver Message to endpoint Writer");
                    context.Forward(_clientConnectionManager);
                    break;
                
            }

            return;
        }
        
      

   




    }
}