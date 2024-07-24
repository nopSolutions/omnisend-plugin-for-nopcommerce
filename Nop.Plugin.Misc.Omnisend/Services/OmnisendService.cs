using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Messages;
using Nop.Core.Domain.Orders;
using Nop.Data;
using Nop.Plugin.Misc.Omnisend.DTO;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Orders;
using Nop.Services.Tax;

namespace Nop.Plugin.Misc.Omnisend.Services
{
    /// <summary>
    /// Represents the main plugin service class
    /// </summary>
    public class OmnisendService
    {
        #region Fields

        private readonly ICategoryService _categoryService;
        private readonly ICountryService _countryService;
        private readonly ICustomerService _customerService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderService _orderService;
        private readonly IOrderTotalCalculationService _orderTotalCalculationService;
        private readonly IProductAttributeService _productAttributeService;
        private readonly IProductService _productService;
        private readonly IRepository<Country> _countryRepository;
        private readonly IRepository<Customer> _customerRepository;
        private readonly IRepository<GenericAttribute> _genericAttributeRepository;
        private readonly IRepository<NewsLetterSubscription> _newsLetterSubscriptionRepository;
        private readonly IRepository<StateProvince> _stateProvinceRepository;
        private readonly ISettingService _settingService;
        private readonly IShoppingCartService _shoppingCartService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly IStoreContext _storeContext;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly OmnisendCustomerService _omnisendCustomerService;
        private readonly OmnisendHelper _omnisendHelper;
        private readonly OmnisendHttpClient _omnisendHttpClient;
        private readonly OmnisendSettings _omnisendSettings;

        #endregion

        #region Ctor

        public OmnisendService(ICategoryService categoryService,
            ICountryService countryService,
            ICustomerService customerService,
            IGenericAttributeService genericAttributeService,
            IOrderService orderService,
            IOrderTotalCalculationService orderTotalCalculationService,
            IProductAttributeService productAttributeService,
            IProductService productService,
            IRepository<Country> countryRepository,
            IRepository<Customer> customerRepository,
            IRepository<GenericAttribute> genericAttributeRepository,
            IRepository<NewsLetterSubscription> newsLetterSubscriptionRepository,
            IRepository<StateProvince> stateProvinceRepository,
            ISettingService settingService,
            IShoppingCartService shoppingCartService,
            IStateProvinceService stateProvinceService,
            IStoreContext storeContext,
            ITaxService taxService,
            IWebHelper webHelper,
            IWorkContext workContext,
            OmnisendCustomerService omnisendCustomerService,
            OmnisendHelper omnisendHelper,
            OmnisendHttpClient omnisendHttpClient,
            OmnisendSettings omnisendSettings)
        {
            _categoryService = categoryService;
            _countryService = countryService;
            _customerService = customerService;
            _genericAttributeService = genericAttributeService;
            _orderService = orderService;
            _orderTotalCalculationService = orderTotalCalculationService;
            _productAttributeService = productAttributeService;
            _productService = productService;
            _countryRepository = countryRepository;
            _customerRepository = customerRepository;
            _genericAttributeRepository = genericAttributeRepository;
            _newsLetterSubscriptionRepository = newsLetterSubscriptionRepository;
            _stateProvinceRepository = stateProvinceRepository;
            _settingService = settingService;
            _shoppingCartService = shoppingCartService;
            _stateProvinceService = stateProvinceService;
            _storeContext = storeContext;
            _taxService = taxService;
            _webHelper = webHelper;
            _workContext = workContext;
            _omnisendCustomerService = omnisendCustomerService;
            _omnisendHelper = omnisendHelper;
            _omnisendHttpClient = omnisendHttpClient;
            _omnisendSettings = omnisendSettings;
        }

        #endregion

        #region Utilities

        private void FillCustomerInfo(BaseContactInfoDto dto, Customer customer)
        {
            if (customer == null)
                return;

            dto.FirstName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.FirstNameAttribute);
            dto.LastName = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.LastNameAttribute);
            
