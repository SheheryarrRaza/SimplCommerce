using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SimplCommerce.Infrastructure.Data;
using SimplCommerce.Module.Catalog.Models;
using SimplCommerce.Module.Core.Data;
using SimplCommerce.Module.Core.Services;

namespace SimplCommerce.Module.Catalog.Services
{
    public class ProductService : IProductService
    {
        private const string ProductEntityTypeId = nameof(Product);

        private readonly IRepository<Product> _productRepository;
        private readonly IEntityService _entityService;
        private readonly SimplDbContext _dbContext;

        public ProductService(IRepository<Product> productRepository, IEntityService entityService, SimplDbContext dbContext)
        {
            _productRepository = productRepository;
            _entityService = entityService;
            _dbContext = dbContext;
        }

        public void Create(Product product)
        {
            using (var transaction = _productRepository.BeginTransaction())
            {
                product.Slug = _entityService.ToSafeSlug(product.Slug, product.Id, ProductEntityTypeId);
                _productRepository.Add(product);
                _productRepository.SaveChanges();

                _entityService.Add(product.Name, product.Slug, product.Id, ProductEntityTypeId);
                _productRepository.SaveChanges();

                transaction.Commit();
            }
        }

        public void Update(Product product)
        {
            var slug = _entityService.Get(product.Id, ProductEntityTypeId);
            if (product.IsVisibleIndividually)
            {
                product.Slug = _entityService.ToSafeSlug(product.Slug, product.Id, ProductEntityTypeId);
                if (slug != null)
                {
                    _entityService.Update(product.Name, product.Slug, product.Id, ProductEntityTypeId);
                }
                else
                {
                    _entityService.Add(product.Name, product.Slug, product.Id, ProductEntityTypeId);
                }
            }
            else
            {
                if (slug != null)
                {
                    _entityService.Remove(product.Id, ProductEntityTypeId);
                }
            }
            _productRepository.SaveChanges();
        }

        public async Task Delete(Product product)
        {
            product.IsDeleted = true;
            await _entityService.Remove(product.Id, ProductEntityTypeId);
            _productRepository.SaveChanges();
        }

        public async Task<IList<Product>> SearchBySkuOrName(string term)
        {
            var sql = $"SELECT * FROM Catalog_Product WHERE IsDeleted = 0 AND (Sku = '{term}' OR Name LIKE '%{term}%')";
            Console.WriteLine("Product search: " + sql);
            return await _dbContext.Set<Product>().FromSqlRaw(sql).Take(20).ToListAsync();
        }
    }
}
