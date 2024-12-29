﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Octockup.Server.Database;

#nullable disable

namespace Octockup.Server.Migrations.PostgresMigrations
{
    [DbContext(typeof(PostgresDbContext))]
    [Migration("20241229091217_AddLogs")]
    partial class AddLogs
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.0")
                .HasAnnotation("Proxies:ChangeTracking", false)
                .HasAnnotation("Proxies:CheckEquality", false)
                .HasAnnotation("Proxies:LazyLoading", true)
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("Octockup.Server.Database.BackupSnapshot", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<int>("BackupTaskId")
                        .HasColumnType("integer")
                        .HasColumnName("backup_task_id");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.PrimitiveCollection<string[]>("Files")
                        .IsRequired()
                        .HasColumnType("text[]")
                        .HasColumnName("files");

                    b.Property<string>("Log")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("log");

                    b.Property<long>("Size")
                        .HasColumnType("bigint")
                        .HasColumnName("size");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.HasKey("Id");

                    b.HasIndex("BackupTaskId");

                    b.ToTable("backup_snapshots");
                });

            modelBuilder.Entity("Octockup.Server.Database.BackupTask", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime?>("CompletedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("completed_at");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<TimeSpan>("Elapsed")
                        .HasColumnType("interval")
                        .HasColumnName("elapsed");

                    b.Property<bool>("ForceRun")
                        .HasColumnType("boolean")
                        .HasColumnName("force_run");

                    b.Property<TimeSpan>("Interval")
                        .HasColumnType("interval")
                        .HasColumnName("interval");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("boolean")
                        .HasColumnName("is_deleted");

                    b.Property<bool>("IsEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("is_enabled");

                    b.Property<bool>("IsNotificationEnabled")
                        .HasColumnType("boolean")
                        .HasColumnName("is_notification_enabled");

                    b.Property<string>("LastMessage")
                        .HasColumnType("text")
                        .HasColumnName("last_message");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("name");

                    b.Property<string>("ParametersJson")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("parameters_json");

                    b.Property<double>("Progress")
                        .HasColumnType("double precision")
                        .HasColumnName("progress");

                    b.Property<string>("Provider")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("provider");

                    b.Property<DateTime>("StartAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("start_at");

                    b.Property<int>("Status")
                        .HasColumnType("integer")
                        .HasColumnName("status");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("UserId")
                        .HasColumnType("integer")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("backup_tasks");
                });

            modelBuilder.Entity("Octockup.Server.Database.Session", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("RefreshToken")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("refresh_token");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<int>("UserId")
                        .HasColumnType("integer")
                        .HasColumnName("user_id");

                    b.HasKey("Id");

                    b.HasIndex("UserId");

                    b.ToTable("sessions");
                });

            modelBuilder.Entity("Octockup.Server.Database.User", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer")
                        .HasColumnName("id");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("created_at");

                    b.Property<string>("Email")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("email");

                    b.Property<string>("PasswordHash")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("password_hash_sha512");

                    b.Property<int>("Role")
                        .HasColumnType("integer")
                        .HasColumnName("role");

                    b.Property<long?>("StorageLimitBytes")
                        .HasColumnType("bigint")
                        .HasColumnName("storage_limit_bytes");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone")
                        .HasColumnName("updated_at");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasColumnType("text")
                        .HasColumnName("username");

                    b.HasKey("Id");

                    b.ToTable("users");
                });

            modelBuilder.Entity("Octockup.Server.Database.BackupSnapshot", b =>
                {
                    b.HasOne("Octockup.Server.Database.BackupTask", "BackupTask")
                        .WithMany()
                        .HasForeignKey("BackupTaskId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("BackupTask");
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