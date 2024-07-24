using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Misc.Omnisend.DTO;
using Nop.Plugin.Misc.Omnisend.Models;
using Nop.Plugin.Misc.Omnisend.Services;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Misc.Omnisend.Controllers
{
    [Area(AreaNames.Admin)]
    [AuthorizeAdmin]
    [AutoValidateAntiforgeryToken]
    public class OmnisendAdminController : BasePluginController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly OmnisendService _omnisendService;
        private readonly OmnisendSettings _omnisendSettings;

        #endregion

        #region Ctor

        public OmnisendAdminController(ILocalizationService localizationService,
            INotificationService notificationService,
            ISettingService settingService,
            OmnisendService omnisendService,
            OmnisendSettings omnisendSettings)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _settingService = settingService;
            _omnisendService = omnisendService;
            _omnisendSettings = omnisendSettings;
        }

        #endregion

        #region Utilities

        private void FillBatches(ConfigurationModel model)
        {
            if (!_omnisendSettings.BatchesIds.Any())
                return;

            var batches = _omnisendSettings.BatchesIds.Select(_omnisendService.GetBatchInfo)
                .ToList();

            batches = batches.Where(p => p != null).ToList();

            if (!batches.Any())
            {
                _omnisendSettings.BatchesIds.Clear();
                _settingService.SaveSetting(_omnisendSettings);
            }

            model.Batches = batches;

            bool needBlock(BatchResponse response, string endpoint)
            {
                return response.Endpoint.Equals(endpoint, StringComparison.InvariantCultureIgnoreCase) &&
                       !response.Status.Equals(OmnisendDefaults.BatchFinishedStatus,
                           StringComparison.InvariantCultureIgnoreCase);
            }

            model.BlockSyncContacts = batches.Any(p => needBlock(p, OmnisendDefaults.ContactsEndpoint));
            model.BlockSyncOrders = batches.Any(p => needBlock(p, OmnisendDefaults.OrdersEndpoint));
            model.BlockSyncProducts = batches.Any(p => needBlock(p, OmnisendDefaults.ProductsEndpoint)) || batches.Any(p => needBlock(p, OmnisendDefaults.CategoriesEndpoint));

            foreach (var batchResponse in batches.Where(p =>
                         p.Status.Equals(OmnisendDefaults.BatchFinishedStatus,
                             StringComparison.InvariantCultureIgnoreCase)))
            {
                _omnisendSettings.BatchesIds.Remove(batchResponse.BatchId);
                _settingService.SaveSetting(_omnisendSettings);
            }
        }

        #endregion

        #region Methods

        public IActionResult Configure()
        {
            var model = new ConfigurationModel
            {
                ApiKey = _omnisendSettings.ApiKey,
                UseTracking = _omnisendSettings.UseTracking
            };

            FillBatches(model);

            return View(model);
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("save")]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!ModelState.IsValid)
                return Configure();

            if (_omnisendSettings.ApiKey != model.ApiKey || string.IsNullOrEmpty(_omnisendSettings.BrandId))
            {
                var brandId = _omnisendService.GetBrandId(model.ApiKey);

                if (brandId != null)
                {
                    _omnisendSettings.ApiKey = model.ApiKey;
                    _omnisendSettings.BrandId = brandId;

                    _omnisendService.SendCustomerData();
                }
            }

            //_omnisendSettings.UseTracking = model.UseTracking;
            _omnisendSettings.UseTracking = true;

            _settingService.SaveSetting(_omnisendSettings);

            if (string.IsNullOrEmpty(_omnisendSettings.BrandId))
                _notificationService.ErrorNotification(_localizationService.GetResource("Plugins.Misc.Omnisend.CantGetBrandId"));
            else
                _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync-contacts")]
        public IActionResult SyncContacts()
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(_omnisendSettings.BrandId))
                return Configure();

            _omnisendService.SyncContacts();

            return Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync-products")]
        public IActionResult SyncProducts()
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(_omnisendSettings.BrandId))
                return Configure();

            _omnisendService.SyncCategories();
            _omnisendService.SyncProducts();

            return Configure();
        }

        [HttpPost, ActionName("Configure")]
        [FormValueRequired("sync-orders")]
        public IActionResult SyncOrders()
        {
            if (!ModelState.IsValid || string.IsNullOrEmpty(_omnisendSettings.BrandId))
                return Configure();

            _omnisendService.SyncOrders();
            _omnisendService.SyncCarts();

            return Configure();
        }

        #endregion
    }
}
