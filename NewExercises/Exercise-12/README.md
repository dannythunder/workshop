NOTE: This and couple of following exercises use automated tests for show how our system behaves in various scenarios that might happen in messaging systems.

Our system has been extended with new functionality. After order has been placed, we can book a payment for a given order or cancel a payment that has already been booked. So far the deduplication mechanism depended on the idempotence of the operations conducted in the message handling logic. If we mark the payment as booked, we can do it multiple times and the result is the same as if we marked it once. Similarly, when we mark the payment as non-booked, we can repat the operation multiple times and still the payment stays non-booked.

Let's see what happens when some of these messages get reordered:

* Open `IntegrationTests.cs` in the `Test` project and naviage to `ChangeStatus` test
* Use `SendInOrder` utility method to simulate scenario in which oder is placed, payment is booked and later cancelled but the `BookPayment` command in duplicated and the duplicate arrives as the last message:
```csharp
await SendInOrder(new IMessage[]
  {
      submitOrder,
      bookPayment,
      cancelPayment,
      bookPayment
  }
);
``` 
* Run `ChangeStatus` test and check if the assertion holds. If not then can you tell what is wrong?
* Add `List<Guid>` property to `Order` enity called `ProcessedMessages`
```csharp
 public List<Guid> ProcessedMessages { get; set; } = new List<Guid>();
```
* Go to the `BookPaymentHandler` and `CancelPaymentHandler` and modify the deduplication logic to use message Ids. At the end of the message processing in the handler, but before the `Order` is persisted, include the Id of the message being processed in the `ProcessedMessages` collection. At the beginning of the handler code check if the message already exists in the collection. If so, return without doing anything in the handler.

What aspect of a message handler is missing in this exercise?
