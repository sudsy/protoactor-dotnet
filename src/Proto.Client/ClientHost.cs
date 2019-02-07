using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;


namespace Proto.Client
{
    public class ClientHost 
    {
        private static readonly ILogger Logger = Log.CreateLogger(typeof(ClientHost).FullName);
        
        static ClientHost()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        public static void Start(string hostname, int port)
        {
            Start(hostname, port, new RemoteConfig());
        }
        
        public static void Start(string hostname, int port, RemoteConfig config)
        {
            Logger.LogDebug($"Starting Client Host");
            var addr = $"{config.AdvertisedHostname??hostname}:{config.AdvertisedPort?? port}";
//            ProcessRegistry.Instance.Address = addr;
            var clientEndpointManager = new ClientEndpointManager(addr);
            config.AdditionalServices = new List<ServerServiceDefinition>
            {
                ClientRemoting.BindService(clientEndpointManager)
            };
            
            Logger.LogDebug($"Starting Remote with Client Host {addr}");
            Remote.Remote.Start(hostname, port, config);
            
            
        }
        
        
        
        
        
    }
}