using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SFE.Application.Interfaces;

namespace SFE.Infrastructure.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(AppDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<List<T>> GetAllAsync()
    {
        // Use AsNoTracking to prevent state contention and context locking
        return await _dbSet.AsNoTracking().ToListAsync();
    }

    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        // Use AsNoTracking so fast navigation between tables doesn't cause a command overlap
        return await _dbSet.AsNoTracking().Where(predicate).ToListAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        var entry = await _dbSet.AddAsync(entity);
        return entry.Entity;
    }

    public virtual Task UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        return Task.CompletedTask;
    }

    public virtual async Task<bool> ExistsAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);
        if (entity != null)
        {
            // Détacher pour ne pas polluer le contexte
            _context.Entry(entity).State = EntityState.Detached;
        }
        return entity != null;
    }

    public virtual async Task<int> CountAsync()
    {
        return await _dbSet.CountAsync();
    }
}