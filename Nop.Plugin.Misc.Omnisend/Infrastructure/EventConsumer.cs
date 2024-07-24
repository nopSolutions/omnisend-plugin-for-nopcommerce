using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Core.Events;
using Nop.Plugin.Misc.Omnisend.Services;
using Nop.Services.Common;
using Nop.Services.Events;
using Nop.Web.Framework.Events;

namespace Nop.Plugin.Misc.Omnisend.Infrastructure
{
    /// <summary>
    /// Represents plugin event consumer
    /// </summary>
    internal class EventConsumer : IConsumer<CustomerLoggedinEvent>,
        IConsumer<CustomerRegisteredEvent>,
        IConsumer<EmailSubscribedEvent>,
        IConsumer<EmailUnsubscribedEvent>,
        IConsumer<EntityDeletedEvent<ProductAttributeCombination>>,
        IConsumer<EntityDeletedEvent<ShoppingCartItem>>,
        IConsumer<EntityUpdatedEvent<Product>>,
        IConsumer<EntityInsertedEvent<ProductAttributeCombination>>,
        IConsumer<EntityInsertedEvent<ShoppingCartItem>>,
        IConsumer<EntityInsertedEvent<StockQuantityHistory>>,
        IConsumer<EntityUpdatedEvent<ShoppingCartItem>>,
        IConsumer<EntityInsertedEvent<OrderItem>>,
        IConsumer<OrderAuthorizedEvent>,
        IConsumer<OrderPaidEvent>,
        IConsumer<OrderPlacedEvent>,
        IConsumer<OrderRefundedEvent>,
        IConsumer<OrderCancelledEvent>,
        IConsumer<EntityUpdatedEvent<Order>>,
        IConsumer<OrderVoidedEvent>,
        IConsumer<PageRenderingEvent>
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly OmnisendEventsService _omnisendEventsService;
        private readonly OmnisendService _omnisendService;
        private readonly OmnisendSettings _settings;

        #endregion

        #region Ctor

        public EventConsumer(IGenericAttributeService genericAttributeService,
            OmnisendEventsService omnisendEventsService,
            OmnisendService omnisendService,
            OmnisendSettings settings)
        {
            _genericAttributeService = genericAttributeService;
            _omnisendEventsService = omnisendEventsService;
            _omnisendService = omnisendService;
            _settings = settings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        public void HandleEvent(CustomerLoggedinEvent eventMessage)
        {
            if (string.IsNullOrEmpty(_settings.IdentifyContactScript))
                return;

            var script = _settings.IdentifyContactScript.Replace(OmnisendDefaults.Email, eventMessage.Customer.Email);

            _genericAttributeService.SaveAttribute(eventMessage.Customer,
                OmnisendDefaults.IdentifyContactAttribute, script);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        public void HandleEvent(CustomerRegisteredEvent eventMessage)
        {
            _omnisendService.UpdateContact(eventMessage.Customer);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EmailSubscribedEvent eventMessage)
        {
            _omnisendService.UpdateOrCreateContact(eventMessage.Subscription, true);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EmailUnsubscribedEvent eventMessage)
        {
            _omnisendService.UpdateOrCreateContact(eventMessage.Subscription);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityInsertedEvent<StockQuantityHistory> eventMessage)
        {
            var history = eventMessage.Entity;

            _omnisendService.UpdateProduct(history.ProductId);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityInsertedEvent<ProductAttributeCombination> eventMessage)
        {
            _omnisendService.UpdateProduct(eventMessage.Entity.ProductId);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityDeletedEvent<ProductAttributeCombination> eventMessage)
        {
            _omnisendService.UpdateProduct(eventMessage.Entity.ProductId);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityInsertedEvent<ShoppingCartItem> eventMessage)
        {
            var entity = eventMessage.Entity;

            if (entity.ShoppingCartType != ShoppingCartType.ShoppingCart)
                return;

            _omnisendEventsService.SendAddedProductToCartEvent(entity);
            //await _omnisendService.AddShoppingCartItemAsync(eventMessage.Entity);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(OrderPlacedEvent eventMessage)
        {
            _omnisendEventsService.SendOrderPlacedEvent(eventMessage.Order);
            _omnisendService.PlaceOrder(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(OrderPaidEvent eventMessage)
        {
            _omnisendEventsService.SendOrderPaidEvent(eventMessage);
            //await _omnisendService.UpdateOrderAsync(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(OrderRefundedEvent eventMessage)
        {
            _omnisendEventsService.SendOrderRefundedEvent(eventMessage);
            //await _omnisendService.UpdateOrderAsync(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityUpdatedEvent<Order> eventMessage)
        {
            _omnisendEventsService.SendOrderStatusChangedEvent(eventMessage.Entity);
            //await _omnisendService.UpdateOrderAsync(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(PageRenderingEvent eventMessage)
        {
            _omnisendEventsService.SendStartedCheckoutEvent(eventMessage);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityUpdatedEvent<ShoppingCartItem> eventMessage)
        {
            //await _omnisendService.EditShoppingCartItemAsync(eventMessage.Entity);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityDeletedEvent<ShoppingCartItem> eventMessage)
        {
            _omnisendService.DeleteShoppingCartItem(eventMessage.Entity);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(OrderAuthorizedEvent eventMessage)
        {
            //await _omnisendService.UpdateOrderAsync(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(OrderVoidedEvent eventMessage)
        {
            //await _omnisendService.UpdateOrderAsync(eventMessage.Order);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityInsertedEvent<OrderItem> eventMessage)
        {
            _omnisendService.OrderItemAdded(eventMessage.Entity);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        /// <returns>A task that represents the asynchronous operation</returns>
        public void HandleEvent(EntityUpdatedEvent<Product> eventMessage)
        {
            _omnisendService.CreateOrUpdateProduct(eventMessage.Entity);
        }

        /// <summary>
        /// Handle event
        /// </summary>
        /// <param name="eventMessage">Event</param>
        public void HandleEvent(OrderCancelledEvent eventMessage)
        {
            _omnisendEventsService.SendOrderStatusChangedEvent(eventMessage.Order);
        }

        #endregion
    }
}
