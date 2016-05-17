using System;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using System.Threading;
using System.Threading.Tasks;
using Nitra.ClientServer.Client;
using Nitra.ClientServer.Messages;
using ReactiveUI;

namespace Nitra.Visualizer.Infrastructure
{
  public class NitraClientAsync
  {
    private readonly NitraClient _nitraClient;
    private readonly ISubject<MessageWithId<ClientMessage>, MessageWithId<ClientMessage>> _syncedOutbox;
    private readonly IObservable<MessageWithId<ServerMessage>> _inbox;
    private int _messageId;

    public NitraClientAsync()
    {
      _nitraClient = new NitraClient(new StringManager());

      var subject = new Subject<MessageWithId<ClientMessage>>();

      // Synchronize makes sure that all messages are processed one after another
      _syncedOutbox = Subject.Synchronize(subject);

      // Received messages are processed by SendReceive if there is someone waiting for a result
      // SendReceive is called on TaskPool
      _inbox = _syncedOutbox.ObserveOn(RxApp.TaskpoolScheduler)
                            .Select(SendReceive)
                            .Publish()
                            .RefCount();
    }

    private MessageWithId<ServerMessage> SendReceive(MessageWithId<ClientMessage> msg)
    {
      _nitraClient.Send(msg.Message);

      return new MessageWithId<ServerMessage> {
        Id = msg.Id,
        Message = _nitraClient.Receive<ServerMessage>()
      };
    }

    public Task<T> Request<T>(ClientMessage message) where T : ServerMessage
    {
      var id = Interlocked.Increment(ref _messageId);

      // Wait for reply with the same message ID
      // Take 1 element and unsubscribe
      // If no response is received in 5 seconds, throw TimeoutException
      var result = _inbox.Where(msg => msg.Id == id)
                         .Select(msg => msg.Message)
                         .OfType<T>()
                         .Take(1)
                         .Timeout(TimeSpan.FromSeconds(5))
                         .ToTask();

      // Post message after subscription
      // We don't send messages unless someone is listening for replies
      _syncedOutbox.OnNext(new MessageWithId<ClientMessage> {
        Id = id,
        Message = message
      });

      return result;
    }

    class MessageWithId<T>
    {
      public T Message { get; set; }
      public int Id { get; set; }
    }
  }
}