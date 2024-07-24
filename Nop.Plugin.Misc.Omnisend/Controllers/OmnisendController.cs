﻿using System;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Plugin.Misc.Omnisend.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Web.Framework.Controllers;

namespace Nop.Plugin.Misc.Omnisend.Controllers
{
    public class OmnisendController : BasePluginController
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OmnisendService _omnisendService;

        #endregion

        #region Ctor

        public OmnisendController(ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IWebHelper webHelper,
            IWorkContext workContext,
            OmnisendService omnisendService)
        {
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _webHelper = webHelper;
            _workContext = workContext;
            _omnisendService = omnisendService;
        }

        #endregion

        #region Methods

        public IActionResult AbandonedCheckout(string cartId)
        {
            var customer = _workContext.CurrentCustomer;
            if (_customerService.IsGuest(customer))
                return RedirectToRoute("Login", new { ReturnUrl = _webHelper.GetRawUrl(Request) });

            var customerEmail = _genericAttributeService.GetAttribute<string>(customer, OmnisendDefaults.CustomerEmailAttribute);
            if (!string.IsNullOrEmpty(customerEmail) && !customerEmail.Equals(customer.Email, StringComparison.InvariantCultureIgnoreCase))
                return RedirectToRoute("Login", new { ReturnUrl = _webHelper.GetRawUrl(Request) });

            _omnisendService.RestoreShoppingCart(cartId);

            return RedirectToRoute("ShoppingCart");
        }

        #endregion
    }
}
