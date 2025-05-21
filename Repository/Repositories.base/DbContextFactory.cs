using App.Repository.Config;
using Entities.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;

namespace App.Repository
{
    public interface IDbContextFactory
    {
        EfDbContext DbContext { get; }

        IEnumerable<dynamic> GetDynamicResult(string commandText, params Microsoft.Data.SqlClient.SqlParameter[] parameters);
        DataTable DataTable(string sqlQuery, params DbParameter[] parameters);
        DataTable DataTable(string sqlQuery, CommandType? commandType = null, params DbParameter[] parameters);
    }

    public class DbContextFactory : IDbContextFactory, IDisposable
    {
        /// <summary>
        /// Create Db context with connection string
        /// </summary>
        /// <param name="settings"></param>
        public DbContextFactory() { }

        public DbContextFactory(IOptions<DbContextSettings> settings)
        {
            var options = new DbContextOptionsBuilder<EfDbContext>().UseSqlServer(settings.Value.DefaultConnection).Options;
            DbContext = new EfDbContext(options);
        }

        public EfDbContext GetDbContext(Microsoft.Extensions.Configuration.IConfiguration settings)
        {
            var options = new DbContextOptionsBuilder<EfDbContext>().UseSqlServer(settings["ConnectionStrings:DefaultConnection"]).Options;
            return new EfDbContext(options);
        }

        /// <summary>
        /// Call Dispose to release DbContext
        /// </summary>
        ~DbContextFactory()
        {
            Dispose();
        }

        public EfDbContext DbContext { get; private set; }

        public IEnumerable<dynamic> GetDynamicResult(string commandText, params Microsoft.Data.SqlClient.SqlParameter[] parameters)
        {
            // Get the connection from DbContext
            var connection = DbContext.Database.GetDbConnection();

            // Open the connection if isn't open
            if (connection.State != System.Data.ConnectionState.Open)
                connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;
                command.Connection = connection;

                if (parameters?.Length > 0)
                {
                    foreach (var parameter in parameters)
                    {
                        command.Parameters.Add(parameter);
                    }
                }

                using (var dataReader = command.ExecuteReader())
                {
                    // List for column names
                    var names = new List<string>();

                    if (dataReader.HasRows)
                    {
                        // Add column names to list
                        for (var i = 0; i < dataReader.VisibleFieldCount; i++)
                        {
                            names.Add(dataReader.GetName(i));
                        }

                        while (dataReader.Read())
                        {
                            // Create the dynamic result for each row
                            var result = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;

                            foreach (var name in names)
                            {
                                // Add key-value pair
                                // key = column name
                                // value = column value
                                result.Add(name, dataReader[name]);
                            }

                            yield return result;
                        }
                    }
                }
            }
        }

        public DataTable DataTable(string sqlQuery, params DbParameter[] parameters)
        {
            return DataTable(sqlQuery, null, parameters);
        }
        public DataTable DataTable(string sqlQuery, CommandType? commandType = null, params DbParameter[] parameters)
        {
            DataTable dataTable = new DataTable();
            DbConnection connection = DbContext.Database.GetDbConnection(); //context.Database.GetDbConnection();
            DbProviderFactory dbFactory = DbProviderFactories.GetFactory(connection);
            using (var cmd = dbFactory.CreateCommand())
            {
                cmd.Connection = connection;
                if (commandType.HasValue)
                {
                    cmd.CommandType = commandType.Value;
                }
                else
                {
                    cmd.CommandType = CommandType.Text;
                }

                cmd.CommandText = sqlQuery;
                if (parameters != null)
                {
                    foreach (var item in parameters)
                    {
                        cmd.Parameters.Add(item);
                    }
                }
                using (DbDataAdapter adapter = dbFactory.CreateDataAdapter())
                {
                    adapter.SelectCommand = cmd;
                    adapter.Fill(dataTable);
                }
            }
            return dataTable;
        }


        /// <summary>
        /// Release DB context
        /// </summary>
        public void Dispose()
        {
            DbContext?.Dispose();
        }
    }


    public static class IQueryableExtensions
    {

        #region Public Methods
        public static int CountWithNoLock<T>(this IQueryable<T> query, Expression<Func<T, bool>> expression = null)
        {
            using (var scope = CreateTransaction())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                int toReturn = query.Count();
                scope.Complete();
                return toReturn;
            }
        }

        public static async Task<int> CountWithNoLockAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default, Expression<Func<T, bool>> expression = null)
        {
            using (var scope = CreateTransactionAsync())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                int toReturn = await query.CountAsync(cancellationToken);
                scope.Complete();
                return toReturn;
            }
        }

        public static T FirstOrDefaultWithNoLock<T>(this IQueryable<T> query, Expression<Func<T, bool>> expression = null)
        {
            using (var scope = CreateTransaction())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                T result = query.FirstOrDefault();
                scope.Complete();
                return result;
            }
        }

        public static async Task<T> FirstOrDefaultWithNoLockAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default, Expression<Func<T, bool>> expression = null)
        {
            using (var scope = CreateTransactionAsync())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                T result = await query.FirstOrDefaultAsync(cancellationToken);
                scope.Complete();
                return result;
            }
        }

        public static List<T> ToListWithNoLock<T>(this IQueryable<T> query, Expression<Func<T, bool>> expression = null)
        {
            List<T> result = default;
            using (var scope = CreateTransaction())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                result = query.ToList();
                scope.Complete();
            }
            return result;
        }

        public static async Task<List<T>> ToListWithNoLockAsync<T>(this IQueryable<T> query, CancellationToken cancellationToken = default, Expression<Func<T, bool>> expression = null)
        {
            List<T> result = default;
            using (var scope = CreateTransactionAsync())
            {
                if (expression is object)
                {
                    query = query.Where(expression);
                }
                result = await query.ToListAsync(cancellationToken);
                scope.Complete();
            }
            return result;
        }

        #endregion


        #region Private Methods
        private static TransactionScope CreateTransactionAsync()
        {
            return new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions()
                {
                    IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted
                },
                TransactionScopeAsyncFlowOption.Enabled);
        }

        private static TransactionScope CreateTransaction()
        {
            return new TransactionScope(TransactionScopeOption.Required,
                new TransactionOptions()
                {
                    IsolationLevel = System.Transactions.IsolationLevel.ReadUncommitted
                });
        }
        #endregion
    }
}
