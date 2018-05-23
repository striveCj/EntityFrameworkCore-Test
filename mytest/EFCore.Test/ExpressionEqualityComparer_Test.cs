using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace EFCore.Test
{
    public class ExpressionEqualityComparer_Test
    {
        [Fact]
        public void GetHashCode_Test()
        {

            for (var index = 0; index < 1000; index++)
            {
                GetUserById(index);
            }

            using (var testDbContext = new TestDbContext())
            {
                var memoryCache= testDbContext.GetInfrastructure().GetRequiredService<IMemoryCache>();
            }
        }

        
        public UserEntity GetUserById(int userId)
        {
            using (var testDbContext = new TestDbContext())
            {
                return testDbContext.Set<UserEntity>().SingleOrDefault(e => e.Id == userId);
            }
        }


        public class TestDbContext:DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseInMemoryDatabase(typeof(TestDbContext).Name);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<UserEntity>();
            }
        }


        public class UserEntity
        {
            public int Id { get; set; }

            public string Name { get; set; }
        }
    }
}
 
