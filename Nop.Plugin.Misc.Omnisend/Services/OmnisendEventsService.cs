using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Misc.Omnisend.DTO;
using Nop.Plugin.Misc.Omnisend.DTO.Events;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Discounts;
using Nop.Services.Localization;
using Nop.Services.Media;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Shipping;
using Nop.Services.Tax;
using Nop.Web.Framework.Events;

namespace Nop.Plugin.Misc.Omnisend.Services
{
    /// <summary>
    /// Events based Platform integration service
    /// </summary>
    public class OmnisendEventsService
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IAddressService _addressService;
        private readonly ICategoryService _categoryService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IDiscountService _discountService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IManufacturerService _manufacturerService;
        private readonly IMeasureService _measureService;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPictureService _pictureService;
        private readonly IProductService _productService;
        private readonly IProductTagService _productTagService;
        private readonly IShipmentService _shipmentService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly ITaxService _taxService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OmnisendCustomerService _omnisendCustomerService;
        private readonly OmnisendHelper _omnisendHelper;
        private readonly OmnisendHttpClient _omnisendHttpClient;

        #endregion

        #region Ctor

        public OmnisendEventsService(IActionContextAccessor actionContextAccessor,
            IAddressService addressService,
            ICategoryService categoryService,
            ICountryService countryService,
            ICustomerService customerService,
            IDiscountService discountService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IManufacturerService manufacturerService,
            IMeasureService measureService,
            IOrderService orderService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IPaymentPluginManager paymentPluginManager,
            IPictureService pictureService,
            IProductService productService,
            IProductTagService productTagService,
            IShipmentService shipmentService,
            IShoppingCartService shoppingCartService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            ITaxService taxService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            IWorkContext workContext,
            OmnisendCustomerService omnisendCustomerService,
            OmnisendHelper omnisendHelper,
            OmnisendHttpClient omnisendHttpClient)
        {
            _actionContextAccessor = actionContextAccessor;
            _addressService = addressService;
            _categoryService = categoryService;
            _countryService = countryService;
            _customerService = customerService;
            _discountService = discountService;
            _localizationService = localizationService;
            _genericAttributeService = genericAttributeService;
            _manufacturerService = manufacturerService;
            _measureService = measureService;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _paymentPluginManager = paymentPluginManager;
            _pictureService = pictureService;
            _productService = productService;
            _productTagService = productTagService;
            _shipmentService = shipmentService;
            _shoppingCartService = shoppingCartService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _taxService = taxService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _workContext = workContext;
            _omnisendCustomerService = omnisendCustomerService;
            _omnisendHelper = omnisendHelper;
            _omnisendHttpClient = omnisendHttpClient;
        }

        #endregion

        #region Utilities

        private void SendEvent(CustomerEvents customerEvent)
        {
            if (customerEvent == null)
                return;

            var data = JsonConvert.SerializeObject(customerEvent);
            _omnisendHttpClient.PerformRequest(OmnisendDefaults.CustomerEventsApiUrl, data, HttpMethod.Post);
        }

        private CustomerEvents CreateAddedProductToCartEvent(ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);

            var customerEvent = _omnisendCustomerService.CreateCustomerEvent(customer,
                PrepareAddedProductToCartProperty(shoppingCartItem));

