using InfiniteTextGame.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfiniteTextGame.Lib
{
    public class ITGDbContext : DbContext
    {
        public ITGDbContext(DbContextOptions<ITGDbContext> options)
            : base(options) { }

        public DbSet<Story> Stories { get; set; }
        public DbSet<StoryChapter> StoryChapters { get; set; }
        public DbSet<StoryChapterOption> StoryChapterOptions { get; set; }
        public DbSet<WritingStyle> WritingStyles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //add index and relations below.
        }
    }
}
