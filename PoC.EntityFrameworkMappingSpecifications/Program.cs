using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PoC.EntityFrameworkMappingSpecifications
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var ctx = new CompanyContext())
            {
                ctx.Database.Delete();
                ctx.Database.Create();

                new PersistenceSpecification<Contact>(ctx)
                    .WithKey(x => x.ContactId)
                    .CheckProperty(x => x.Name, "Kris McGinnes")
                    .VerifyMappings();
            }
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }
    }

    public class PersistenceSpecification<TEntity>
    {
        private readonly DbContext _ctx;
        private readonly List<Property> _properties;
        private PropertyInfo _keyPropertyInfo;

        public PersistenceSpecification(DbContext ctx)
        {
            _ctx = ctx;
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
            Console.WriteLine("Creating instance of {0}", typeof(TEntity).Name);
            var expected = Activator.CreateInstance<TEntity>();
            foreach (var property in _properties)
            {
                Console.WriteLine("Setting value {0} on property {1}", property._value, property._propertyInfo.Name);
                property.SetValueOn(expected);
            }
            Console.WriteLine("Creating db set");
            var dbSet = _ctx.Set(typeof (TEntity));
            Console.WriteLine("Adding instance to db set");
            dbSet.Add(expected);
            Console.WriteLine("Saving changes to database");
            _ctx.SaveChanges();

            var actual = (TEntity) dbSet.Find(_keyPropertyInfo.GetValue(expected));
            foreach (var property in _properties)
            {
                Console.WriteLine("Verifying property value of {0} was sent to db", property._propertyInfo.Name);
                property.AssertValue(actual);
            }

            Console.WriteLine("Removing instance from db set");
            dbSet.Remove(expected);
            Console.WriteLine("Saving changes to database");
            _ctx.SaveChanges();
        }

        public class Property
        {
            public readonly PropertyInfo _propertyInfo;
            public readonly object _value;

            public Property(PropertyInfo propertyInfo, object value)
            {
                _propertyInfo = propertyInfo;
                _value = value;
            }

            public void SetValueOn(TEntity instance)
            {
                _propertyInfo.SetValue(instance, _value);
            }

            public void AssertValue(TEntity actual)
            {
                var actualValue = _propertyInfo.GetValue(actual).ToString() + "1";
                if (actualValue != _value)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Assertion failed! Expected: {0} Actual: {1}", _value, actualValue);
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Assertion Succeeded! Expected: {0} Actual: {1}", _value, actualValue);
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

    public class Contact
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContactId { get; set; }
        [Required]
        public string Name { get; set; }
        public Address Address { get; set; }
    }

    public class Address
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AddressId { get; set; }
    }

    public class CompanyContext : DbContext
    {
        public DbSet<Contact> Contacts { get; set; }

        public CompanyContext()
        {
        }
    }
}
