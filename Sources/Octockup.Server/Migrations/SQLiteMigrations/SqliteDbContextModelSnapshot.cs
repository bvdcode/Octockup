﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Octockup.Server.Database;

#nullable disable

namespace Octockup.Server.Migrations.SQLiteMigrations
{
    [DbContext(typeof(SqliteDbContext))]
    partial class SqliteDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true);

            modelBuilder.Entity("Octockup.Server.Database.BackupTask", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("completed_at");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<TimeSpan>("Interval")
                        .HasColumnType("TEXT")
                        .HasColumnName("interval");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("INTEGER")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("is_enabled");

                    b.Property<bool>("IsNotificationEnabled")
                        .HasColumnType("INTEGER")
                        .HasColumnName("is_notification_enabled");

                    b.Property<string>("LastError")
                        .HasColumnType("TEXT")
                        .HasColumnName("last_error");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("name");

                    b.Property<string>("ParametersJson")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("parameters_json");

                    b.Property<double>("Progress")
                        .HasColumnType("REAL")
                        .HasColumnName("progress");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("provider");

                    b.Property<DateTime>("StartAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("start_at");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER")
                        .HasColumnName("status");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("backup_tasks");
                });

            modelBuilder.Entity("Octockup.Server.Database.Session", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("RefreshToken")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("refresh_token");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<int>("UserId")
                        .HasColumnType("INTEGER")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("sessions");
                });

            modelBuilder.Entity("Octockup.Server.Database.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER")
                        .HasColumnName("id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("created_at");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("email");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("password_hash_sha512");

                    b.Property<int>("Role")
                        .HasColumnType("INTEGER")
                        .HasColumnName("role");

                    b.Property<long?>("StorageLimitBytes")
                        .HasColumnType("INTEGER")
                        .HasColumnName("storage_limit_bytes");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("TEXT")
                        .HasColumnName("updated_at");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("TEXT")
                        .HasColumnName("username");

                    b.HasKey("Id");

                    b.ToTable("users");
                });

            modelBuilder.Entity("Octockup.Server.Database.BackupTask", b =>
                {
                    b.HasOne("Octockup.Server.Database.User", "User")
                        .WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("Octockup.Server.Database.Session", b =>
                {
                    b.HasOne("Octockup.Server.Database.User", "User")
                        .WithMany("Sessions")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("User");
                });

            modelBuilder.Entity("Octockup.Server.Database.User", b =>
                {
                    b.Navigation("Sessions");
                });
#pragma warning restore 612, 618
        }
    }
}
