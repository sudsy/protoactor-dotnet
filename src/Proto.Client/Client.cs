using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Client
{
    public class Client : IDisposable
    {
        private static readonly ILogger _logger = Log.CreateLogger<Client>();
        private static PID _clientEndpointManager;
        private static Tuple<string, int> _endpointConfig;
        
        
        private PID _clientHostPID;


        static Client()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }

        public static async Task<Client> CreateAsync(string hostname, int port, RemoteConfig config, int connectionTimeoutMs = 10000)
        {
            
            var endpointConfig = Tuple.Create(hostname, port);
            if (_endpointConfig != null & !endpointConfig.Equals(_endpointConfig))
            {
                throw new InvalidOperationException("Can't connect to multiple client hosts");
            }
            if (_clientEndpointManager == null)
            {
                var clientEndpointManager =
                    RootContext.Empty.SpawnPrefix(Props.FromProducer(() => new ClientEndpointManager(hostname, port, config, connectionTimeoutMs)), "clientMarshaller");
                await RootContext.Empty.RequestAsync<int>(clientEndpointManager, new AcquireClientEndpointReference(), TimeSpan.FromMilliseconds(connectionTimeoutMs));
                _clientEndpointManager = clientEndpointManager;
                _endpointConfig = endpointConfig;
                return new Client();    
            }

            await RootContext.Empty.RequestAsync<int>(_clientEndpointManager, new AcquireClientEndpointReference(),
                TimeSpan.FromMilliseconds(connectionTimeoutMs));
            return new Client();  
            
            
            
        }

        private Client()
        {
            
        }

      
 
        public Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnNamedAsync(address, name, kind, timeout);
        }

        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return Remote.Remote.SpawnAsync(address, kind, timeout);
        }

        public async Task<PID> GetClientHostPID()
        {
            if (_clientHostPID != null)
            {
                return _clientHostPID;
            }
            _clientHostPID = await RootContext.Empty.RequestAsync<PID>(_clientEndpointManager, "getclienthostpid");
            return _clientHostPID;


        }

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string name, string kind, TimeSpan timeout)
        {
            var hostPID = await GetClientHostPID();
            return await SpawnNamedAsync(hostPID.Address, name, kind, timeout);

        }

        public async Task<ActorPidResponse> SpawnOnClientHostAsync(string kind, TimeSpan timeout)
        {
            var hostPID = await GetClientHostPID();
            return await SpawnAsync(hostPID.Address, kind, timeout);
        }
        
     
        public void Dispose()
        {

            RootContext.Empty.Send(_clientEndpointManager, new ReleaseClientEndpointReference());
            
        }
        
        
        public static void SendMessage(PID target, object envelope, int serializerId)
        {
           
            var (message, sender, header) = MessageEnvelope.Unwrap(envelope);
            
            
            var env = new RemoteDeliver(header, message, target, sender, serializerId);
            

            RootContext.Empty.Send(_clientEndpointManager, env);

        }
    





   
    }
}