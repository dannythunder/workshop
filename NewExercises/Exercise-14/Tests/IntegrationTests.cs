using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marketing;
using Messages;
using Messaging.IntegrationTests.Tests;
using NServiceBus;
using NServiceBus.Pipeline;
using NUnit.Framework;

namespace Tests
{
    public class IntegrationTests
    {
        IEndpointInstance ordersEndpoint;
        IEndpointInstance marketingEndpoint;
        Repository ordersRepository;
        Repository marketingRepository;

        Tracer tracer = new Tracer();

        [SetUp]
        public async Task Setup()
        {
            Console.SetOut(TestContext.Progress);

            (ordersEndpoint, ordersRepository) = await Orders.Program.Start((c, r) =>
            {
                c.AssemblyScanner().ExcludeAssemblies("Tests.dll", "Marketing.exe");

                c.Pipeline.Register(new TracingBehavior(), "Trace input and output messages.");

                r.RouteToEndpoint(typeof(SubmitOrder), "Orders");
                r.RouteToEndpoint(typeof(BookPayment), "Orders");
                r.RouteToEndpoint(typeof(CancelPayment), "Orders");
            });

            (marketingEndpoint, marketingRepository) = await Marketing.Program.Start((c, r) =>
            {
                c.AssemblyScanner().ExcludeAssemblies("Tests.dll", "Orders.exe");

                c.Pipeline.Register(new TracingBehavior(), "Trace input and output messages.");
                c.Pipeline.Register(new ImmediateTracingBehavior(), "Trace immediate output messages.");

                c.Pipeline.Register(new DropMessagesBehavior(), "Drops selected messages");
            });

            await tracer.Start();
        }

        [TearDown]
        public async Task CleanUp()
        {
            await ordersEndpoint.Stop();
            await marketingEndpoint.Stop();
            await tracer.Stop();
        }

        [Test]
        public async Task IssueCouponCardAfterFirst100USDSpent()
        {
            var firstCartId = Guid.NewGuid().ToString();
            var secondCartId = Guid.NewGuid().ToString();
            var customerId = Guid.NewGuid().ToString();

            var submitFirstOrder = new SubmitOrder
            {
                CartId = firstCartId,
                Customer = customerId,
                Items = new List<Filling>
                {
                    Filling.Meat,
                    Filling.Mushrooms,
                    Filling.QuarkAndPotatoes
                }
            };

            var submitSecondOrder = new SubmitOrder
            {
                CartId = secondCartId,
                Customer = customerId,
                Items = new List<Filling>
                {
                    Filling.Strawberry,
                    Filling.SwissCheese
                }
            };

            var bookFirstPayment = new BookPayment { Id = Guid.NewGuid(), CartId = firstCartId, Customer = customerId };
            var bookSecondPayment = new BookPayment { Id = Guid.NewGuid(), CartId = secondCartId, Customer = customerId };

            await SendInOrder(new IMessage[] {
                    //TODO: Define message sequence
                }
            );

            var (_, version) = await marketingRepository.Get<Coupon>(customerId, Coupon.RowId);

            Assert.IsNotNull(version);
        }

        [Test]
        public async Task TrackTotalPaymentsValue()
        {
            var cartId = Guid.NewGuid().ToString();
            var customerId = Guid.NewGuid().ToString();

            var submitOrder = new SubmitOrder
            {
                CartId = cartId,
                Customer = customerId,
                Items = new List<Filling>
                {
                    Filling.Meat,
                    Filling.Mushrooms,
                    Filling.QuarkAndPotatoes
                }
            };

            var bookPayment = new BookPayment { Id = Guid.NewGuid(), CartId = cartId, Customer = customerId };

            var cancelPayment = new CancelPayment { Id = Guid.NewGuid(), CartId = cartId, Customer = customerId };

            await SendInOrder(new IMessage[]
                {
                    submitOrder,
                    bookPayment,
                    cancelPayment,
                    bookPayment
                }
            );

            var (payment, _) = await marketingRepository.Get<Payments>(customerId, Payments.RowId);

            Assert.AreEqual(0, payment.TotalValue);
        }

        [Test]
        public async Task ChangeStatus()
        {
            var cartId = Guid.NewGuid().ToString();
            var customerId = Guid.NewGuid().ToString();

            var submitOrder = new SubmitOrder { CartId = cartId, Customer = customerId, Items = new List<Filling>() };

            var bookPayment = new BookPayment { Id = Guid.NewGuid(), CartId = cartId, Customer = customerId };

            var cancelPayment = new CancelPayment { Id = Guid.NewGuid(), CartId = cartId, Customer = customerId };

            await SendInOrder(new IMessage[]
                {
                    submitOrder,
                    bookPayment,
                    cancelPayment,
                    bookPayment
                }
            );

            var (order, _) = await ordersRepository.Get<Order>(customerId, cartId);

            Assert.AreEqual(order.PaymentBooked, false);
        }

        async Task SendInOrder(IMessage[] messages)
        {
            foreach (var message in messages)
            {
                var (conversationId, options) = tracer.Prepare();

                await ordersEndpoint.Send(message, options);

                await tracer.WaitUntilFinished(conversationId);
            }
        }

        public class DropMessagesBehavior : Behavior<IOutgoingLogicalMessageContext>
        {
            public override async Task Invoke(IOutgoingLogicalMessageContext context, Func<Task> next)
            {
                await next();
            }
        }
    }
}