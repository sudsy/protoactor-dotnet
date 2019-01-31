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

            var clientEndpointManager = new ClientEndpointManager($"{hostname}:{port}");
            config.AdditionalServices = new List<ServerServiceDefinition>
            {
                ClientRemoting.BindService(clientEndpointManager)
            };
            
            Remote.Remote.Start(hostname, port, config);
            
            
        }
        
        
        
        
        
    }
}