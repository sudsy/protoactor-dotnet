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
            
            
            var proxyActivator = SpawnProxyActivator(responseStream);
            //Read any messages we receive from the client
            await requestStream.ForEachAsync(clientMessageBatch =>
            {
                var targetAddress = clientMessageBatch.Address;
                
                foreach (var envelope in clientMessageBatch.Batch.Envelopes)
                {
                    
                    var message = Serialization.Deserialize(clientMessageBatch.Batch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);
                    
                    
                    var target = new PID(targetAddress, clientMessageBatch.Batch.TargetNames[envelope.Target]); 
                    
                    if (target.Id == "proxy_activator")
                    {
                        target = proxyActivator;
                    }
                    
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
                   
                    var forwardingEnvelope = new Proto.MessageEnvelope(message, envelope.Sender, header);
                    if (target.Address.Equals(ProcessRegistry.Instance.Address))
                    {
                        RootContext.Empty.Send(target, forwardingEnvelope);
                    }
                    else
                    {
                        //todo: We could have forwarded this batch without deserializing contents if we had access to SendEnvelopesAsync from EndPointWriter
                        //todo: If we use a naming prefix for proxies, we could tell if we are sending to another client proxy and avoid deserialization there too
                        Remote.Remote.SendMessage(target, forwardingEnvelope, Serialization.DefaultSerializerId);
                    }
                   
                   
                   
                    
                }

                return Task.CompletedTask;

            });
        }
        
        
        private static PID SpawnProxyActivator(IServerStreamWriter<MessageBatch> responseStream)
        {
            
            //TOD, make one of these supervise the other so we can shutdown the whole tree if the connection dies
            var endpointWriter = RootContext.Empty.Spawn(Props.FromProducer(() => new ClientHostEndpointWriter(responseStream)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy));
            
            
            var props = Props.FromProducer(() => new ProxyActivator(endpointWriter)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            return RootContext.Empty.Spawn(props);
        }

        
    }
}