using System;
using System.Linq.Expressions;

namespace SimplCommerce.Module.Catalog.Models
{
    public static class ProductLowStockRule
    {
        public static readonly Expression<Func<Product, bool>> IsLowStock = product =>
            product.StockTrackingIsEnabled &&
            product.LowStockThreshold.HasValue &&
            product.StockQuantity <= product.LowStockThreshold.Value;
    }
}
