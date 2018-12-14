using System.Linq;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Proto.Remote;


namespace Proto.Client
{
    public class ClientProxy 
    {
        private static Server _server;

        public static void Start(string hostname, int port)
        {
            Start(hostname, port, new RemoteConfig());
        }
        
        public static void Start(string hostname, int port, RemoteConfig config)
        {
//            RemoteConfig = config;

            
            //Change this to client manager
//            EndpointManager.Start();
//            _endpointReader = new EndpointReader();
            var clientEndpointManager = new ClientEndpointManager();
            _server = new Server
            {
                Services = { ClientRemoting.BindService(clientEndpointManager) },
                Ports = { new ServerPort(hostname, port, config.ServerCredentials) }
            };
            _server.Start();

          

            

            
        }
        
        public static void SendMessage(PID pid, object msg)
        {
            

//            var env = new RemoteDeliver(header, message, pid, sender, serializerId);
//            EndpointManager.RemoteDeliver(env);
        }
    }
}