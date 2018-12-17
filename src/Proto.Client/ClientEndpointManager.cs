using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Utils;
using Proto.Remote;

namespace Proto.Client
{
    public class ClientEndpointManager: ClientRemoting.ClientRemotingBase
    {
        public ClientEndpointManager(string clientHostAddress)
        {
            _clientHostAddress = clientHostAddress;
            
        }
        
        
        private readonly string _clientHostAddress;

        public override async Task ConnectClient(IAsyncStreamReader<ClientMessageBatch> requestStream,
            IServerStreamWriter<MessageBatch> responseStream, ServerCallContext context)
        {
            Console.WriteLine("connected");
            
            SpawnProxyActivator(responseStream);
            //Read any messages we receive from the client
            await requestStream.ForEachAsync(clientMessageBatch =>
            {
                var targetAddress = clientMessageBatch.Address;
                
                foreach (var envelope in clientMessageBatch.Batch.Envelopes)
                {
                    Console.WriteLine("message received");
                    var message = Serialization.Deserialize(clientMessageBatch.Batch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);
                    Console.WriteLine("message deserialized");
                    
                    
                    var target = new PID(targetAddress, clientMessageBatch.Batch.TargetNames[envelope.Target]); 
                    if (target.Address == _clientHostAddress)
                    {
                        //Remap host address from the client hosting port to the proper Remote Port
                        target.Address = ProcessRegistry.Instance.Address;
                    }
                    
                    //Forward the message to the correct endpoint
                    Proto.MessageHeader header = null;
                    if (envelope.MessageHeader != null)
                    {
                        header = new Proto.MessageHeader(envelope.MessageHeader.HeaderData);
                    }
                    Console.WriteLine($"About to forward message from client to {target} from {envelope.Sender}");
                    var localEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
                    RootContext.Empty.Send(target, localEnvelope);
                    Console.WriteLine("Message forwarded");
                   
                    
                }

                return Task.CompletedTask;

            });
        }
        
        
        private static void SpawnProxyActivator(IServerStreamWriter<MessageBatch> responseStream)
        {
            var endpointWriter = RootContext.Empty.Spawn(Props.FromProducer(() => new ClientHostEndpointWriter(responseStream)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy));
            
            var props = Props.FromProducer(() => new ProxyActivator(endpointWriter)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            RootContext.Empty.SpawnNamed(props, "proxy_activator");
        }

        
    }
}