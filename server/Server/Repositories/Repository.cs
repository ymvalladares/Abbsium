using Microsoft.EntityFrameworkCore;
using Server.Data;
using Server.Repositories.IRepositories;
using System.Linq.Expressions;

namespace Server.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        private readonly DbContext_app _db;
        internal DbSet<T> DbSet;

        public Repository(DbContext_app db)
        {
            _db = db;
            DbSet = _db.Set<T>();
        }

        public void Add(T entity)
        {
            DbSet.Add(entity);
        }

        public IEnumerable<T> GetAll(Expression<Func<T, bool>>? expression = null, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = DbSet;
            if (expression != null)
            {
                query = query.Where(expression);
            }
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return query.ToList();
        }

        public T GetFirstOrDefault(Expression<Func<T, bool>> expression, params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = DbSet;
            foreach (var include in includes)
            {
                query = query.Include(include);
            }
            return query.Where(expression).FirstOrDefault();
        }

        public void Remove(T entity)
        {
            DbSet.Remove(entity);
        }

        public void RemoveRange(IEnumerable<T> entities)
        {
            DbSet.RemoveRange(entities);
        }
    }
}
