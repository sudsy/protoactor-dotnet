using System.Threading;
using System.Threading.Tasks;
using Divergic.Logging.Xunit;
using Microsoft.Extensions.Logging;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Remote.Tests
{
    public class ConnectionFailTests
    {
        public ConnectionFailTests(ITestOutputHelper testOutputHelper){
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);

            

        }
        // [Fact, DisplayTestMethodName]
        // public async Task CanRecoverFromConnectionFailureAsync()
        // {

        //     var logger = Log.CreateLogger("ConnectionFail");
            
        //     var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
        //     var ct = new CancellationTokenSource(30000);
        //     var tcs = new TaskCompletionSource<bool>();
        //     var receivedTerminationTCS = new TaskCompletionSource<bool>();
        //     ct.Token.Register(() =>
        //     {
        //         tcs.TrySetCanceled();
        //         receivedTerminationTCS.TrySetCanceled();
        //     });
        //     var endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(termEvent => {
        //         receivedTerminationTCS.TrySetResult(true);
        //     });

        //     Remote.Start("127.0.0.1", 12001);

        //     var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
        //     {
                
        //         if (ctx.Message is Pong)
        //         {
        //             tcs.SetResult(true);
        //             ctx.Stop(ctx.Self);
        //         }

        //         return Actor.Done;
        //     }));
            
        //     var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
        //     var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
        //     Remote.SendMessage(remoteActor, envelope, 1);
        //     logger.LogDebug("sent message");
        //     // await Task.Delay(3000);
        //     logger.LogDebug("starting remote manager");
        //     using(var remoteService = new RemoteManager(false)){
        //         logger.LogDebug("awaiting completion");
        //         await tcs.Task;
        //     }
            
        //     Remote.Shutdown(true);
        //     await receivedTerminationTCS.Task;
        // }


        // [Fact, DisplayTestMethodName]
        // public async Task CanDealWithConnectionFailureGracefully()
        // {

        //     var logger = Log.CreateLogger("ConnectionFail");
        //     Remote.Start("127.0.0.1", 12001);

        //     var remoteActor = new PID("127.0.0.1:12000", "EchoActorInstance");
        //     var ct = new CancellationTokenSource(30000);
        //     var receivedPongTCS = new TaskCompletionSource<bool>();
        //     var receivedTerminationTCS = new TaskCompletionSource<bool>();
        //     var receivedSecondPongTCS = new TaskCompletionSource<bool>();
        //     ct.Token.Register(() =>
        //     {
        //         receivedPongTCS.TrySetCanceled();
        //         receivedTerminationTCS.TrySetCanceled();
        //         receivedSecondPongTCS.TrySetCanceled();
        //     });
        //     var endpointTermEvnSub = EventStream.Instance.Subscribe<EndpointTerminatedEvent>(termEvent => {
        //         receivedTerminationTCS.TrySetResult(true);
        //     });
            
        //     logger.LogDebug("starting remote manager");


        //     using(var remoteService = new RemoteManager(false)){

        //         var localActor = RootContext.Empty.Spawn(Props.FromFunc(ctx =>
        //         {
                    
        //             if (ctx.Message is Pong)
        //             {
        //                 if(!receivedPongTCS.Task.IsCompleted){
        //                     receivedPongTCS.TrySetResult(true);
        //                 }else{
        //                     receivedSecondPongTCS.TrySetResult(true);
        //                 }
                        
        //                 ctx.Stop(ctx.Self);
        //             }

        //             return Actor.Done;
        //         }));
                
        //         var json = new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}");
        //         var envelope = new Proto.MessageEnvelope(json, localActor, Proto.MessageHeader.Empty);
        //         Remote.SendMessage(remoteActor, envelope, 1);
        //         logger.LogDebug("sent message");
        //         // await Task.Delay(3000);
        //         await receivedPongTCS.Task;
            
                
        //         //Maybe await something in the event stream to say that the endpoint is shut down
        //     }
        //     await Task.Delay(3000);
        //     //Remote should be shut down by now

        //     logger.LogDebug("sending second message");
                
        //     Remote.SendMessage(remoteActor, new JsonMessage("remote_test_messages.Ping", "{ \"message\":\"Hello\"}"), 1);

            
        //     var endpointConnectedEvnSub = EventStream.Instance.Subscribe<EndpointConnectedEvent>(termEvent => {
        //         receivedSecondPongTCS.TrySetResult(true);
        //     });
        //     await receivedTerminationTCS.Task;

        //     EventStream.Instance.Unsubscribe(endpointTermEvnSub.Id);

        //     //Try a restart

        //     using(var remoteService = new RemoteManager(false)){
        //         await receivedSecondPongTCS.Task;
        //     }
            
        //     Remote.Shutdown(true);

        // }

        
    }
}