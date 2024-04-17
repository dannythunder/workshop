﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Messages;
using Messaging.IntegrationTests.Tests;
using NServiceBus;
using NUnit.Framework;

namespace Tests
{
    public class IntegrationTests
    {
        IEndpointInstance ordersEndpoint;
        Repository ordersRepository;

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

            await tracer.Start();
        }

        [TearDown]
        public async Task CleanUp()
        {
            await ordersEndpoint.Stop();
            await tracer.Stop();
        }

        [Test]
        public async Task ChangeStatus()
        {
            var cartId = Guid.NewGuid().ToString();
            var customerId = Guid.NewGuid().ToString();

            var submitOrder = new SubmitOrder {CartId = cartId, Customer = customerId, Items = new List<Filling>()};

            var bookPayment = new BookPayment {Id = Guid.NewGuid(), CartId = cartId, Customer = customerId};

            var cancelPayment = new CancelPayment {Id = Guid.NewGuid(), CartId = cartId, Customer = customerId};

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
    }
}