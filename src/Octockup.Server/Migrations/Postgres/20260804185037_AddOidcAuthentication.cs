using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Octockup.Server.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class AddOidcAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_disabled",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "authentication_settings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    password_login_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authentication_settings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oidc_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    public_base_url = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    client_id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    client_secret_encrypted = table.Column<string>(type: "text", nullable: true),
                    scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oidc_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "oidc_login_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    code_verifier_encrypted = table.Column<string>(type: "text", nullable: false),
                    nonce_encrypted = table.Column<string>(type: "text", nullable: false),
                    return_url = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    link_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oidc_login_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_oidc_login_states_oidc_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "oidc_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_oidc_login_states_users_link_user_id",
                        column: x => x.link_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_external_identities",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issuer = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    display_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    last_used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_external_identities_oidc_providers_provider_id",
                        column: x => x.provider_id,
                        principalTable: "oidc_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_external_identities_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authentication_settings_name",
                table: "authentication_settings",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_expires_at",
                table: "oidc_login_states",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_link_user_id",
                table: "oidc_login_states",
                column: "link_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_provider_id",
                table: "oidc_login_states",
                column: "provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_oidc_login_states_state_hash",
                table: "oidc_login_states",
                column: "state_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_oidc_providers_slug",
                table: "oidc_providers",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_identities_provider_id_subject",
                table: "user_external_identities",
                columns: new[] { "provider_id", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_external_identities_user_id_provider_id",
                table: "user_external_identities",
                columns: new[] { "user_id", "provider_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authentication_settings");

            migrationBuilder.DropTable(
                name: "oidc_login_states");

            migrationBuilder.DropTable(
                name: "user_external_identities");

            migrationBuilder.DropTable(
                name: "oidc_providers");

            migrationBuilder.DropColumn(
                name: "is_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_disabled",
                table: "users");
        }
    }
}
