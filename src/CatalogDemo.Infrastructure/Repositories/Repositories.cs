using Microsoft.EntityFrameworkCore;
using CatalogDemo.Application.Interfaces;
using CatalogDemo.Domain.Entities;

namespace CatalogDemo.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly Data.ApplicationDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(Data.ApplicationDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await _dbSet.AsNoTracking().ToListAsync(ct);

    public async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
        await _context.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => await _dbSet.FindAsync(new object[] { id }, ct) is not null;
}

public class CategoryRepository : Repository<Category>, ICategoryRepository
{
    public CategoryRepository(Data.ApplicationDbContext context) : base(context) { }
}

public class ProductRepository : Repository<Product>, IProductRepository
{
    public ProductRepository(Data.ApplicationDbContext context) : base(context) { }
}