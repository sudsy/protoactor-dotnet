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
        public ClientEndpointManager()
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
        }
        
        private Dictionary<PID, PID> mappingTable = new Dictionary<PID, PID>();
        public override async Task ConnectClient(IAsyncStreamReader<ClientMessageBatch> requestStream,
            IServerStreamWriter<ClientMessageBatch> responseStream, ServerCallContext context)
        {
            Console.WriteLine("connected");
            //Read any messages we receive from the client
            await requestStream.ForEachAsync(async clientMessageBatch =>
            {
                foreach (var envelope in clientMessageBatch.Envelopes)
                {
                    Console.WriteLine("message received");
                    var message = Serialization.Deserialize(clientMessageBatch.TypeNames[envelope.TypeId], envelope.MessageData, envelope.SerializerId);
                    Console.WriteLine("message deserialized");
                    
                    if (envelope.Target.Equals(-1))
                    {
                        //We are calling this a system message for the moment
                        if (message is CreateClientProxyActor createClientMessage)
                        {
                            var props =
                                Props.FromProducer(() => new ClientProxyActor(envelope.Sender, responseStream)); //TODO: fix this to take an actor as a refence to ensure we write to stream in a thread safe way
                            var clientProxyActorPID = RootContext.Empty.Spawn(props);
                            //Send a return message with the proxy id contained within
                            Console.WriteLine("Sending created message");
                            //todo: move this inside the actor started hook
                            await responseStream.WriteAsync(ClientContext.getClientMessageBatch(createClientMessage.ClientPID,
                                new ClientProxyActorCreated()
                                {
                                    ClientPID = createClientMessage.ClientPID,
                                    ProxyPID = clientProxyActorPID
                                }, Serialization.DefaultSerializerId));
                        }
                        
                    }
                    else
                    {
                        var target = clientMessageBatch.TargetPids[envelope.Target]; //There is a logic problem with this, it's hard to define null - maybe need a null pid
                        //Forward the message to the correct endpoint
                        Console.WriteLine($"About to forward message from client to {target}");
                        RootContext.Empty.Send(target, message);
                        Console.WriteLine("Message forwarded");
                    }
                    
                }
                
               
            });
        }
    }
}