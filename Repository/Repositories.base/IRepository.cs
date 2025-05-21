using Microsoft.EntityFrameworkCore.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace App.Repository
{
    public interface IRepository<T> where T : class //, new()
    {
        IQueryable<T> Table { get; }
        IQueryable<T> TableNoTracking { get; }
        //Microsoft.EntityFrameworkCore.DbContext test { get; }
        void RemoveRange(System.Linq.Expressions.Expression<System.Func<T, bool>> predicate);
        void RemoveRange(IEnumerable<T> entities);
        T Add(T entity);
        Task<T> AddAsync(T entity);
        int Delete(T entity);
        Task<int> DeleteAsync(T entity);
        IQueryable<T> FromSql(string query);
        IQueryable<T> FromSql(string query, params object[] parameters);

        Task<T> GetEntity(object id);
        T Update(T entity);
        Task<T> UpdateAsync(T entity);
        Task SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        // List<T> GetCacheList();
        // UpdateCache(List<T> newlist, string dml);

        Task ExecuteSqlCommandAsync(string query);
        Task ExecuteSqlCommandAsync(string query, params object[] parameters);
        void ExecuteSqlCommand(string query, params object[] parameters);
    }
}