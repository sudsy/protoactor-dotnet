using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;
using Proto.Remote.Tests;
using Proto.Remote.Tests.Messages;
using Xunit;

namespace Proto.Client.Tests
{
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ClientTests
    {
        private static readonly ClientContext Context = new ClientContext("127.0.0.1", 12222, new RemoteConfig());
        private readonly RemoteManager _remoteManager;

        public ClientTests(RemoteManager remoteManager)
        {
            _remoteManager = remoteManager;
        }
        
        [Fact, DisplayTestMethodName]
        public async void CanSendJsonAndReceiveToClient()
        {
            var remoteActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var ct = new CancellationTokenSource(3000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            //This needs to spawn with a connection ID
            var localActor = Context.Spawn(Props.FromFunc(ctx =>
            {
                if (ctx.Message is Pong)
                {
                    tcs.SetResult(true);
                    ctx.Self.Stop();
                }

                return Actor.Done;
            }));
            
            var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
            var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
            Client.SendMessage(remoteActor, envelope, 1);
            await tcs.Task;
        }
    }
}