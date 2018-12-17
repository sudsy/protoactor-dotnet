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
            var server = new Server
            {
                Services = { ClientRemoting.BindService(clientEndpointManager) },
                Ports = { new ServerPort(hostname, port, config.ServerCredentials) }
            };
            
           
            
            //We need a test to make sure that normal remote is started - maybe test to see if the regular activator is registered?
            
            server.Start();

            
        }
        
        
        
        
        
    }
}