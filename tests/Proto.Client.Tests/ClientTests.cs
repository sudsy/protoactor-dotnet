using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Moq;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Client.Tests
{
//    class mockStream<T> : IAsyncStreamReader<T>
//    {
//        public void Dispose()
//        {
//            throw new NotImplementedException();
//        }
//
//        public Task<bool> MoveNext(CancellationToken cancellationToken)
//        {
//            throw new NotImplementedException();
//        }
//
//        public T Current { get; }
//    }
    
//    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ClientTests
    {
        private static readonly ClientContext Context = new ClientContext("127.0.0.1", 12222, new RemoteConfig());
        private readonly RemoteManager _remoteManager;

//        public ClientTests(RemoteManager remoteManager)
//        {
//            _remoteManager = remoteManager;
//        }

        [Fact, DisplayTestMethodName]
        public async void CanCreateClientProxyActor()
        {
            
            var mockRequestStream = new Mock<IAsyncStreamReader<ClientMessageBatch>>();
            mockRequestStream.SetupSequence(stream => stream.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true))
                .Returns(Task.FromResult(false));
                
            mockRequestStream.SetupGet(stream => stream.Current)
                .Returns(() =>
                {
                    var localPID = new PID();
                    return ClientContext.getClientMessageBatch(null, new CreateClientProxyActor()
                    {
                        ClientPID = localPID
                    });
                    
                });
            var mockResponseStream = new Mock<IServerStreamWriter<ClientMessageBatch>>();
            var fakeServerCallContext = TestServerCallContext.Create("fooMethod", null, DateTime.UtcNow.AddHours(1), new Metadata(), CancellationToken.None, "127.0.0.1", null, null, (metadata) => TaskUtils.CompletedTask, () => new WriteOptions(), (writeOptions) => { });
            var clientEndpointManager = new ClientEndpointManager();
            clientEndpointManager.ConnectClient(mockRequestStream.Object, mockResponseStream.Object,
                fakeServerCallContext);
            
            mockResponseStream.Verify(stream => stream.WriteAsync(It.Is<ClientMessageBatch>(messageBatch => messageBatch.TypeNames.Contains("client.ClientProxyActorCreated"))));
            
            
        } 
        
//        [Fact, DisplayTestMethodName]
//        public async void CanSendJsonAndReceiveToClient()
//        {
//            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
//            var ct = new CancellationTokenSource(3000);
//            var tcs = new TaskCompletionSource<bool>();
//            ct.Token.Register(() =>
//            {
//                tcs.TrySetCanceled();
//            });
//            
//            //This needs to spawn with a connection ID
//            var localActor = Context.Spawn(Props.FromFunc(ctx =>
//            {
//                if (ctx.Message is Pong)
//                {
//                    tcs.SetResult(true);
//                    ctx.Self.Stop();
//                }
//
//                return Actor.Done;
//            }));
//            
//            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
//            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
//            Client.SendMessage(remoteActor, envelope, 1);
//            await tcs.Task;
//        }
    }
}