            return customerEvent;
        }

        private CustomerEvents CreateStartedCheckoutEvent()
        {
            var customer = _workContext.CurrentCustomer;
            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PrepareStartedCheckoutProperty(customer));

            return customerEvent;
        }

        private CustomerEvents CreateOrderPlacedEvent(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PreparePlacedOrderProperty(order));

            return customerEvent;
        }

        private CustomerEvents CreateOrderPaidEvent(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PreparePlacedPaidProperty(order));

            return customerEvent;
        }

        private CustomerEvents CreateOrderCanceledEvent(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PrepareOrderCanceledProperty(order));

            return customerEvent;
        }

        private CustomerEvents CreateOrderFulfilledEvent(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PrepareOrderFulfilledProperty(order));

            return customerEvent;
        }

        private CustomerEvents CreateOrderRefundedEvent(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            var customerEvent =
                _omnisendCustomerService.CreateCustomerEvent(customer,
                    PrepareOrderRefundedProperty(order));

            return customerEvent;
        }

        private AddedProductToCartProperty PrepareAddedProductToCartProperty(
            ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);
            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart,
                shoppingCartItem.StoreId);

            var cartId = _omnisendCustomerService.GetCartId(customer);

            var property = new AddedProductToCartProperty
            {
                AbandonedCheckoutURL = _omnisendCustomerService.GetAbandonedCheckoutUrl(cartId),
                CartId = cartId,
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                LineItems =
                    cart.Select(ShoppingCartItemToProductItem)
                        .ToList(),
                Value = GetShoppingCartItemPrice(shoppingCartItem).price,
                AddedItem = ShoppingCartItemToProductItem(shoppingCartItem)
            };

            return property;
        }

        private StartedCheckoutProperty PrepareStartedCheckoutProperty(Customer customer)
        {
            var store = _storeContext.CurrentStore;
            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);

            var cartSum = _orderTotalCalculationService.GetShoppingCartTotal(cart) ?? 0;
            var cartId = _omnisendCustomerService.GetCartId(customer);

            var property = new StartedCheckoutProperty
            {
                AbandonedCheckoutURL = _omnisendCustomerService.GetAbandonedCheckoutUrl(cartId),
                CartId = cartId,
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                LineItems = cart.Select(ShoppingCartItemToProductItem)
                    .ToList(),
                Value = (float)cartSum
            };

            return property;
        }

        private PlacedOrderProperty PreparePlacedOrderProperty(Order order)
        {
            var property = new PlacedOrderProperty();
            FillOrderEventBase(property, order);

            return property;
        }

        private PaidForOrderProperty PreparePlacedPaidProperty(Order order)
        {
            var property = new PaidForOrderProperty();
            FillOrderEventBase(property, order);

            return property;
        }

        private OrderCanceledProperty PrepareOrderCanceledProperty(Order order)
        {
            var property = new OrderCanceledProperty();
            FillOrderEventBase(property, order);
            property.CancelReason = null;

            return property;
        }

        private OrderFulfilledProperty PrepareOrderFulfilledProperty(Order order)
        {
            var property = new OrderFulfilledProperty();
            FillOrderEventBase(property, order);

            return property;
        }

        private OrderRefundedProperty PrepareOrderRefundedProperty(Order order)
        {
            var property = new OrderRefundedProperty();
            FillOrderEventBase(property, order);
            property.TotalRefundedAmount = (float)order.RefundedAmount;

            return property;
        }

        private ProductItem ShoppingCartItemToProductItem(ShoppingCartItem shoppingCartItem)
        {
            var product = _productService.GetProductById(shoppingCartItem.ProductId);

            var (sku, variantId) = _omnisendHelper.GetSkuAndVariantId(product, shoppingCartItem.AttributesXml);
            var (price, discount) = GetShoppingCartItemPrice(shoppingCartItem);
            var picture = _pictureService.GetProductPicture(product, shoppingCartItem.AttributesXml);
            var pictureUrl = _pictureService.GetPictureUrl(picture.Id);

            var productItem = new ProductItem
            {
                ProductCategories = GetProductCategories(product),
                ProductDescription = product.ShortDescription,
                ProductDiscount = discount,
                ProductId = product.Id,
                ProductImageURL = pictureUrl,
                ProductPrice = price,
                ProductQuantity = shoppingCartItem.Quantity,
                ProductSku = sku,
                ProductStrikeThroughPrice = (float)product.OldPrice,
                ProductTitle = product.Name,
                ProductURL = _omnisendHelper.GetProductUrl(product),
                ProductVariantId = variantId,
                ProductVariantImageURL = pictureUrl
            };

            return productItem;
        }

        private void FillOrderEventBase(OrderEventBaseProperty property, Order order)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

            var items = _orderService.GetOrderItems(order.Id);
            var appliedDiscounts = _discountService.GetAllDiscountUsageHistory(orderId: order.Id);

            var paymentMethodName = _paymentPluginManager.LoadPluginBySystemName(order.PaymentMethodSystemName) is { } plugin
                ? _localizationService.GetLocalizedFriendlyName(plugin, order.CustomerLanguageId)
                : order.PaymentMethodSystemName;

            property.BillingAddress = GetAddressItemData(order.BillingAddressId);
            property.CreatedAt = order.CreatedOnUtc.ToDtoString();
            property.Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode();
            property.Discounts = appliedDiscounts.Select(duh =>
            {
                var discount = _discountService.GetDiscountById(duh.DiscountId);

                return new DiscountItem
                {
                    Amount = (float)discount.DiscountAmount,
                    Code = discount.CouponCode,
                    Type = discount.DiscountType.ToString()
                };
            }).ToList();

            property.FulfillmentStatus = order.OrderStatus.ToString();
            property.LineItems =
                items.Select(OrderItemToProductItem).ToList();
            property.Note = null;
            property.OrderId = order.CustomOrderNumber;
            property.OrderNumber = order.Id;
            property.OrderStatusURL = urlHelper.RouteUrl("OrderDetails", new { orderId = order.Id }, _webHelper.CurrentRequestProtocol);
            property.PaymentMethod = paymentMethodName;
            property.PaymentStatus = order.PaymentStatus.ToString();
            property.ShippingAddress = GetAddressItemData(order.ShippingAddressId);
            property.ShippingMethod = order.ShippingMethod;
            property.ShippingPrice = (float)order.OrderShippingInclTax;
            property.SubTotalPrice = (float)order.OrderSubtotalInclTax;
            property.SubTotalTaxIncluded = true;
            property.Tags = null;
            property.TotalDiscount = (float)order.OrderDiscount;
            property.TotalPrice = (float)order.OrderTotal;
            property.TotalTax = (float)order.OrderTax;

            if (_shipmentService.GetShipmentsByOrderId(order.Id).LastOrDefault() is { } shipment &&
                _shipmentService.GetShipmentTracker(shipment) is { } shipmentTracker)
                property.Tracking = new TrackingItem
                {
                    Code = shipment.TrackingNumber,
                    CourierURL = shipmentTracker.GetUrl(shipment.TrackingNumber)
                };
        }

        private OrderProductItem OrderItemToProductItem(OrderItem orderItem)
        {
            var product = _productService.GetProductById(orderItem.ProductId);

            var (sku, variantId) = _omnisendHelper.GetSkuAndVariantId(product, orderItem.AttributesXml);

            var picture = _pictureService.GetProductPicture(product, orderItem.AttributesXml);
            var pictureUrl = _pictureService.GetPictureUrl(picture.Id);

            var productManufacturer = _manufacturerService.GetProductManufacturersByProductId(orderItem.ProductId).FirstOrDefault();
            var manufacturer = _manufacturerService.GetManufacturerById(productManufacturer?.ManufacturerId ?? 0);
            var productsTags = _productTagService.GetAllProductTagsByProductId(product.Id);

            var weight = _measureService.GetMeasureWeightBySystemKeyword("grams") is { } measureWeight
                ? _measureService.ConvertFromPrimaryMeasureWeight(orderItem.ItemWeight ?? 0, measureWeight)
                : 0;

            float discount = 0;

            if (orderItem.DiscountAmountInclTax > 0 && orderItem.Quantity > 0)
                discount = (float)orderItem.DiscountAmountInclTax / orderItem.Quantity;

            var productItem = new OrderProductItem
            {
                ProductCategories = GetProductCategories(product),
                ProductDescription = product.ShortDescription,
                ProductDiscount = discount,
                ProductId = product.Id,
                ProductImageURL = pictureUrl,
                ProductPrice = (float)orderItem.UnitPriceInclTax + discount,
                ProductQuantity = orderItem.Quantity,
                ProductSku = sku,
                ProductStrikeThroughPrice = (float)product.OldPrice,
                ProductTags = productsTags.Select(tag => tag.Name).ToList(),
                ProductTitle = product.Name,
                ProductURL = _omnisendHelper.GetProductUrl(product),
                ProductVariantId = variantId,
                ProductVariantImageURL = pictureUrl,
                ProductVendor = manufacturer?.Name,
                ProductWeight = (int)weight
            };

            return productItem;
        }

        private AddressItem GetAddressItemData(int? addressId)
        {
            var address = _addressService.GetAddressById(addressId ?? 0);

            if (address == null)
                return null;

            var country = _countryService.GetCountryById(address.CountryId ?? 0);
            var state = _stateProvinceService.GetStateProvinceById(address.StateProvinceId ?? 0);

            return new AddressItem
            {
                Address1 = address.Address1,
                Address2 = address.Address2,
                City = address.City,
                Company = address.Company,
                Country = country?.Name,
                CountryCode = country?.TwoLetterIsoCode,
                FirstName = address.FirstName,
                LastName = address.LastName,
                Phone = address.PhoneNumber,
                State = state?.Name,
                StateCode = state?.Abbreviation,
                Zip = address.ZipPostalCode,
            };
        }

        private (float price, float discountAmount) GetShoppingCartItemPrice(
            ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);
            var product = _productService.GetProductById(shoppingCartItem.ProductId);

            var scSubTotal = _shoppingCartService.GetSubTotal(shoppingCartItem, true, out var discountAmount, out _, out  _);
            var price = (float)_taxService.GetProductPrice(product, scSubTotal, true, customer, out _);

            return (price, (float)discountAmount);
        }

        private List<ProductItem.ProductItemCategories> GetProductCategories(Product product)
        {
            var productCategories = _categoryService.GetProductCategoriesByProductId(product.Id);

            return productCategories.Select(pc => new ProductItem.ProductItemCategories
            {
                Id = pc.Id,
                Title = _categoryService.GetCategoryById(pc.CategoryId).Name
            }).ToList();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Send "added product to cart" event
        /// </summary>
        /// <param name="shoppingCartItem">Shopping cart item</param>
        public void SendAddedProductToCartEvent(ShoppingCartItem shoppingCartItem)
        {
            SendEvent(CreateAddedProductToCartEvent(shoppingCartItem));
        }

        /// <summary>
        /// Send "order placed" event
        /// </summary>
        /// <param name="order">Order</param>
        public void SendOrderPlacedEvent(Order order)
        {
            SendEvent(CreateOrderPlacedEvent(order));
        }

        /// <summary>
        /// Send "order paid" event
        /// </summary>
        /// <param name="eventMessage">Order paid event</param>
        public void SendOrderPaidEvent(OrderPaidEvent eventMessage)
        {
            SendEvent(CreateOrderPaidEvent(eventMessage.Order));
        }

        /// <summary>
        /// Send "order refunded" event
        /// </summary>
        /// <param name="eventMessage">Order refunded event</param>
        public void SendOrderRefundedEvent(OrderRefundedEvent eventMessage)
        {
            if (eventMessage.Order.PaymentStatus == PaymentStatus.Refunded)
                SendEvent(CreateOrderRefundedEvent(eventMessage.Order));
        }

        /// <summary>
        /// Send "order canceled" or "order fulfilled" events
        /// </summary>
        /// <param name="order">The order</param>
        public void SendOrderStatusChangedEvent(Order order)
        {
            switch (order.OrderStatus)
            {
                case OrderStatus.Cancelled:
                {
                    var sent = _genericAttributeService.GetAttribute<bool>(order, OmnisendDefaults.OrderCanceledAttribute);

                    if (sent)
                        return;

                    SendEvent(CreateOrderCanceledEvent(order));

                    _genericAttributeService.SaveAttribute(order, OmnisendDefaults.OrderCanceledAttribute, true);

                    break;
                }
                case OrderStatus.Complete:
                {
                    var sent = _genericAttributeService.GetAttribute<bool>(order, OmnisendDefaults.OrderFulfilledAttribute);

                    if (sent)
                        return;

                    SendEvent(CreateOrderFulfilledEvent(order));

                    _genericAttributeService.SaveAttribute(order, OmnisendDefaults.OrderFulfilledAttribute, true);

                    break;
                }
            }
        }

        /// <summary>
        /// Send "started checkout" event
        /// </summary>
        /// <param name="eventMessage">Page rendering event</param>
        public void SendStartedCheckoutEvent(PageRenderingEvent eventMessage)
        {
            var routeName = eventMessage.GetRouteName() ?? string.Empty;
            if (!routeName.Equals("CheckoutOnePage", StringComparison.InvariantCultureIgnoreCase) &&
                !routeName.Equals("CheckoutBillingAddress", StringComparison.InvariantCultureIgnoreCase))
                return;

            SendEvent(CreateStartedCheckoutEvent());
        }

        #endregion
    }
}