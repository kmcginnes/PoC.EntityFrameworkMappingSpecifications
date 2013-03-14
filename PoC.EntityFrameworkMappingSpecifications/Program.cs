using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;

namespace PoC.EntityFrameworkMappingSpecifications
{
    class Program
    {
        static void Main(string[] args)
        {
            new PersistenceSpecification<Contact>(() => new CompanyContext())
                .WithKey(x => x.ContactId)
                .CheckProperty(x => x.Name, "Kris McGinnes")
                .CheckProperty(x => x.Age, 29)
                .CheckProperty(x => x.Address, new Address())
                .VerifyMappings();
            Console.WriteLine("Press any key to quit...");
            Console.ReadKey();
        }
    }

    public class Contact
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int ContactId { get; set; }
        [Required]
        public string Name { get; set; }
        [Required]
        public int Age { get; set; }
        [Required]
        public virtual Address Address { get; set; }

        public override int GetHashCode()
        {
            return ContactId.GetHashCode();
        }
    }

    public class Address
    {
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int AddressId { get; set; }

        public override int GetHashCode()
        {
            return AddressId.GetHashCode();
        }
    }

    public class CompanyContext : DbContext
    {
        public DbSet<Contact> Contacts { get; set; }

        public CompanyContext()
        {
        }
    }
}
