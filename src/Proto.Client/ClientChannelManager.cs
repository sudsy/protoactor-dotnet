using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientChannelManager: IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<ClientChannelManager>();
        
        private int _endpointReferenceCount = 0;
        private string _hostName;
        private int _port;
        private RemoteConfig _config;
        private int _connectionTimeoutMs;
        
        private PID _clientRemotingManager;
        
        private Behavior _behaviour;
        private Queue<PID> _endpointReferenceRequestors = new Queue<PID>();
        private PID _hostProcess;
        private Channel _channel;


        public ClientChannelManager(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            _logger.LogDebug("Constructor for Client Endpoint Manager called");
            _hostName = hostname;
            _port = port;
            _config = config;
            _connectionTimeoutMs = connectionTimeoutMs;
            _behaviour = new Behavior();
            _behaviour.Become(NoConnection);
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Stopped)
            {
                return _channel.ShutdownAsync();
            }
            
            return _behaviour.ReceiveAsync(context);
        }

        private Task NoConnection(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Creating Channel");
                    
                   
                    _channel = new Channel(_hostName, _port, _config.ChannelCredentials, _config.ChannelOptions);
                   

                    break;
                
                case AcquireClientEndpointReference _:
           
                    
                    _logger.LogDebug($"Acquiring EndpointReference  - reference count prior to grant is {_endpointReferenceCount}");
                       
                    //Standard Supervisor strategy should work, we want a restart in case of failure - we will stop it when finished with it

                    
                    var escalateFailureStrategy = new OneForOneStrategy((pid, reason) =>
                    {
                        
                        _behaviour.Become(WaitingForConnection);
                        
                        return SupervisorDirective.Escalate;
                    }, 1,null);

                   
                    _clientRemotingManager = context.SpawnPrefix(Props.FromProducer(() =>
                            new ClientRemotingManager(_channel, _connectionTimeoutMs))
                            .WithChildSupervisorStrategy(escalateFailureStrategy),
                        "connmgr");
                    _logger.LogDebug($"Spawned connection manager - endpoint manager now has {context.Children.Count} child(ren)");

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
                    _logger.LogDebug("AcquireClientReference Request Received while waiting for connection");
                    _endpointReferenceRequestors.Enqueue(context.Sender);
                    break;
                    //Shouldn't need to deal with release request here because nothing has been allocated
                case ClientHostPIDResponse clientHostPidResponse:
                    //Check if this is the same as the existing address - invalidate existing clients if not
                    var clientAddress = "client://" + clientHostPidResponse.HostProcess.Address + "/" +
                                        clientHostPidResponse.HostProcess.Id;
                    _logger.LogInformation($"Connected to clienthost as {clientAddress}");
                 
                    
                    ProcessRegistry.Instance.Address = clientAddress;
                 
                    _hostProcess = clientHostPidResponse.HostProcess;
                    
                    while (_endpointReferenceRequestors.Count > 0)
                    {
                        _logger.LogDebug($"Responding to AcquireReferenceCount received while starting - reference count prior to grant is {_endpointReferenceCount}");
                        var referenceRequestor = _endpointReferenceRequestors.Dequeue();
                        _endpointReferenceCount++;
                        context.Send(referenceRequestor, _endpointReferenceCount);
                    }
                    
                    
                    _behaviour.Become(ConnectionStarted);
                    break;
                
                case ReleaseClientEndpointReference _:
                    reduceReferenceCount();

                    break;
                case RemoteDeliver rd:
                    if (_clientRemotingManager != null)
                    {
                       
                        context.Forward(_clientRemotingManager);
                    }
                    else
                    {
                        _logger.LogDebug("Dumping Remote Deliver message since _clientConnection unavailable");
                    }
                   
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
                    _logger.LogDebug($"Acquiring EndpointReference while connection started - reference count prior to grant is {_endpointReferenceCount}");
                       
                   
 
                    _endpointReferenceCount++;
                    context.Respond(_endpointReferenceCount);
                    break;
                
                case ReleaseClientEndpointReference _:
                    reduceReferenceCount();

                    break;
                
                case RemoteDeliver rd:
                    
                    _logger.LogDebug($"Forwarding Remote Deliver Message to endpoint Writer");
                    context.Forward(_clientRemotingManager);
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
                _clientRemotingManager.Poison();
                _clientRemotingManager = null;
                _endpointReferenceCount = 0; //Just to be sure it's never less than zero
                _behaviour.Become(NoConnection);
            }
        }
    }
}