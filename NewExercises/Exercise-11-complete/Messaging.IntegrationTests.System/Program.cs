﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NServiceBus;

namespace Messaging.IntegrationTests.System
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var (endpoint, _) = await StartEndpoint();

            await endpoint.SendLocal(new PlaceOrder());

            Console.WriteLine("Press any <key> to exit.");
            Console.ReadKey();
        }

        public static async Task<(IEndpointInstance, OrderStore)> StartEndpoint(Action<EndpointConfiguration> configure = null)
        {
            var endpointName = "IntegrationTests.Endpoint";
            
            var configuration = new EndpointConfiguration(endpointName);
            
            var transport = configuration.UseTransport<LearningTransport>();
            transport.Routing().RouteToEndpoint(typeof(PlaceOrder), endpointName);

            var orderStore = new OrderStore();
            configuration.RegisterComponents(cc => cc.AddSingleton(orderStore));

            configure?.Invoke(configuration);

            var endpoint = await Endpoint.Start(configuration).ConfigureAwait(false);

            return (endpoint, orderStore);
        }
    }

    public class PlaceOrder : ICommand
    {
        public Guid Id { get; set; }
    }

    public class FinalizeOrder : ICommand
    {
        public Guid Id { get; set; }
    }

    public class OrderStore
    {
        public List<Guid> PlacedOrders = new List<Guid>();
    }

    public class PlaceOrderHandler : IHandleMessages<PlaceOrder>,
        IHandleMessages<FinalizeOrder>
    {
        readonly OrderStore store;

        public PlaceOrderHandler(OrderStore store)
        {
            this.store = store;
        }

        public async Task Handle(PlaceOrder message, IMessageHandlerContext context)
        {
            Console.WriteLine("Order received");

            var options = new SendOptions();
            options.DelayDeliveryWith(TimeSpan.FromSeconds(5));
            options.RouteReplyToThisInstance();

            await context.SendLocal(new FinalizeOrder{Id = message.Id});
        }

        public Task Handle(FinalizeOrder message, IMessageHandlerContext context)
        {
            Console.WriteLine("Finalizing order");

            store.PlacedOrders.Add(message.Id);

            return Task.CompletedTask;
        }
    }
}