            var country = _countryService.GetCountryById(_genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.CountryIdAttribute));

            if (country != null)
            {
                dto.Country = country.Name;
                dto.CountryCode = country.TwoLetterIsoCode;
            }

            var state = _stateProvinceService.GetStateProvinceById(_genericAttributeService.GetAttribute<int>(customer, NopCustomerDefaults.StateProvinceIdAttribute));

            if (state != null)
                dto.State = state.Name;

            dto.City = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.CityAttribute);
            dto.Address = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.StreetAddressAttribute);
            dto.PostalCode = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.ZipPostalCodeAttribute);
            dto.Gender = _genericAttributeService.GetAttribute<string>(customer, NopCustomerDefaults.GenderAttribute)?.ToLower() ?? "f";
            dto.BirthDate = _genericAttributeService.GetAttribute<DateTime?>(customer, NopCustomerDefaults.DateOfBirthAttribute)?.ToString("yyyy-MM-dd");
        }

        private OrderDto.OrderItemDto OrderItemToDto(OrderItem orderItem)
        {
            var product = _productService.GetProductById(orderItem.ProductId);

            var (sku, variantId) = _omnisendHelper.GetSkuAndVariantId(product, orderItem.AttributesXml);

            var dto = new OrderDto.OrderItemDto
            {
                ProductId = orderItem.ProductId.ToString(),
                Sku = sku,
                VariantId = variantId.ToString(),
                Title = product.Name,
                Quantity = orderItem.Quantity,
                Price = orderItem.PriceInclTax.ToCents()
            };

            return dto;
        }

        private ProductDto ProductToDto(Product product)
        {
            List<string> getProductCategories()
            {
                var productCategories = _categoryService.GetProductCategoriesByProductId(product.Id);

                return productCategories.Select(pc => pc.CategoryId.ToString()).ToList();
            }

            IList<ProductAttributeCombination> getProductCombinations()
            {
                return _productAttributeService.GetAllProductAttributeCombinations(product.Id);
            }

            var combinations = getProductCombinations();

            string getProductStatus(ProductAttributeCombination productAttributeCombination = null)
            {
                var status = "notAvailable";

                if (!product.Published || product.Deleted)
                    return status;

                int stockQuantity;

                switch (product.ManageInventoryMethod)
                {
                    case ManageInventoryMethod.ManageStock:
                        stockQuantity = _productService.GetTotalStockQuantity(product);

                        if (stockQuantity > 0 || product.BackorderMode == BackorderMode.AllowQtyBelow0)
                            status = "inStock";
                        else
                            status = "outOfStock";

                        break;
                    case ManageInventoryMethod.ManageStockByAttributes:
                        if (productAttributeCombination == null)
                            return combinations.Any(c => c.StockQuantity > 0 || c.AllowOutOfStockOrders) ? "inStock" : "outOfStock";

                        stockQuantity = productAttributeCombination.StockQuantity;

                        if (stockQuantity > 0 || productAttributeCombination.AllowOutOfStockOrders)
                            status = "inStock";
                        else
                            status = "outOfStock";

                        break;
                    case ManageInventoryMethod.DontManageStock:
                        status = "inStock";
                        break;
                }

                return status;
            }

            var dto = new ProductDto
            {
                ProductId = product.Id.ToString(),
                Title = product.Name,
                Status = getProductStatus(),
                Description = product.ShortDescription,
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                ProductUrl = _omnisendHelper.GetProductUrl(product),
                Images = new List<ProductDto.Image>
                {
                    _omnisendHelper.GetProductPictureUrl(product),
                },
                CreatedAt = product.CreatedOnUtc.ToDtoString(),
                UpdatedAt = product.UpdatedOnUtc.ToDtoString(),
                CategoryIDs = getProductCategories(),
                Variants = new List<ProductDto.Variant>
                {
                    new ProductDto.Variant()
                    {
                        VariantId = product.Id.ToString(),
                        Title = product.Name,
                        Sku = product.Sku,
                        Status = getProductStatus(),
                        Price = product.Price.ToCents()
                    }
                }
            };

            if (combinations.Any())
                dto.Variants.AddRange(combinations.Select(c => new ProductDto.Variant
                {
                    VariantId = c.Id.ToString(),
                    Title = product.Name,
                    Sku = c.Sku,
                    Status = getProductStatus(c),
                    Price = (c.OverriddenPrice ?? product.Price).ToCents()
                }).ToList());

            return dto;
        }

        private OrderDto OrderToDto(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);
            var email = _omnisendCustomerService.GetEmail(customer, order.BillingAddressId);
            if (string.IsNullOrEmpty(email))
                return null;

            var dto = new OrderDto
            {
                OrderId = order.OrderGuid.ToString(),
                Email = email,
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                OrderSum = order.OrderTotal.ToCents(),
                SubTotalSum = order.OrderSubtotalInclTax.ToCents(),
                DiscountSum = order.OrderDiscount.ToCents(),
                TaxSum = order.OrderTax.ToCents(),
                ShippingSum = order.OrderShippingInclTax.ToCents(),
                CreatedAt = order.CreatedOnUtc.ToDtoString(),
                Products = _orderService.GetOrderItems(order.Id).Select(OrderItemToDto).ToList()
            };

            return dto;
        }

        private CartItemDto ShoppingCartItemToDto(ShoppingCartItem shoppingCartItem, Customer customer)
        {
            var product = _productService.GetProductById(shoppingCartItem.ProductId);

            var (sku, variantId) = _omnisendHelper.GetSkuAndVariantId(product, shoppingCartItem.AttributesXml);

            var scSubTotal = _shoppingCartService.GetSubTotal(shoppingCartItem, true);

            var dto = new CartItemDto
            {
                CartProductId = shoppingCartItem.Id.ToString(),
                ProductId = shoppingCartItem.ProductId.ToString(),
                Sku = sku,
                VariantId = variantId.ToString(),
                Title = product.Name,
                Quantity = shoppingCartItem.Quantity,
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                Price = _taxService.GetProductPrice(product, scSubTotal, true, customer, out _).ToCents()
            };

            return dto;
        }

        private CategoryDto CategoryToDto(Category category)
        {
            return new CategoryDto
            {
                CategoryId = category.Id.ToString(),
                Title = category.Name,
                CreatedAt = category.CreatedOnUtc.ToDtoString()
            };
        }

        private CartDto GetCartDto(IList<ShoppingCartItem> cart)
        {
            if (!cart.Any())
                return null;

            var customerId = cart.First().CustomerId;
            var customer = _customerService.GetCustomerById(customerId);

            var items = cart
                .Select(ci => ShoppingCartItemToDto(ci, customer))
                .ToList();

            var cartSum = _orderTotalCalculationService.GetShoppingCartTotal(cart)
                ?.ToCents() ?? 0;

            var email = _omnisendCustomerService.GetEmail(customer);
            if (string.IsNullOrEmpty(email))
                return null;

            var cartId = _omnisendCustomerService.GetCartId(customer);

            return new CartDto
            {
                Currency = _omnisendHelper.GetPrimaryStoreCurrencyCode(),
                Email = email,
                CartId = cartId,
                CartSum = cartSum,
                Products = items,
                CartRecoveryUrl = _omnisendCustomerService.GetAbandonedCheckoutUrl(cartId)
            };
        }

        private void CreateOrder(Order order)
        {
            var orderDto = OrderToDto(order);
            if (orderDto is null)
                return;

            var data = JsonConvert.SerializeObject(orderDto);
            _omnisendHttpClient.PerformRequest(OmnisendDefaults.OrdersApiUrl, data, HttpMethod.Post);
        }

        private void CreateCart(IList<ShoppingCartItem> cart)
        {
            var cartDto = GetCartDto(cart);
            if (cartDto is null)
                return;

            var data = JsonConvert.SerializeObject(cartDto);
            _omnisendHttpClient.PerformRequest(OmnisendDefaults.CartsApiUrl, data, HttpMethod.Post);
        }

        private void UpdateCart(Customer customer, ShoppingCartItem newItem)
        {
            var item = ShoppingCartItemToDto(newItem, customer);
            if (item is null)
                return;

            var data = JsonConvert.SerializeObject(item);
            _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.CartsApiUrl}/{_omnisendCustomerService.GetCartId(customer)}/{OmnisendDefaults.ProductsEndpoint}", data, HttpMethod.Post);
        }

        private bool CanSendRequest(Customer customer)
        {
            return !string.IsNullOrEmpty(
                _omnisendCustomerService.GetEmail(customer, customer.BillingAddressId));
        }

        /// <summary>
        /// Prepare newsletter subscribers to sync
        /// </summary>
        /// <param name="storeId">Store identifier</param>
        /// <param name="pageIndex">Page index</param>
        /// <param name="pageSize">Page size</param>
        /// <param name="sendWelcomeMessage">Specifies whether to send a welcome message</param>
        /// <param name="subscriber">Newsletter subscription to filter</param>
        /// <param name="inactiveStatus">Inactive status</param>
        /// <returns>
        /// The list of subscriber data
        /// </returns>
        private List<IBatchSupport> PrepareNewsletterSubscribers(int storeId,
            int pageIndex, int pageSize,
            bool sendWelcomeMessage = false, NewsLetterSubscription subscriber = null, string inactiveStatus = "nonSubscribed")
        {
            //get contacts of newsletter subscribers
            var subscriptions = (subscriber == null ? _newsLetterSubscriptionRepository.Table : _newsLetterSubscriptionRepository.Table.Where(nlsr => nlsr.Id.Equals(subscriber.Id)))
                .Where(subscription => subscription.StoreId == storeId)
                .OrderBy(subscription => subscription.Id)
                .Skip(pageIndex * pageSize)
                .Take(pageSize);

            var contacts = from item in subscriptions
                join c in _customerRepository.Table on item.Email equals c.Email
                    into temp
                from c in temp.DefaultIfEmpty()
                where c == null || c.Active && !c.Deleted
                select new { subscription = item, customer = c };

            var contactsWithCountryId = from item in contacts
                join gr in _genericAttributeRepository.Table on new {item.customer.Id, KeyGroup=nameof(Customer), Key=NopCustomerDefaults.CountryIdAttribute } equals new {Id=gr.EntityId, gr.KeyGroup, gr.Key}
                    into temp
                from cr in temp.DefaultIfEmpty()
                select new { item.customer, item.subscription, countryId = int.Parse(cr.Value) };

            var contactsWithCountry = from item in contactsWithCountryId
                join cr in _countryRepository.Table on item.countryId equals cr.Id
                    into temp
                from cr in temp.DefaultIfEmpty()
                select new { item.customer, item.subscription, country = cr };

            var contactsWithStateId = from item in contactsWithCountry
                join gr in _genericAttributeRepository.Table on new { item.customer.Id, KeyGroup = nameof(Customer), Key = NopCustomerDefaults.StateProvinceIdAttribute } equals new { Id = gr.EntityId, gr.KeyGroup, gr.Key }
                                      into temp
                from cr in temp.DefaultIfEmpty()
                select new { item.customer, item.subscription, item.country, stateId=int.Parse(cr.Value) };

            var contactsWithState = from item in contactsWithStateId
                join sp in _stateProvinceRepository.Table on item.stateId equals sp.Id
                    into temp
                from sp in temp.DefaultIfEmpty()
                select new
                {
                    item.subscription,
                    CountryName = item.country.Name,
                    CountryTwoLetterIsoCode = item.country.TwoLetterIsoCode,
                    StateProvinceName = sp.Name,
                    item.customer
                };

            var subscribers = (contactsWithState.ToList()).Select(item =>
            {
                var dto = new CreateContactRequest(item.subscription, inactiveStatus, sendWelcomeMessage)
                {
                    FirstName = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.FirstNameAttribute),
                    LastName = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.LastNameAttribute),
                    City = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.CityAttribute),
                    Address = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.StreetAddressAttribute),
                    PostalCode = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.ZipPostalCodeAttribute),
                    Gender = _genericAttributeService.GetAttribute<string>(item.customer, NopCustomerDefaults.GenderAttribute)?.ToLower(),
                    BirthDate = _genericAttributeService.GetAttribute<DateTime?>(item.customer, NopCustomerDefaults.DateOfBirthAttribute)?.ToString("yyyy-MM-dd")
                };

                if (!string.IsNullOrEmpty(item.CountryName))
                    dto.Country = item.CountryName;

                if (!string.IsNullOrEmpty(item.CountryTwoLetterIsoCode))
                    dto.CountryCode = item.CountryTwoLetterIsoCode;

                if (!string.IsNullOrEmpty(item.StateProvinceName))
                    dto.State = item.StateProvinceName;

                return (IBatchSupport)dto;
            }).ToList();

            return subscribers;
        }

        #endregion

        #region Methods

        #region Sync methods

        /// <summary>
        /// Synchronize contacts
        /// </summary>
        public void SyncContacts()
        {
            var store = _storeContext.CurrentStore;
            var su = PrepareNewsletterSubscribers(store.Id, 0, _omnisendSettings.PageSize);

            if (su.Count >= OmnisendDefaults.MinCountToUseBatch)
            {
                var page = 0;

                while (true)
                {
                    var data = JsonConvert.SerializeObject(new BatchRequest
                    {
                        Endpoint = OmnisendDefaults.ContactsEndpoint,
                        Items = su
                    });

                    var rez = _omnisendHttpClient.PerformRequest(OmnisendDefaults.BatchesApiUrl, data, HttpMethod.Post);

                    var bathId = JsonConvert.DeserializeAnonymousType(rez, new { batchID = "" })?.batchID;

                    if (bathId != null)
                    {
                        _omnisendSettings.BatchesIds.Add(bathId);
                        _settingService.SaveSetting(_omnisendSettings);
                    }

                    page++;

                    su = PrepareNewsletterSubscribers(store.Id, page, _omnisendSettings.PageSize);

                    if (!su.Any())
                        break;
                }
            }
            else
                foreach (var newsLetterSubscription in su)
                    UpdateOrCreateContact(newsLetterSubscription as CreateContactRequest);
        }

        /// <summary>
        /// Synchronize categories
        /// </summary>
        public void SyncCategories()
        {
            var categories = _categoryService.GetAllCategories(null, pageSize: _omnisendSettings.PageSize);

            if (categories.TotalCount >= OmnisendDefaults.MinCountToUseBatch || categories.TotalCount > _omnisendSettings.PageSize)
            {
                var page = 0;

                while (page < categories.TotalPages)
                {
                    var data = JsonConvert.SerializeObject(new BatchRequest
                    {
                        Endpoint = OmnisendDefaults.CategoriesEndpoint,
                        Items = categories.Select(category => CategoryToDto(category) as IBatchSupport).ToList()
                    });

                    var rez = _omnisendHttpClient.PerformRequest(OmnisendDefaults.BatchesApiUrl, data, HttpMethod.Post);

                    var bathId = JsonConvert.DeserializeAnonymousType(rez, new { batchID = "" })?.batchID;

                    if (bathId != null)
                    {
                        _omnisendSettings.BatchesIds.Add(bathId);
                        _settingService.SaveSetting(_omnisendSettings);
                    }

                    page++;

                    categories = _categoryService.GetAllCategories(null, pageIndex: page, pageSize: _omnisendSettings.PageSize);
                }
            }
            else
                foreach (var category in categories)
                {
                    var data = JsonConvert.SerializeObject(CategoryToDto(category));
                    _omnisendHttpClient.PerformRequest(OmnisendDefaults.CategoriesApiUrl, data, HttpMethod.Post);
                }
        }

        /// <summary>
        /// Synchronize products
        /// </summary>
        public void SyncProducts()
        {
            var products = _productService.SearchProducts(pageSize: _omnisendSettings.PageSize);

            if (products.TotalCount >= OmnisendDefaults.MinCountToUseBatch || products.TotalCount > _omnisendSettings.PageSize)
            {
                var page = 0;

                while (page < products.TotalPages)
                {
                    var data = JsonConvert.SerializeObject(new BatchRequest
                    {
                        Endpoint = OmnisendDefaults.ProductsEndpoint,
                        Items = products.Select(product => ProductToDto(product) as IBatchSupport).ToList()
                    });

                    var rez = _omnisendHttpClient.PerformRequest(OmnisendDefaults.BatchesApiUrl, data, HttpMethod.Post);

                    var bathId = JsonConvert.DeserializeAnonymousType(rez, new { batchID = "" })?.batchID;

                    if (bathId != null)
                    {
                        _omnisendSettings.BatchesIds.Add(bathId);
                        _settingService.SaveSetting(_omnisendSettings);
                    }

                    page++;

                    products = _productService.SearchProducts(pageIndex: page, pageSize: _omnisendSettings.PageSize);
                }
            }
            else
                foreach (var product in products)
                    AddNewProduct(product);
        }

        /// <summary>
        /// Synchronize orders
        /// </summary>
        public void SyncOrders()
        {
            var orders = _orderService.SearchOrders(pageSize: _omnisendSettings.PageSize);

            if (orders.TotalCount >= OmnisendDefaults.MinCountToUseBatch || orders.TotalCount > _omnisendSettings.PageSize)
            {
                var page = 0;

                while (page < orders.TotalPages)
                {
                    var data = JsonConvert.SerializeObject(new BatchRequest
                    {
                        Endpoint = OmnisendDefaults.OrdersEndpoint,
                        Items = orders.Select(order => OrderToDto(order) as IBatchSupport).ToList()
                    });

                    var rez = _omnisendHttpClient.PerformRequest(OmnisendDefaults.BatchesApiUrl, data, HttpMethod.Post);

                    var bathId = JsonConvert.DeserializeAnonymousType(rez, new { batchID = "" })?.batchID;

                    if (bathId != null)
                    {
                        _omnisendSettings.BatchesIds.Add(bathId);
                        _settingService.SaveSetting(_omnisendSettings);
                    }

                    page++;

                    orders = _orderService.SearchOrders(pageIndex: page, pageSize: _omnisendSettings.PageSize);
                }
            }
            else
                foreach (var order in orders)
                    CreateOrder(order);
        }

        /// <summary>
        /// Synchronize shopping carts
        /// </summary>
        public void SyncCarts()
        {
            var store = _storeContext.CurrentStore;
            var customers = _customerService.GetCustomersWithShoppingCarts(ShoppingCartType.ShoppingCart, store.Id);
            foreach (var customer in customers)
            {
                var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);
                CreateCart(cart);
            }
        }

        #endregion

        #region Configuration

        /// <summary>
        /// Gets the brand identifier 
        /// </summary>
        /// <param name="apiKey">API key to send request</param>
        /// <returns>
        /// A task that represents the asynchronous operation
        /// The task result contains the brand identifier or null
        /// </returns>
        public string GetBrandId(string apiKey)
        {
            _omnisendHttpClient.ApiKey = apiKey;

            var result = _omnisendHttpClient.PerformRequest<AccountsResponse>(OmnisendDefaults.AccountsApiUrl);

            return result?.BrandId;
        }

        /// <summary>
        /// Registers the site on the omnisend service
        /// </summary>
        public void SendCustomerData()
        {
            var site = _webHelper.GetStoreLocation();

            var data = JsonConvert.SerializeObject(new
            {
                website = site,
                platform = OmnisendDefaults.IntegrationOrigin,
                version = OmnisendDefaults.IntegrationVersion
            });

            _omnisendHttpClient.PerformRequest(OmnisendDefaults.AccountsApiUrl, data, HttpMethod.Post);
        }

        /// <summary>
        /// Gets information about batch
        /// </summary>
        /// <param name="bathId">Batch identifier</param>
        public BatchResponse GetBatchInfo(string bathId)
        {
            var url = OmnisendDefaults.BatchesApiUrl + $"/{bathId}";

            var result = _omnisendHttpClient.PerformRequest<BatchResponse>(url, httpMethod: HttpMethod.Get);

            return result;
        }

        #endregion

        #region Contacts

        /// <summary>
        /// Gets the contacts information
        /// </summary>
        /// <param name="contactId">Contact identifier</param>
        public ContactInfoDto GetContactInfo(string contactId)
        {
            var url = $"{OmnisendDefaults.ContactsApiUrl}/{contactId}";

            var res = _omnisendHttpClient.PerformRequest<ContactInfoDto>(url, httpMethod: HttpMethod.Get);

            return res;
        }

        /// <summary>
        /// Update or create contact information
        /// </summary>
        /// <param name="request">Create contact request</param>
        public void UpdateOrCreateContact(CreateContactRequest request)
        {
            var email = request.Identifiers.First().Id;
            var exists = !string.IsNullOrEmpty(_omnisendCustomerService.GetContactId(email));

            if (!exists)
            {
                var data = JsonConvert.SerializeObject(request);
                _omnisendHttpClient.PerformRequest(OmnisendDefaults.ContactsApiUrl, data, HttpMethod.Post);
            }
            else
            {
                var url = $"{OmnisendDefaults.ContactsApiUrl}?email={email}";
                var data = JsonConvert.SerializeObject(new { identifiers = new[] { request.Identifiers.First() } });

                _omnisendHttpClient.PerformRequest(url, data, HttpMethod.Patch);
            }
        }

        /// <summary>
        /// Update or create contact information
        /// </summary>
        /// <param name="subscription">Newsletter subscription</param>
        /// <param name="sendWelcomeMessage">Specifies whether to send a welcome message</param>
        public void UpdateOrCreateContact(NewsLetterSubscription subscription, bool sendWelcomeMessage = false)
        {
            var su = PrepareNewsletterSubscribers(subscription.StoreId,
                0, _omnisendSettings.PageSize,
                sendWelcomeMessage, subscription, "unsubscribed");

            if (su.FirstOrDefault() is CreateContactRequest request)
                UpdateOrCreateContact(request);
        }

        /// <summary>
        /// Updates contact information by customer data
        /// </summary>
        /// <param name="customer">Customer</param>
        public void UpdateContact(Customer customer)
        {
            var contactId = _omnisendCustomerService.GetContactId(customer.Email);

            if (string.IsNullOrEmpty(contactId))
                return;

            var url = $"{OmnisendDefaults.ContactsApiUrl}/{contactId}";

            var dto = new BaseContactInfoDto();

            FillCustomerInfo(dto, customer);

            var data = JsonConvert.SerializeObject(dto);

            _omnisendHttpClient.PerformRequest(url, data, HttpMethod.Patch);
        }

        #endregion

        #region Products

        /// <summary>
        /// Adds new product
        /// </summary>
        /// <param name="product">Product to add</param>
        public void AddNewProduct(Product product)
        {
            var data = JsonConvert.SerializeObject(ProductToDto(product));
            _omnisendHttpClient.PerformRequest(OmnisendDefaults.ProductsApiUrl, data, HttpMethod.Post);
        }

        /// <summary>
        /// Updates the product
        /// </summary>
        /// <param name="productId">Product identifier to update</param>
        public void UpdateProduct(int productId)
        {
            var product = _productService.GetProductById(productId);

            CreateOrUpdateProduct(product);
        }

        /// <summary>
        /// Updates the product
        /// </summary>
        /// <param name="product">Product to update</param>
        public void CreateOrUpdateProduct(Product product)
        {
            var result = _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.ProductsApiUrl}/{product.Id}", httpMethod: HttpMethod.Get);
            if (string.IsNullOrEmpty(result))
                AddNewProduct(product);
            else
            {
                var data = JsonConvert.SerializeObject(ProductToDto(product));
                _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.ProductsApiUrl}/{product.Id}", data, HttpMethod.Put);
            }
        }

        #endregion

        #region Shopping cart

        /// <summary>
        /// Restore the abandoned shopping cart
        /// </summary>
        /// <param name="cartId">Cart identifier</param>
        public void RestoreShoppingCart(string cartId)
        {
            var res = _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.CartsApiUrl}/{cartId}", httpMethod: HttpMethod.Get);

            if (string.IsNullOrEmpty(res))
                return;

            var restoredCart = JsonConvert.DeserializeObject<CartDto>(res)
                ?? throw new NopException("Cart data can't be loaded");

            var customer = _workContext.CurrentCustomer;
            var store = _storeContext.CurrentStore;
            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);

            foreach (var cartProduct in restoredCart.Products)
            {
                var combination = _productAttributeService.GetProductAttributeCombinationBySku(cartProduct.Sku);
                var product = combination == null ? _productService.GetProductBySku(cartProduct.Sku) : _productService.GetProductById(combination.ProductId);

                if (product == null)
                {
                    if (!int.TryParse(cartProduct.VariantId, out var variantId))
                        continue;

                    product = _productService.GetProductById(variantId);

                    if (product == null)
                        continue;
                }

                var shoppingCartItem = cart.FirstOrDefault(item => item.ProductId.ToString() == cartProduct.ProductId &&
                    item.Quantity == cartProduct.Quantity &&
                    item.AttributesXml == combination?.AttributesXml);
                
                if (shoppingCartItem is null)
                    _shoppingCartService.AddToCart(customer, product, ShoppingCartType.ShoppingCart, store.Id,
                        combination?.AttributesXml, quantity: cartProduct.Quantity);
            }
        }

        /// <summary>
        /// Adds new item to the shopping cart
        /// </summary>
        /// <param name="shoppingCartItem">Shopping cart item</param>
        public void AddShoppingCartItem(ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);

            if (!CanSendRequest(customer))
                return;

            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, shoppingCartItem.StoreId);

            if (cart.Count == 1)
                CreateCart(cart);
            else
                UpdateCart(customer, shoppingCartItem);
        }

        /// <summary>
        /// Updates the shopping cart item
        /// </summary>
        /// <param name="shoppingCartItem">Shopping cart item</param>
        public void EditShoppingCartItem(ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);

            if (!CanSendRequest(customer))
                return;

            var item = ShoppingCartItemToDto(shoppingCartItem, customer);

            var data = JsonConvert.SerializeObject(item);

            _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.CartsApiUrl}/{_omnisendCustomerService.GetCartId(customer)}/{OmnisendDefaults.ProductsEndpoint}/{item.CartProductId}", data, HttpMethod.Put);
        }

        /// <summary>
        /// Deletes item from shopping cart
        /// </summary>
        /// <param name="shoppingCartItem">Shopping cart item</param>
        public void DeleteShoppingCartItem(ShoppingCartItem shoppingCartItem)
        {
            var customer = _customerService.GetCustomerById(shoppingCartItem.CustomerId);

            if (!CanSendRequest(customer))
                return;

            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, shoppingCartItem.StoreId);

            //var sendRequest = await _omnisendCustomerService.IsNeedToSendDeleteShoppingCartEventAsync(customer);

            //if (sendRequest)
            //    await _omnisendHttpClient.PerformRequestAsync($"{OmnisendDefaults.CartsApiUrl}/{await _omnisendCustomerService.GetCartIdAsync(customer)}/{OmnisendDefaults.ProductsEndpoint}/{shoppingCartItem.Id}", httpMethod: HttpMethod.Delete);

            if (!cart.Any())
                //if (sendRequest)
                //    await _omnisendHttpClient.PerformRequestAsync($"{OmnisendDefaults.CartsApiUrl}/{await _omnisendCustomerService.GetCartIdAsync(customer)}", httpMethod: HttpMethod.Delete);
                _omnisendCustomerService.DeleteCurrentCustomerShoppingCartId(customer);
        }

        #endregion

        #region Orders

        /// <summary>
        /// Sends the new order to the omnisend service
        /// </summary>
        /// <param name="order">Order</param>
        public void PlaceOrder(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            if (!CanSendRequest(customer))
                return;

            //await CreateOrderAsync(order);

            _omnisendCustomerService.DeleteStoredCustomerShoppingCartId(customer);
        }

        /// <summary>
        /// Updates the order information
        /// </summary>
        /// <param name="order">Order</param>
        public void UpdateOrder(Order order)
        {
            var customer = _customerService.GetCustomerById(order.CustomerId);

            if (!CanSendRequest(customer))
                return;

            var item = OrderToDto(order);
            if (item is null)
                return;

            var data = JsonConvert.SerializeObject(item);
            _omnisendHttpClient.PerformRequest($"{OmnisendDefaults.OrdersApiUrl}/{item.OrderId}", data, HttpMethod.Put);
        }

        /// <summary>
        /// Store the CartId during order placing
        /// </summary>
        /// <param name="entity">Order item</param>
        public void OrderItemAdded(OrderItem entity)
        {
            var customer = _workContext.CurrentCustomer;
            var store = _storeContext.CurrentStore;
            var cart = _shoppingCartService.GetShoppingCart(customer, ShoppingCartType.ShoppingCart, store.Id);

            if (cart.Any(sci =>
                    sci.ProductId == entity.ProductId &&
                    sci.AttributesXml.Equals(entity.AttributesXml, StringComparison.InvariantCultureIgnoreCase) &&
                    sci.Quantity == entity.Quantity))
                _omnisendCustomerService.StoreCartId(customer);
        }

        #endregion


        #endregion
    }
}
