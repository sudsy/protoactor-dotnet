using System;
using System.Collections.Generic;
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
        
        private Behavior _behaviour;
        private Queue<PID> _endpointReferenceRequestors = new Queue<PID>();
        private PID _hostProcess;


        public ClientEndpointManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _hostName = hostname;
            _port = port;
            _config = config;
            _connectionTimeoutMs = connectionTimeoutMs;
            _behaviour = new Behavior();
            _behaviour.Become(NoConnection);
        }

        public Task ReceiveAsync(IContext context)
        {
            
            return _behaviour.ReceiveAsync(context);
        }

        private Task NoConnection(IContext context)
        {
            switch (context.Message)
            {
                case AcquireClientEndpointReference _:
                    _logger.LogDebug($"Acquiring EndpointReference  - reference count prior to grant is {_endpointReferenceCount}");
                       
                    //Standard Supervisor strategy should work, we want a restart in case of failure - we will stop it when finished with it

                    
                    var escalateFailureStrategy = new OneForOneStrategy((pid, reason) =>
                    {
                        _behaviour.Become(WaitingForConnection);
                        return SupervisorDirective.Escalate;
                    }, 1,null);

                    //TODO: Maybe we need exponential backoff here
                    _clientConnectionManager = context.SpawnPrefix(Props.FromProducer(() =>
                            new ClientConnectionManager(_hostName, _port, _config, _connectionTimeoutMs))
                            .WithChildSupervisorStrategy(escalateFailureStrategy),
                        "connmgr");

                    _endpointReferenceRequestors.Enqueue(context.Sender);
                    _behaviour.Become(WaitingForConnection);
                    break;
                
            }

            return Actor.Done;
        }

        private Task WaitingForConnection(IContext context)
        {
            switch (context.Message)
            {
                case AcquireClientEndpointReference _:
                    _endpointReferenceRequestors.Enqueue(context.Sender);
                    break;
                    //Shouldn't need to deal with release request here because nothing has been allocated
                case ClientHostPIDResponse clientHostPidResponse:
                    ProcessRegistry.Instance.Address = "client://" + clientHostPidResponse.HostProcess.Address + "/" +
                                                       clientHostPidResponse.HostProcess.Id;

                    _hostProcess = clientHostPidResponse.HostProcess;
                    
                    while (_endpointReferenceRequestors.Count > 0)
                    {
                        var referenceRequestor = _endpointReferenceRequestors.Dequeue();
                        _endpointReferenceCount++;
                        context.Send(referenceRequestor, _endpointReferenceCount);
                    }
                    
                    _behaviour.Become(ConnectionStarted);
                    break;
                
                case ReleaseClientEndpointReference _:
                    reduceReferenceCount();

                    break;
            }

            return Actor.Done;
        }

        public async Task ConnectionStarted(IContext context)
        {
            switch (context.Message)
            {
                case String str:
                    if (str == "getclienthostpid")
                    {
                        context.Respond(_hostProcess);
                    }
                    break;
                case AcquireClientEndpointReference _:
                    _logger.LogDebug($"Acquiring EndpointReference  - reference count prior to grant is {_endpointReferenceCount}");
                       
                   
 
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
                    break;
                
                case ReleaseClientEndpointReference _:
                    reduceReferenceCount();

                    break;
                
                case RemoteDeliver rd:
                    
                    _logger.LogDebug($"Forwarding Remote Deliver Message to endpoint Writer");
                    context.Forward(_clientConnectionManager);
                    break;
                
            }

            return;
        }

        private void reduceReferenceCount()
        {
            _endpointReferenceCount--;
            _logger.LogDebug($"Releasing EndpointReference  - reference count after release is {_endpointReferenceCount}");
            if (_endpointReferenceCount <= 0)
            {
                _clientConnectionManager.Stop();
                _clientConnectionManager = null;
                _endpointReferenceCount = 0; //Just to be sure it's never less than zero
                _behaviour.Become(NoConnection);
            }
        }
    }
}