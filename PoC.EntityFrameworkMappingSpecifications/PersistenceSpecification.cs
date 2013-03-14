using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Reflection;
using System.Transactions;

namespace PoC.EntityFrameworkMappingSpecifications
{
    public class PersistenceSpecification<TEntity> 
        where TEntity : class
    {
        private readonly Func<DbContext> _createContext;
        private readonly List<Property> _properties;
        private PropertyInfo _keyPropertyInfo;

        public PersistenceSpecification(Func<DbContext> createContext)
        {
            _createContext = createContext;
            _properties = new List<Property>();
        }

        public PersistenceSpecification<TEntity> CheckProperty<TProperty>(Expression<Func<TEntity, TProperty>> property, object value)
        {
            var propertyName = GetPropertyName(property);

            var propertyInfo = typeof (TEntity).GetProperty(propertyName);

            _properties.Add(new Property(propertyInfo, value));

            return this;
        }

        private static string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> property)
        {
            var expression = property.Body as MemberExpression;
            if (expression == null || expression.Expression != property.Parameters[0]
                || expression.Member.MemberType != MemberTypes.Property)
            {
                throw new InvalidOperationException(
                    "Now tell me about the property");
            }
            var propertyName = expression.Member.Name;
            return propertyName;
        }

        public void VerifyMappings()
        {
            Database.SetInitializer<CompanyContext>(null);
            using (var ctx = _createContext())
            {
                ctx.Database.CreateIfNotExists();
                var expected = CreateEntity();
                SetPropertiesOnEntity(expected);
                SaveEntityToDb(ctx, expected);

                var id = GetKeyValue(expected);
                if (id == default(int)) return;

                var actual = GetActualEntity(ctx, id);
                AssertPropertyValues(actual);
                Cleanup(ctx, id);
            }
        }

        private void AssertPropertyValues(TEntity actual)
        {
            foreach (var property in _properties)
            {
                Console.WriteLine("Verifying property value of {0} was sent to db", property.PropertyInfo.Name);
                property.AssertValue(actual);
            }
        }

        private void SaveEntityToDb(DbContext ctx, TEntity expected)
        {
            Console.WriteLine("Creating db set");
            var dbSet = GetDbSet(ctx);
            try
            {
                Console.WriteLine("Adding instance to db set");
                dbSet.Add(expected);
                Console.WriteLine("Saving changes to database");
                ctx.SaveChanges();
                Console.WriteLine("Entity saved with id {0}", GetKeyValue(expected));
            }
            catch (Exception exception)
            {
                var baseException = exception.GetBaseException();
                if (baseException is SqlException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(baseException.Message);
                    Console.ResetColor();
                }
            }
        }

        private void SetPropertiesOnEntity(TEntity expected)
        {
            foreach (var property in _properties)
            {
                Console.WriteLine("Setting value {0} on property {1}", property.Value,
                                  property.PropertyInfo.Name);
                property.SetValueOn(expected);
            }
        }

        private static TEntity CreateEntity()
        {
            Console.WriteLine("Creating instance of {0}", typeof (TEntity).Name);
            var expected = Activator.CreateInstance<TEntity>();
            return expected;
        }

        private int GetKeyValue(TEntity expected)
        {
            return (int)_keyPropertyInfo.GetValue(expected);
        }

        private static void Cleanup(DbContext ctx, int id)
        {
            var dbSet = GetDbSet(ctx);
            var actual = GetActualEntity(ctx, id);
            Console.WriteLine("Removing instance from db set");
            dbSet.Remove(actual);
            Console.WriteLine("Saving changes to database");
            ctx.SaveChanges();
        }

        private static TEntity GetActualEntity(DbContext ctx, int id)
        {
            var dbSet = GetDbSet(ctx);
            return dbSet.Find(id);
        }

        private static DbSet<TEntity> GetDbSet(DbContext ctx)
        {
            return ctx.Set<TEntity>();
        }

        public class Property
        {
            public PropertyInfo PropertyInfo { get; private set; }
            public object Value { get; private set; }

            public Property(PropertyInfo propertyInfo, object value)
            {
                PropertyInfo = propertyInfo;
                Value = value;
            }

            public void SetValueOn(TEntity instance)
            {
                PropertyInfo.SetValue(instance, Value);
            }

            public void AssertValue(TEntity actual)
            {
                var actualValue = PropertyInfo.GetValue(actual);
                if (actualValue != Value)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Assertion failed! Expected: {0} Actual: {1}", Value, actualValue);
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Assertion Succeeded! Expected: {0} Actual: {1}", Value, actualValue);
                    Console.ResetColor();
                }
            }
        }

        public PersistenceSpecification<TEntity> WithKey<TProperty>(Expression<Func<TEntity, TProperty>> property)
        {
            var propertyName = GetPropertyName(property);
            var propertyInfo = typeof (TEntity).GetProperty(propertyName);
            _keyPropertyInfo = propertyInfo;
            return this;
        }
    }
}