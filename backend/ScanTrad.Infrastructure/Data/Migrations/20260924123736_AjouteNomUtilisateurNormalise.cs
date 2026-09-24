using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ScanTrad.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AjouteNomUtilisateurNormalise : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.AddColumn<string>(
                name: "NormalizedUsername",
                table: "Users",
                type: "TEXT",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Les comptes déjà présents recevraient tous "" : l'index unique créé
            // juste après échouerait dès deux comptes. On calcule donc leur nom
            // normalisé, comme User.NormalizeUsername (UPPER suffit : les noms
            // d'utilisateur ne contiennent que des caractères ASCII).
            migrationBuilder.Sql("UPDATE Users SET NormalizedUsername = UPPER(Username);");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users",
                column: "NormalizedUsername",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_NormalizedUsername",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "NormalizedUsername",
                table: "Users");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }
    }
}
