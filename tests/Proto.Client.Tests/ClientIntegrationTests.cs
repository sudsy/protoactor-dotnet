using System;
using System.Collections.Generic;
using System.Data;
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

    
    [Collection("RemoteTests"), Trait("Category", "Remote")]
    public class ClientIntegrationTests
    {
        
       
        private readonly RemoteManager _remoteManager;
        

        public ClientIntegrationTests(RemoteManager remoteManager)
        {
            Client.Start("127.0.0.1", 12222, new RemoteConfig());
            _remoteManager = remoteManager;
        }

        
        
        [Fact, DisplayTestMethodName]
        public async void CanSendJsonAndReceiveToClient()
        {
            
            
            var clientHostActor = new PID(_remoteManager.DefaultNode.Address, "EchoActorInstance");
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            //This needs to spawn with a connection ID
            var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
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
            
            Client.SendMessage(clientHostActor, envelope, 1);
            await tcs.Task;
        }

        [Fact, DisplayTestMethodName]
        public async void CanRequestAsyncToRemoteFromClientActor()
        {
            
            
            var remoteActor = new PID(_remoteManager.RemoteNode.Address, "EchoActorInstance");
            
            var ct = new CancellationTokenSource(30000);
            var tcs = new TaskCompletionSource<bool>();
            ct.Token.Register(() =>
            {
                tcs.TrySetCanceled();
            });
            
            //This needs to spawn with a connection ID
            var proxyActor = RootContext.Empty.Spawn(Props.FromFunc(async ctx =>
            {
                if (ctx.Message is Started)
                {
                   
                    var result = await ctx.RequestAsync<Pong>(remoteActor, new Ping()
                    {
                        Message = "Hello to remote from inside"
                    });
                    tcs.SetResult(true);
                    ctx.Self.Stop();
                }
                
                
            }));
            
           
            await tcs.Task;
        }
    }
}