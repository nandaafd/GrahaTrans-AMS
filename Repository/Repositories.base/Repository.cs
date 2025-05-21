using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace App.Repository
{
    /// <summary>
    /// Generic Repository
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly IDbContextFactory _dbContextFactory;
        protected Serilog.ILogger _logger;

        public Repository(IDbContextFactory dbContextFactory, Serilog.ILogger logger)
        {
            _dbContextFactory = dbContextFactory;
            _logger = logger;
        }



        #region old dbContextFactory with cache
        /*
		private readonly IMemoryCache _cache = null;
		private MemoryCacheEntryOptions _cacheOption = null;
		private string _cacheKey = "";
		public Repository(IDbContextFactory dbContextFactory, ILogger logger, IMemoryCache cache)
		{
			_dbContextFactory = dbContextFactory;
			_logger = logger;

			var model = _dbContextFactory.DbContext.Model;
			var entityTypes = model.GetEntityTypes();
			var entityType = entityTypes.First(t => t.ClrType == typeof(T));
			var tableNameAnnotation = entityType.GetAnnotation("Relational:TableName");

			_cacheKey = tableNameAnnotation.Value.ToString();
			_cache = cache;
			_cacheOption = new MemoryCacheEntryOptions().SetAbsoluteExpiration(relative: TimeSpan.FromHours(Constants.DEFAULT_CACHE_HOURS));
		}

		public List<T> GetCacheList()
		{
			try
			{
				return _cache.GetOrCreate(_cacheKey, entry =>
				{
					entry.SetOptions(_cacheOption);
					var list = DbSet.AsNoTracking().AsParallel().ToList(); //var list = DbContext.Set<T>().AsNoTracking().AsParallel().ToList();
					return list;
				});
			}
			catch //(Exception ex)
			{
				throw;
			}
		}
		*/
        #endregion

        protected DbContext DbContext => _dbContextFactory?.DbContext;

        protected virtual DbSet<T> DbSet
        {
            get
            {
                if (_dbSet == null)
                    _dbSet = DbContext.Set<T>();

                return _dbSet;
            }
        }
        private DbSet<T> _dbSet;


        // Gets a table with "no tracking" enabled (EF feature) Use it only when you load record(s) only for read-only operations
        public virtual IQueryable<T> TableNoTracking => DbSet.AsNoTracking();
        public virtual IQueryable<T> Table => DbSet;

        public void RemoveRange(System.Linq.Expressions.Expression<Func<T, bool>> predicate)
        {
            var records = DbSet.Where(predicate).ToList();
            if (records.Count() > 0) DbContext.RemoveRange(records);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            if (entities.Count() > 0) DbSet.RemoveRange(entities);
        }

        //public virtual DbContext test => _dbContextFactory?.DbContext;

        /// <summary>
        /// Get Entity
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<T> GetEntity(object id)
        {
            var entity = await DbContext.FindAsync<T>(id);
            return entity;
        }


        /// <summary>
        /// Add Entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<T> AddAsync(T entity)
        {
            try
            {
                var result = await DbContext.AddAsync<T>(entity);
                //var result = await DbSet.AddAsync(entity);
                await DbContext.SaveChangesAsync();
                return result.Entity;
            }
            catch (DbUpdateException exception)
            {
                //ensure that the detailed error text is saved in the Log
                throw new Exception(await GetFullErrorTextAndRollbackEntityChangesAsync(exception), exception);
            }
        }

        public T Add(T entity)
        {
            try
            {
                var result = DbSet.Add(entity); //DbContext.Add<T>(entity);
                DbContext.SaveChanges();
                return result.Entity;
            }
            catch (DbUpdateException exception)
            {
                //ensure that the detailed error text is saved in the Log
                throw new Exception(GetFullErrorTextAndRollbackEntityChanges(exception), exception);
            }
        }

        /// <summary>
        /// Update Entity
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public async Task<T> UpdateAsync(T entity)
        {
            try
            {
                var entry = DbContext.Entry(entity);
                if (entry.State == EntityState.Detached)
                {
                    DbSet.Attach(entity);
                }
                //DbSet.Attach(entity);
                entry.State = EntityState.Modified;
                await DbContext.SaveChangesAsync();
                return entity;

                // old
                //DbSet.Update(entity); //DbContext.Update<T>(entity);
                //await DbContext.SaveChangesAsync();
                //return entity;
            }
            catch (DbUpdateException exception)
            {
                //ensure that the detailed error text is saved in the Log
                throw new Exception(await GetFullErrorTextAndRollbackEntityChangesAsync(exception), exception);
            }
        }

        public T Update(T entity)
        {
            var entry = DbContext.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                DbSet.Attach(entity); //DbContext.Attach(entity);
            }
            entry.State = EntityState.Modified;

            DbSet.Update(entity); //DbContext.Update<T>(entity);
            DbContext.SaveChanges();
            return entity;
        }

        /// <summary>
        /// Delete Entity
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<bool> DeleteAsync(object id)
        {
            var entity = await DbContext.FindAsync<T>(id);
            if (entity != null)
            {
                DbContext.Remove<T>(entity);
                await DbContext.SaveChangesAsync();
            }
            return true;
        }

        public async Task<int> DeleteAsync(T entity)
        {
            var entry = DbContext.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                DbSet.Attach(entity);
            }
            entry.State = EntityState.Deleted;
            var retValue = await DbContext.SaveChangesAsync();
            return retValue;

            //var ret = 0;
            //if(entity != null)
            //{
            //	DbContext.Remove<T>(entity);
            //	ret = await DbContext.SaveChangesAsync();
            //}
            //return ret;
        }

        public int Delete(T entity)
        {
            var entry = DbContext.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                DbSet.Attach(entity);
            }
            entry.State = EntityState.Deleted;
            var retValue = DbContext.SaveChanges();
            return retValue;

            //var ret = 0;
            //if(entity != null)
            //{
            //	DbContext.Remove<T>(entity);
            //	ret = DbContext.SaveChanges();
            //}
            //return ret;
        }
        public async Task SaveChangesAsync()
        {
            await DbContext.SaveChangesAsync();
        }
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            return await DbContext.Database.BeginTransactionAsync();
        }
        public IQueryable<T> FromSql(string query, params object[] parameters)
        {
            var type = DbSet.FromSqlRaw(query, parameters);
            //var type = DbSet.FromSql(query, parameters); -> net core 2.x
            return type;
        }

        public IQueryable<T> FromSql(string query)
        {
#pragma warning disable EF1000 // Possible SQL injection vulnerability.
            var type = DbSet.FromSqlRaw(query);
            //var type = DbSet.FromSql(query); -> net core 2.x
            //var type = _dbContext.Set<T>().FromSql(query);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.
            return type;
        }

        public async Task ExecuteSqlCommandAsync(string query)
        {
            await DbContext.Database.ExecuteSqlRawAsync(query);
        }

        [Obsolete]
        public async Task ExecuteSqlCommandAsync(string query, params object[] parameters)
        {
#pragma warning disable EF1000 // Possible SQL injection vulnerability.
            await DbContext.Database.ExecuteSqlRawAsync(query, parameters);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.
        }

        public void ExecuteSqlCommand(string query)
        {
            DbContext.Database.ExecuteSqlRaw(query);
        }
        [Obsolete]
        public void ExecuteSqlCommand(string query, params object[] parameters)
        {
#pragma warning disable EF1000 // Possible SQL injection vulnerability.
            DbContext.Database.ExecuteSqlRaw(query, parameters);
#pragma warning restore EF1000 // Possible SQL injection vulnerability.
        }

        protected string GetFullErrorTextAndRollbackEntityChanges(DbUpdateException exception)
        {
            //rollback entity changes
            GetFullErrorCheckEntityState();

            try
            {
                DbContext.SaveChanges();
                return exception.ToString();
            }
            catch (Exception ex)
            {
                //if after the rollback of changes the context is still not saving,
                //return the full text of the exception that occurred when saving
                _logger.Error(ex, "Unhandled Exception");
                //	throw;
                return ex.ToString();
            }
        }
        protected async Task<string> GetFullErrorTextAndRollbackEntityChangesAsync(DbUpdateException exception)
        {
            //rollback entity changes
            GetFullErrorCheckEntityState();

            try
            {
                await DbContext.SaveChangesAsync();
                return exception.ToString();
            }
            catch (Exception ex)
            {
                //if after the rollback of changes the context is still not saving,
                //return the full text of the exception that occurred when saving
                _logger.Error(ex, "Unhandled-Exception-Repository");
                //	throw;
                return ex.ToString();
            }
        }

        private void GetFullErrorCheckEntityState()
        {
            //rollback entity changes
            if (DbContext is DbContext dbContext)
            {
                var entries = dbContext.ChangeTracker.Entries()
                        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified).ToList();

                entries.ForEach(entry =>
                {
                    try
                    {
                        entry.State = EntityState.Unchanged;
                    }
                    catch (InvalidOperationException)
                    {
                        // ignored
                    }
                });
            }
        }


        #region tobe updatecache
        /*
using App.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

		private readonly object s_syncObject = new object();
		public void UpdateCache(List<T> newlist, string dml)
		{
			lock(s_syncObject)
			{
				_cache.Remove(_cacheKey);
				_cache.Set(_cacheKey, newlist);
			}
		}
		*/
        #endregion


    }
}