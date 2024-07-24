using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Plugin.Misc.Omnisend.DTO;
using Nop.Services.Catalog;
using Nop.Services.Directory;
using Nop.Services.Media;
using Nop.Services.Seo;

namespace Nop.Plugin.Misc.Omnisend.Services
{
    /// <summary>
    /// Represents the plugins helpers class
    /// </summary>
    public class OmnisendHelper
    {
        #region Fields

        private string _primaryStoreCurrencyCode;

        private readonly CurrencySettings _currencySettings;
        private readonly IActionContextAccessor _actionContextAccessor;
        private readonly ICurrencyService _currencyService;
        private readonly IPictureService _pictureService;
        private readonly IProductAttributeParser _productAttributeParser;
        private readonly IUrlHelperFactory _urlHelperFactory;
        private readonly IUrlRecordService _urlRecordService;
        private readonly IWebHelper _webHelper;

        #endregion

        #region Ctor

        public OmnisendHelper(CurrencySettings currencySettings,
            IActionContextAccessor actionContextAccessor,
            ICurrencyService currencyService,
            IPictureService pictureService,
            IProductAttributeParser productAttributeParser,
            IUrlHelperFactory urlHelperFactory,
            IUrlRecordService urlRecordService,
            IWebHelper webHelper)
        {
            _currencySettings = currencySettings;
            _actionContextAccessor = actionContextAccessor;
            _currencyService = currencyService;
            _pictureService = pictureService;
            _productAttributeParser = productAttributeParser;
            _urlHelperFactory = urlHelperFactory;
            _urlRecordService = urlRecordService;
            _webHelper = webHelper;
        }

        #endregion
        
        #region Methods

        /// <summary>
        /// Gets the primary store currency code
        /// </summary>
        public string GetPrimaryStoreCurrencyCode()
        {
            if (!string.IsNullOrEmpty(_primaryStoreCurrencyCode))
                return _primaryStoreCurrencyCode;

            var currency = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId);

            _primaryStoreCurrencyCode = currency.CurrencyCode;

            return _primaryStoreCurrencyCode;
        }

        /// <summary>
        /// Gets the product or product attribute combination SKU and ID
        /// </summary>
        /// <param name="product">Product</param>
        /// <param name="attributesXml">Product attributes on XML format</param>
        public (string sku, int variantId) GetSkuAndVariantId(Product product, string attributesXml)
        {
            var sku = product.Sku;
            var variantId = product.Id;

            var combination = _productAttributeParser.FindProductAttributeCombination(product, attributesXml);

            if (combination == null)
                return (sku, variantId);

            sku = combination.Sku;
            variantId = combination.Id;

            return (sku, variantId);
        }

        /// <summary>
        /// Gets the product URL
        /// </summary>
        /// <param name="product">Product</param>
        public string GetProductUrl(Product product)
        {
            var values = new { SeName = _urlRecordService.GetSeName(product) };

            return GetProductUrl(values);
        }

        /// <summary>
        /// Gets the product URL
        /// </summary>
        /// <param name="values">An object that contains route values</param>
        public string GetProductUrl(object values)
        {
            var urlHelper = _urlHelperFactory.GetUrlHelper(_actionContextAccessor.ActionContext);

            return urlHelper.RouteUrl("Product", values, _webHelper.CurrentRequestProtocol, null, null);
        }

        /// <summary>
        /// Gets the product picture URL
        /// </summary>
        /// <param name="product">Product</param>
        public ProductDto.Image GetProductPictureUrl(Product product)
        {
            var picture = _pictureService
                .GetPicturesByProductId(product.Id, 1).DefaultIfEmpty(null).FirstOrDefault();

            var url = _pictureService.GetPictureUrl(picture?.Id ?? 0);

            var storeLocation = _webHelper.GetStoreLocation();

            if (!url.StartsWith(storeLocation))
                url = storeLocation + url;

            return new ProductDto.Image { ImageId = (picture?.Id ?? 0).ToString(), Url = url };
        }

        #endregion
    }
}