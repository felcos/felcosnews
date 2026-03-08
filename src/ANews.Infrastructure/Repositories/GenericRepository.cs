using System.Linq.Expressions;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANews.Infrastructure.Repositories;

public class GenericRepository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _ctx;
    protected readonly DbSet<T> _set;

    public GenericRepository(AppDbContext ctx)
    {
        _ctx = ctx;
        _set = ctx.Set<T>();
    }

    public async Task<T?> GetByIdAsync(int id) => await _set.FindAsync(id);

    public async Task<IEnumerable<T>> GetAllAsync() => await _set.ToListAsync();

    public async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _set.Where(predicate).ToListAsync();

    public async Task<T> AddAsync(T entity)
    {
        await _set.AddAsync(entity);
        await _ctx.SaveChangesAsync();
        return entity;
    }

    public async Task<IEnumerable<T>> AddRangeAsync(IEnumerable<T> entities)
    {
        var list = entities.ToList();
        await _set.AddRangeAsync(list);
        await _ctx.SaveChangesAsync();
        return list;
    }

    public async Task UpdateAsync(T entity)
    {
        _set.Update(entity);
        await _ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _set.Remove(entity);
        await _ctx.SaveChangesAsync();
    }

    public async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
        => predicate == null
            ? await _set.CountAsync()
            : await _set.CountAsync(predicate);

    public async Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate)
        => await _set.AnyAsync(predicate);

    public IQueryable<T> Query() => _set.AsQueryable();

    public async Task<int> SaveChangesAsync() => await _ctx.SaveChangesAsync();
}
