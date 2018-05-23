using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Xunit;

namespace EFCore.Test
{
     public class Generate_DbContext_Test
    {



        [Fact]
        public void Execute()
        {
            using (var generateDbContext = new GenerateDbContext())
            {
                generateDbContext.Database.EnsureCreated();

                var project = new Project();
                project.AddMember(new Member { Id = "100", Name = "1000" });
                project.SetId("100");

                generateDbContext.Set<Project>().Add(
                    project);

                generateDbContext.SaveChanges();
            }

            using (var generateDbContext = new GenerateDbContext())
            {
                var project = generateDbContext.Set<Project>().Single(e => e.Id == "100");
                Assert.NotNull(project);
            }
        }



        public class GenerateDbContext : DbContext
        {

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                var sqlConnectionStringBuilder = new SqlConnectionStringBuilder
                {
                    DataSource = "10.0.2.229",
                    InitialCatalog = "____testDataBase",
                    UserID = "sa",
                    Password = "w1!"
                };
                optionsBuilder.UseSqlServer(sqlConnectionStringBuilder.ConnectionString);


                base.OnConfiguring(optionsBuilder);
            }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Project>(
                    b =>
                    {
                        var converter = new ValueConverter<HashSet<Member>, string>(
                            v => JsonConvert.SerializeObject(v),
                            v => JsonConvert.DeserializeObject<HashSet<Member>>(v));

                        b.Property(x => x.Members)
                            .HasConversion(converter);
                    });
            }
        }


        public class Project
        {
            private HashSet<Member> _members = new HashSet<Member>();

            public void AddMember(Member member)
            {
                _members.Add(member);
            }

            public void SetId(string id)
            {
                Id = id;
            }

            public string Id { get; protected set; }

            public HashSet<Member> Members => _members;
        }

        public class Member
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

    }
}
