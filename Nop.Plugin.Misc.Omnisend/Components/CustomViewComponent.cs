using System;
using System.Linq;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewComponents;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Plugin.Misc.Omnisend.Services;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Web.Framework.Components;
using Nop.Web.Framework.Infrastructure;
using Nop.Web.Models.Catalog;

namespace Nop.Plugin.Misc.Omnisend.Components
{
    /// <summary>
    /// Represents view component to embed tracking script on pages
    /// </summary>
    [ViewComponent(Name = OmnisendDefaults.VIEW_COMPONENT_NAME)]
    public class WidgetsOmnisendViewComponent : NopViewComponent
    {
        #region Fields

        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OmnisendHelper _omnisendHelper;
        private readonly OmnisendService _omnisendService;
        private readonly OmnisendSettings _omnisendSettings;

        #endregion

        #region Ctor

        public WidgetsOmnisendViewComponent(ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IWebHelper webHelper,
            IWorkContext workContext,
            OmnisendHelper omnisendHelper,
            OmnisendService omnisendService,
            OmnisendSettings omnisendSettings)
        {
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _webHelper = webHelper;
            _workContext = workContext;
            _omnisendHelper = omnisendHelper;
            _omnisendService = omnisendService;
            _omnisendSettings = omnisendSettings;
        }

        #endregion

        #region Utilities

        private string AddIdentifyContactScript(string script)
        {
            var customer = _workContext.CurrentCustomer;

            if (_customerService.IsGuest(customer))
                GenerateGuestScript(customer);

            var identifyContactScript = _genericAttributeService.GetAttribute<string>(customer,
                OmnisendDefaults.IdentifyContactAttribute);

            if (string.IsNullOrEmpty(identifyContactScript))
                return script;

            script += $"{Environment.NewLine}{identifyContactScript}";

            _genericAttributeService.SaveAttribute<string>(customer,
                OmnisendDefaults.IdentifyContactAttribute, null);

            return script;
        }

        private void GenerateGuestScript(Customer customer)
        {
            var customerEmail = _genericAttributeService.GetAttribute<string>(customer,
                OmnisendDefaults.CustomerEmailAttribute);

            if (!string.IsNullOrEmpty(customerEmail))
                return;

            //try to get the ContactId from query parameters
            var omnisendContactId = _webHelper.QueryString<string>(OmnisendDefaults.ContactIdQueryParamName);
            if (string.IsNullOrEmpty(omnisendContactId))
                //try to get the ContactId from cookies
                Request.Cookies.TryGetValue($"{OmnisendDefaults.ContactIdQueryParamName}", out omnisendContactId);

            if (string.IsNullOrEmpty(omnisendContactId))
                return;

            var contact = _omnisendService.GetContactInfo(omnisendContactId);

            var email = contact?.Identifiers.FirstOrDefault(p => !string.IsNullOrEmpty(p.Id))?.Id;

            if (string.IsNullOrEmpty(email))
                return;

            _genericAttributeService.SaveAttribute(customer,
                OmnisendDefaults.CustomerEmailAttribute, email);

            if (string.IsNullOrEmpty(_omnisendSettings.IdentifyContactScript))
                return;

            var identifyScript = _omnisendSettings.IdentifyContactScript.Replace(OmnisendDefaults.Email,
                email);

            _genericAttributeService.SaveAttribute(customer,
                OmnisendDefaults.IdentifyContactAttribute, identifyScript);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Invoke view component
        /// </summary>
        /// <param name="widgetZone">Widget zone name</param>
        /// <param name="additionalData">Additional data</param>
        /// <returns>
        /// The view component result
        /// </returns>
        public IViewComponentResult Invoke(string widgetZone, object additionalData)
        {
            //ensure tracking is enabled
            if (!_omnisendSettings.UseTracking || string.IsNullOrEmpty(_omnisendSettings.BrandId))
                return Content(string.Empty);

            var script = string.Empty;

            //prepare tracking script
            if (!string.IsNullOrEmpty(_omnisendSettings.TrackingScript) &&
                widgetZone.Equals(PublicWidgetZones.BodyStartHtmlTagAfter, StringComparison.InvariantCultureIgnoreCase))
                script = AddIdentifyContactScript(_omnisendSettings.TrackingScript
                    .Replace(OmnisendDefaults.BrandId, _omnisendSettings.BrandId));

            //prepare product script
            if (!string.IsNullOrEmpty(_omnisendSettings.ProductScript) &&
                widgetZone.Equals(PublicWidgetZones.ProductDetailsBottom,
                    StringComparison.InvariantCultureIgnoreCase) &&
                additionalData is ProductDetailsModel productDetails)
                script = _omnisendSettings.ProductScript
                    .Replace(OmnisendDefaults.ProductId, $"{productDetails.Id}")
                    .Replace(OmnisendDefaults.Sku, productDetails.Sku)
                    .Replace(OmnisendDefaults.Currency, productDetails.ProductPrice.CurrencyCode)
                    .Replace(OmnisendDefaults.Price, $"{(int)(productDetails.ProductPrice.PriceValue * 100)}")
                    .Replace(OmnisendDefaults.Title, productDetails.Name)
                    .Replace(OmnisendDefaults.ImageUrl, productDetails.DefaultPictureModel.ImageUrl)
                    .Replace(OmnisendDefaults.ProductUrl, _omnisendHelper.GetProductUrl(new { productDetails.SeName }));

            if (string.IsNullOrEmpty(script))
                return Content(string.Empty);

            return new HtmlContentViewComponentResult(new HtmlString(script));
        }

        #endregion
    }
}
