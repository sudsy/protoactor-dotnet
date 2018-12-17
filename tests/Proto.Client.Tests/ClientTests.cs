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

    public class ClientTests
    {
      

        [Fact, DisplayTestMethodName]
        public async void CanCreateClientProxyActor()
        {
            
            /*
            var mockRequestStream = new Mock<IAsyncStreamReader<ClientMessageBatch>>();
            mockRequestStream.SetupSequence(stream => stream.MoveNext(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true))
                .Returns(Task.FromResult(false));
                
            mockRequestStream.SetupGet(stream => stream.Current)
                .Returns(() =>
                {
                    var localPID = new PID();
                    return Client.getClientMessageBatch(null, new ProxyPidRequest()
                    {
                        ClientPID = localPID
                    }, Serialization.DefaultSerializerId);
                    
                });
            var mockResponseStream = new Mock<IServerStreamWriter<ClientMessageBatch>>();
            var fakeServerCallContext = TestServerCallContext.Create("fooMethod", null, DateTime.UtcNow.AddHours(1), new Metadata(), CancellationToken.None, "127.0.0.1", null, null, (metadata) => TaskUtils.CompletedTask, () => new WriteOptions(), (writeOptions) => { });
            var clientEndpointManager = new ClientEndpointManager("");
            clientEndpointManager.ConnectClient(mockRequestStream.Object, mockResponseStream.Object,
                fakeServerCallContext);
            
            mockResponseStream.Verify(stream => stream.WriteAsync(It.Is<ClientMessageBatch>(messageBatch => messageBatch.TypeNames.Contains("client.ClientProxyActorCreated"))));
            */

            
        } 
  
    }
}