using System;
using System.Threading.Tasks;
using Microsoft.Extensions.CommandLineUtils;
using Proto.Client;
using Proto.Remote.Tests.Messages;

namespace Proto.Remote.Tests.Node
{
    class Program
    {
        static void Main(string[] args)
        {
            var context = new RootContext();
            var app = new CommandLineApplication();
            var hostOption = app.Option("-h|--host", "host", CommandOptionType.SingleValue);
            var portArgument = app.Option("-p|--port", "port", CommandOptionType.SingleValue);
            var clientProxyPortArgument = app.Option("-c|--clientport", "port", CommandOptionType.SingleValue);

            app.OnExecute(() => {
                var host = hostOption.Value() ?? "127.0.0.1";
                var portString = portArgument.Value() ?? "12000";
                int port = 12000;
               
                if (!string.IsNullOrWhiteSpace(portString))
                {
                    int.TryParse(portString, out port);
                }

                Serialization.RegisterFileDescriptor(Messages.ProtosReflection.Descriptor);
                Remote.Start(host, port);

                
                if (clientProxyPortArgument.HasValue())
                {
                    Console.WriteLine("Starting Client Proxy");
                    ClientHost.Start("127.0.0.1", Convert.ToInt32(clientProxyPortArgument.Values[0]));    
                }
                
                
                var props = Props.FromProducer(() => new EchoActor(host, port));
                Remote.RegisterKnownKind("EchoActor", props);
                context.SpawnNamed(props, "EchoActorInstance");
                Console.ReadLine();
                return 0;
            });

            app.Execute(args);
        }
    }

    public class EchoActor : IActor
    {
        private readonly string _host;
        private readonly int _port;

        public EchoActor(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public Task ReceiveAsync(IContext context)
        {
            Console.WriteLine($"Echo Actor Received - {context.Message} from {context.Sender}");
            switch (context.Message)
            {
                case Ping ping:
                    try
                    {
                        context.Respond(new Pong {Message = $"{_host}:{_port} {ping.Message}"});
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                   
                    return Actor.Done;
                default:
                    return Actor.Done;
            }
        }
    }
}
