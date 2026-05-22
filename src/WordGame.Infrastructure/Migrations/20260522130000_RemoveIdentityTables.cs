using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using WordGame.Infrastructure.Persistence;

#nullable disable

namespace WordGame.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260522130000_RemoveIdentityTables")]
    public partial class RemoveIdentityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetRoleClaims\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetUserClaims\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetUserLogins\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetUserRoles\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetUserTokens\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetRoles\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"AspNetUsers\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
