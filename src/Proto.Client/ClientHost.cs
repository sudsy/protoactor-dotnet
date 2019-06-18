using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;


namespace Proto.Client
{
    public static class ClientHost 
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(ClientHost).FullName);
        
        static ClientHost()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        
        public static RemoteConfig WithClientHost(this RemoteConfig config){
            
            ProcessRegistry.Instance.RegisterHostResolver(pid =>
            {
                Logger.LogDebug($"Testing if {pid} is a client");
                return isClientAddress(pid.Address) ? new ClientProcess(pid) : null;
            });

            var addr = $"{config.AdvertisedHostname}:{config.AdvertisedPort}";

            var clientEndpointManager = new ClientHostEndpointManager(addr);
            config.AdditionalServices = new List<ServerServiceDefinition>
            {
                ClientRemoting.BindService(clientEndpointManager)
            };

            return config;
        }
        
        private static bool isClientAddress(string argAddress)
        {
            return argAddress.StartsWith("client:");
        }
        
        
    }
}