using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Newtonsoft.Json;
using Xunit;

namespace EFCore.Test
{
    public class LazyLoadingProxyTests
    {

        [Fact]
        public void LazyLoader_Load_SqlServer_Test()
        {
            using (var context = new NorthwindContext())
            {

                var order = context.Orders.Single(item => item.OrderID == 10253);

                Assert.NotNull(order);
                order.OrderDate = DateTime.Now;
                Assert.NotNull(order.OrderDetails);

                Assert.Equal(3, order.OrderDetails.Count);

                var orderDetail = order.OrderDetails.First();

                Assert.Equal(order, orderDetail.Order);
                
            }
        }

        [Fact]
        public void LazyLoader_Load_InMemory_Test()
        {
            using (var testDbContext = new TestDbContext())
            {
                var entity = new Student
                {
                    Id = 101,
                    Name = "Student",
                    ClassId = 102
                };
                testDbContext.Set<Student>().Add(
                    entity);
                testDbContext.Set<Class>().Add(
                    new Class
                    {
                        Id = 102,
                        Name = "Student"
                    });
                //var project = new Project();
                //project.AddMember(new Member{Id = "100",Name = "1000"});
                //project.SetId("100");

                //testDbContext.Set<Project>().Add(
                //    project);

                testDbContext.SaveChanges();

                Assert.Equal(1, testDbContext.Set<Student>().Count());

                var student = testDbContext.Set<Student>().Single(e => e.Id == 101);

                Assert.Same(entity, student);
                Assert.NotNull(student.Class);
            }

            //using (var testDbContext = new TestDbContext())
            //{
            //    var project = testDbContext.Set<Project>().Single(e => e.Id == "100");
            //    Assert.NotNull(project);
            //}
        }

        private class TestDbContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder
                    .UseInMemoryDatabase(typeof(TestDbContext).FullName)
            //.UseLazyLoadingProxies();
            ;

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Student>();
                modelBuilder.Entity<Class>();
                //modelBuilder.Entity<Project>(
                //    b =>
                //    {
                //        // Mapping: 
                //        var converter = new ValueConverter<IEnumerable<Member>, string>(
                //            v => JsonConvert.SerializeObject(v),
                //            v => JsonConvert.DeserializeObject<IEnumerable<Member>>(v));

                //        b.Property(x => x.Members)
                //            .HasConversion(converter);
                //    });
            }
        }


        //public class Project
        //{
        //    private ISet<Member> _members = new HashSet<Member>();

        //    public void AddMember(Member member)
        //    {
        //        _members.Add(member);
        //    }

        //    public void SetId(string id)
        //    {
        //        Id = id;
        //    }
        //    public string Id { get; protected set; }

        //    public IEnumerable<Member> Members => _members;
        //}

        //public class Member
        //{
        //    public string Id { get;  set; }
        //    public string Name { get;  set; }
        //}

      



        public class Student
        {

            private ILazyLoader _lazyLoader;

            private Class _class;

            public Student(ILazyLoader lazyLoader)
            {
                _lazyLoader = lazyLoader;
            }

            public Student()
            {
            }

            public int Id { get; set; }

            public string Name { get; set; }


            public int ClassId { get; set; }

            public Class Class
            {
                get =>_lazyLoader.Load(this, ref _class);
                set => _class = value;
            }
        }

        public class Class
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }



}
