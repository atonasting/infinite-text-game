﻿// <auto-generated />
using System;
using InfiniteTextGame.Lib;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace InfiniteTextGame.Lib.Migrations
{
    [DbContext(typeof(ITGDbContext))]
    [Migration("20230813105300_Init")]
    partial class Init
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "7.0.10");

            modelBuilder.Entity("InfiniteTextGame.Models.Story", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Closed")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsPublic")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Model")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("StylePrompt")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("UpdateTime")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Stories");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.StoryChapter", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("CompletionTokens")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Content")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("TEXT");

                    b.Property<bool>("Deleted")
                        .HasColumnType("INTEGER");

                    b.Property<long?>("PreviousChapterId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("PreviousOptionOrder")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PreviousSummary")
                        .HasColumnType("TEXT");

                    b.Property<int>("PromptTokens")
                        .HasColumnType("INTEGER");

                    b.Property<Guid>("StoryId")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long>("UseTime")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("PreviousChapterId");

                    b.HasIndex("StoryId");

                    b.ToTable("StoryChapters");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.StoryChapterOption", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<long>("ChapterId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsContinue")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<int>("Order")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("ChapterId");

                    b.ToTable("StoryChapterOptions");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.WritingStyle", b =>
                {
                    b.Property<long>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreateTime")
                        .HasColumnType("TEXT");

                    b.Property<string>("KeyWords")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Source")
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("UpdateTime")
                        .HasColumnType("TEXT");

                    b.Property<int>("UseTimes")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.ToTable("WritingStyles");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.StoryChapter", b =>
                {
                    b.HasOne("InfiniteTextGame.Models.StoryChapter", "PreviousChapter")
                        .WithMany("NextChapters")
                        .HasForeignKey("PreviousChapterId");

                    b.HasOne("InfiniteTextGame.Models.Story", "Story")
                        .WithMany("Chapters")
                        .HasForeignKey("StoryId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("PreviousChapter");

                    b.Navigation("Story");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.StoryChapterOption", b =>
                {
                    b.HasOne("InfiniteTextGame.Models.StoryChapter", "Chapter")
                        .WithMany("Options")
                        .HasForeignKey("ChapterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Chapter");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.Story", b =>
                {
                    b.Navigation("Chapters");
                });

            modelBuilder.Entity("InfiniteTextGame.Models.StoryChapter", b =>
                {
                    b.Navigation("NextChapters");

                    b.Navigation("Options");
                });
#pragma warning restore 612, 618
        }
    }
}
