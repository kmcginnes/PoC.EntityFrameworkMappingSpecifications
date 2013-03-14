using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.Metadata.Edm;
using System.Data.SqlClient;
using System.Linq.Expressions;
using System.Reflection;
using System.Transactions;
using EntityFramework.Mapping;

namespace PoC.EntityFrameworkMappingSpecifications
{
    public class PersistenceSpecification<TEntity> 
        where TEntity : class
    {
        private readonly Func<DbContext> _createContext;
        private readonly List<Property> _properties;
        private readonly List<Reference> _references;
	    private ReadOnlyMetadataCollection<EdmMember> _keyMembers;

        public PersistenceSpecification(Func<DbContext> createContext)
        {
            _createContext = createContext;
            _properties = new List<Property>();
            _references = new List<Reference>();
        }

        public PersistenceSpecification<TEntity> CheckProperty<TProperty>(Expression<Func<TEntity, TProperty>> property, object value)
        {
            var propertyName = GetPropertyName(property);
            var propertyInfo = typeof (TEntity).GetProperty(propertyName);
            _properties.Add(new Property(propertyInfo, value));
            return this;
        }

        public PersistenceSpecification<TEntity> CheckReference<TProperty>(Expression<Func<TEntity, TProperty>> property, object value)
        {
            var propertyName = GetPropertyName(property);
            var propertyInfo = typeof(TEntity).GetProperty(propertyName);
            _references.Add(new Reference(propertyInfo, value));
            return this;
        }

        public void VerifyMappings()
        {
            try
            {
                Database.SetInitializer<CompanyContext>(null);

                using (new TransactionScope())
                {
                    object[] id;
                    using (var ctx = _createContext())
                    {
                        var objectContext = ((IObjectContextAdapter) ctx).ObjectContext;
                        var entitySet = objectContext.GetEntitySet<TEntity>();
                        _keyMembers = entitySet.ElementType.KeyMembers;

                        var expected = CreateEntity(ctx);
                        SetPropertiesOnEntity(expected);

                        SaveEntityToDb(ctx, expected);
                        id = GetKeyValue(expected);
                    }

                    using (var ctx = _createContext())
                    {
                        var actual = GetActualEntity(ctx, id);
                        AssertPropertyValues(actual);
                        AssertReferenceValues(ctx, actual);
                    }

                    throw new CoastIsClearException();
                }
            }
            catch (CoastIsClearException)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Rolling back all transactions");
                Console.ResetColor();
            }
            catch (DbEntityValidationException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                foreach (var eve in e.EntityValidationErrors)
                {
                    Console.WriteLine("Entity of type \"{0}\" in state \"{1}\" has the following validation errors:",
                        eve.Entry.Entity.GetType().Name, eve.Entry.State);
                    foreach (var ve in eve.ValidationErrors)
                    {
                        Console.WriteLine("- Property: \"{0}\", Error: \"{1}\"",
                            ve.PropertyName, ve.ErrorMessage);
                    }
                }
                Console.WriteLine("Rolling back all transactions");
                Console.ResetColor();
            }
            catch (Exception exception)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: {0}", exception.GetBaseException().Message);
                Console.WriteLine("Rolling back all transactions");
                Console.ResetColor();
            }
        }

        private string GetPropertyName<TProperty>(Expression<Func<TEntity, TProperty>> property)
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

        private void AssertPropertyValues(TEntity actual)
        {
            foreach (var property in _properties)
            {
                Console.WriteLine("Verifying property value of {0} was sent to db", property.PropertyInfo.Name);
                property.AssertValue(actual);
            }
        }

        private void AssertReferenceValues(DbContext ctx, TEntity actual)
        {
            foreach (var reference in _references)
            {
                Console.WriteLine("Verifying reference value of {0} was sent to db", reference.PropertyInfo.Name);
                reference.AssertValue(ctx, actual);
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
                throw;
            }
        }

        private void SetPropertiesOnEntity(TEntity expected)
        {
            foreach (var property in _properties)
            {
                Console.WriteLine("Setting value {0} on property {1}",
                    property.Value, property.PropertyInfo.Name);
                property.SetValueOn(expected);
            }
            foreach (var property in _references)
            {
                Console.WriteLine("Setting value {0} on property {1}",
                    property.Value, property.PropertyInfo.Name);
                property.SetValueOn(expected);
            }
        }

        private TEntity CreateEntity(DbContext ctx)
        {
            Console.WriteLine("Creating instance of {0}", typeof (TEntity).Name);
            var entity = GetDbSet(ctx).Create<TEntity>();
            return entity;
        }

        private object[] GetKeyValue(TEntity expected)
        {
            var keyValues = new List<object>();
            foreach (var keyMember in _keyMembers)
            {
                var propertyInfo = typeof (TEntity).GetProperty(keyMember.Name);
                var value = propertyInfo.GetValue(expected);
                keyValues.Add(value);
            }
            return keyValues.ToArray();
        }

        private TEntity GetActualEntity(DbContext ctx, object[] id)
        {
            var dbSet = GetDbSet(ctx);
            return dbSet.Find(id);
        }

        private DbSet<TEntity> GetDbSet(DbContext ctx)
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
                if (!actualValue.Equals(Value))
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

        public class Reference
        {
            public PropertyInfo PropertyInfo { get; private set; }
            public object Value { get; private set; }

            public Reference(PropertyInfo propertyInfo, object value)
            {
                PropertyInfo = propertyInfo;
                Value = value;
            }

            public void SetValueOn(TEntity instance)
            {
                PropertyInfo.SetValue(instance, Value);
            }

            public void AssertValue(DbContext ctx, TEntity actual)
            {
                var propertyType = PropertyInfo.PropertyType;

                var objectContext = ((IObjectContextAdapter)ctx).ObjectContext;
                var entitySet = objectContext.GetEntitySet(propertyType);
                var keyMembers = entitySet.ElementType.KeyMembers;

                var actualValue = PropertyInfo.GetValue(actual);
                foreach (var keyMember in keyMembers)
                {
                    var actualKeyValue = propertyType.GetProperty(keyMember.Name).GetValue(actualValue);
                    var expectedKeyValue = propertyType.GetProperty(keyMember.Name).GetValue(Value);
                    if (!actualKeyValue.Equals(expectedKeyValue))
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
        }
    }

    public class CoastIsClearException : Exception
    {
    }
}