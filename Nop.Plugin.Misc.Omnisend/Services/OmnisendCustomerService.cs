using System;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.Omnisend.DTO;
using Nop.Plugin.Misc.Omnisend.DTO.Events;
using Nop.Services.Common;

namespace Nop.Plugin.Misc.Omnisend.Services
{
    /// <summary>
    /// Represents the helper class for customer
    /// </summary>
    public class OmnisendCustomerService
    {
        #region Fields

        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly IAddressService _addressService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IWebHelper _webHelper;
        private readonly OmnisendHttpClient _httpClient;

        #endregion

        #region Ctor

        public OmnisendCustomerService(IActionContextAccessor actionContextAccessor,
            IAddressService addressService,
            IGenericAttributeService genericAttributeService,
            IUrlHelperFactory urlHelperFactory,
            IWebHelper webHelper,
            OmnisendHttpClient httpClient)
        {
            _actionContextAccessor = actionContextAccessor;
            _addressService = addressService;
            _genericAttributeService = genericAttributeService;
            _urlHelperFactory = urlHelperFactory;
            _webHelper = webHelper;
            _httpClient = httpClient;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Gets the cart identifier for customer
        /// </summary>
        /// <param name="customer">Customer</param>
        public string GetCartId(Customer customer)
        {
            var cartId = _genericAttributeService.GetAttribute<string>(customer,
                OmnisendDefaults.StoredCustomerShoppingCartIdAttribute);

            cartId = string.IsNullOrEmpty(cartId)
                ? _genericAttributeService.GetAttribute<string>(customer,
                    OmnisendDefaults.CurrentCustomerShoppingCartIdAttribute)
                : cartId;

            if (!string.IsNullOrEmpty(cartId))
                return cartId;

            cartId = Guid.NewGuid().ToString();

            _genericAttributeService.SaveAttribute(customer,
                OmnisendDefaults.CurrentCustomerShoppingCartIdAttribute, cartId);

            return cartId;
        }

        /// <summary>
        /// Gets the customer email address
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="billingAddressId">billing address identifier</param>
        public string GetEmail(Customer customer, int? billingAddressId = null)
        {
            var email = !string.IsNullOrEmpty(customer.Email)
                ? customer.Email
                : _genericAttributeService.GetAttribute<string>(customer,
                    OmnisendDefaults.CustomerEmailAttribute);

            return !string.IsNullOrEmpty(email)
                ? email
                : (_addressService.GetAddressById((billingAddressId ?? customer.BillingAddressId) ?? 0))
                ?.Email;
        }

        /// <summary>
        /// Create customer event
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <param name="properties">Event properties</param>
        /// <returns>
        /// Customer event or null if customer email is not determinate
        /// </returns>
        public CustomerEvents CreateCustomerEvent(Customer customer, IEventProperty properties)
        {
            var email = GetEmail(customer);

            if (string.IsNullOrEmpty(email))
                return null;

            var customerEvent = new CustomerEvents { Email = email, Properties = properties };

            return customerEvent;
        }

        /// <summary>
        /// Gets the contact identifier
        /// </summary>
        /// <param name="email">Email to determinate customer</param>
        public string GetContactId(string email)
        {
            var url = $"{OmnisendDefaults.ContactsApiUrl}?email={email}&limit=1";

            var res = _httpClient.PerformRequest(url, httpMethod: HttpMethod.Get);

            if (string.IsNullOrEmpty(res))
                return null;

            var contact = JsonConvert
                .DeserializeAnonymousType(res, new { contacts = new[] { new { contactID = string.Empty } } })?.contacts
                .FirstOrDefault();

            return contact?.contactID;
        }

        /// <summary>
        /// Store the cart identifier
        /// </summary>
        /// <param name="customer">Customer</param>
        public void StoreCartId(Customer customer)
        {
            var cartId = _genericAttributeService.GetAttribute<string>(customer,
                OmnisendDefaults.StoredCustomerShoppingCartIdAttribute);

            if (string.IsNullOrEmpty(cartId))
                _genericAttributeService.SaveAttribute(customer,
                    OmnisendDefaults.StoredCustomerShoppingCartIdAttribute, GetCartId(customer));
        }

        /// <summary>
        /// Delete the current shopping cart identifier for customer
        /// </summary>
        /// <param name="customer">Customer</param>
        public void DeleteCurrentCustomerShoppingCartId(Customer customer)
        {
            _genericAttributeService.SaveAttribute<string>(customer,
                OmnisendDefaults.CurrentCustomerShoppingCartIdAttribute, null);
        }

        /// <summary>
        /// Specifies whether to send the delete shopping cart event
        /// </summary>
        /// <param name="customer">Customer</param>
        /// <returns>
        /// The True if we need to sand delete events</returns>
        public bool IsNeedToSendDeleteShoppingCartEvent(Customer customer)
        {
            return string.IsNullOrEmpty(_genericAttributeService.GetAttribute<string>(customer,
                OmnisendDefaults.StoredCustomerShoppingCartIdAttribute));
        }

        /// <summary>
        /// Delete the stored shopping cart identifier for customer
        /// </summary>
        /// <param name="customer">Customer</param>
        public void DeleteStoredCustomerShoppingCartId(Customer customer)
        {
            _genericAttributeService.SaveAttribute<string>(customer,
                OmnisendDefaults.StoredCustomerShoppingCartIdAttribute, null);
        }

        /// <summary>
        /// Gets the abandoned checkout url
        /// </summary>
        /// <param name="cartId">Cart identifier</param>
        /// <returns>The abandoned checkout url</returns>
        public string GetAbandonedCheckoutUrl(string cartId)
        {
            if (_actionContextAccessor.ActionContext == null)
                return null;

            return _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext)
                .RouteUrl(OmnisendDefaults.AbandonedCheckoutRouteName, new { cartId }, _webHelper.CurrentRequestProtocol);
        }

        #endregion
    }
}